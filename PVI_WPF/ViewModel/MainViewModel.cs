using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;              // ★ 新增：新加的数据库代码用到 Path
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace PVI_WPF
{
    internal partial class MainViewModel : ObservableObject
    {
        #region 字段
        ImageService imageService;
        DispatcherTimer autoScrollTimer;

        DetectService detectService = new DetectService();
        DataService? dataService;                                   // ★ 新增：结果落库（初始化失败就为 null，不影响检测）
        public ObservableCollection<LogEntry> logs { get; } = new();

        public event Action<string>? ShowImageRequested;
        public event Action<string,List<PillResult>>? DetectionCompleted;

        public ICollectionView LogsItemsView { get; }
        #endregion


        #region 属性
        [ObservableProperty] private int _timerInterval = 1000; //ms

        [ObservableProperty] private bool _isAutoScroll = false;

        [ObservableProperty] private string _progressText = "0/0";

        [ObservableProperty] private string _folderPath = "无文件夹";

        [ObservableProperty] private string _currentFileName = "无文件";

        [ObservableProperty] private ObservableCollection<ImageItem> _images = new();

        [ObservableProperty] private ImageItem? _selectedImage;
        [ObservableProperty] private string _imageInfoText = "";

        [ObservableProperty] private string _statusText = "就绪";


        [ObservableProperty] private bool _onlyNg;                 // ★ 新增：只看NG
        [ObservableProperty] private string _historySummary = "";  // ★ 新增：查询结果摘要

        // ★ 新增：历史结果（从数据库查出来填这个）
        public ObservableCollection<ImageRow> HistoryRows { get; } = new();

        // ★ 新增：自动轮播统计（给"跑完打一条汇总日志"用）
        private int _autoPills, _autoNg;
        private readonly System.Diagnostics.Stopwatch _autoWatch = new();

        [ObservableProperty] private ObservableCollection<PillResult> _pillResults = new();
        [ObservableProperty] private string _resultSummary = "未检测";

        [ObservableProperty] private DetectParams _params = new();//当前配置

        [ObservableProperty] private string _newName = "";            
        [ObservableProperty] private paramProfile? _slelctedParams;    
        [ObservableProperty] private ObservableCollection<paramProfile> _savedParams = new();

        [ObservableProperty]private string _selectedFilter = "全部";
        #endregion

        #region 构造函数
        public MainViewModel()
        {
            imageService = new ImageService();
            autoScrollTimer = new DispatcherTimer();
            autoScrollTimer.Tick += AutoScrollTimer_Tick;

            

            AddLog(LogLevelKind.Info, $"HalconRoot = {App.HalconRoot}");
            AddLog(LogLevelKind.Info, $"配置文件路径{detectService.Floder}");
            AddLog(LogLevelKind.Warn, "测试Warn", "warn");
            AddLog(LogLevelKind.Error, "测试error", "错误");

            // ★ 新增：数据库。跟配置同一个可写目录（detectService.Floder 是 ...\profiles，取它上一层）
            //   用 try/catch 包住：数据库建不起来也不能让程序起不来
            try
            {
                string writableDir = Path.GetDirectoryName(detectService.Floder) ?? AppContext.BaseDirectory;
                dataService = new DataService(writableDir);
                var dbCounts = dataService.GetCounts();
                AddLog(LogLevelKind.Info, $"数据库={dataService.DbPath}  已有 {dbCounts.Images} 张图 / {dbCounts.Pills} 颗药");
            }
            catch (Exception ex)
            {
                AddLog(LogLevelKind.Warn, "数据库初始化失败：" + ex.Message + "（本次结果不会入库）");
            }

            LogsItemsView = CollectionViewSource.GetDefaultView(logs);
        }
        #endregion

        #region Command
        [RelayCommand]//保存配置
        private void SaveNewParam()
        {
            string name = (NewName ?? "").Trim();
            if (name.Length == 0) { MessageBox.Show("先给配置起个名字"); return; }

            if (SavedParams.Any(i => i.Name == name))
            {
                MessageBox.Show("该项已存在");
                return;
            }
            AddLog(LogLevelKind.Info, $"已保存配置:{name}");
            detectService.Save(name, Params);
            SavedParams.Add(new paramProfile { Name = name, detectParams = Params });
            MessageBox.Show($"已保存配置:{name}");
        }

        [RelayCommand]//删除配置
        private void DelaySavedParam()
        {
            if (SlelctedParams == null) return;
            if(MessageBox.Show($"确定删除{SlelctedParams.Name}?","删除",MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                return;
            }
            AddLog(LogLevelKind.Info, $"已删除配置{SlelctedParams.Name}");
            detectService.Delete(SlelctedParams.Name);
            SavedParams?.Remove(SlelctedParams);
            MessageBox.Show($"已删除配置{SlelctedParams.Name}");
        }

        [RelayCommand]//加载配置
        private void LoadSavedParams()
        {
            if (SlelctedParams == null) {return; }

            DetectParams? loaded = detectService.Load(SlelctedParams.Name);
            if (loaded == null) { MessageBox.Show("该配置无法读取"); return; }

            Params = loaded;
            AddLog(LogLevelKind.Info, $"已加载配置:{SlelctedParams.Name}");
            MessageBox.Show($"已加载配置:{SlelctedParams.Name}");
        }


        [RelayCommand]//选择文件夹
        private void SelectFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string selectedPath = dialog.SelectedPath; ;
                if(!imageService.LoadImagesFolder(selectedPath))
                {
                    MessageBox.Show("文件夹不存在","错误");
                }
                Images = new ObservableCollection<ImageItem>(imageService.ImageItems);
                SelectedImage = null;
                RefreshImage();
                RefreshUI();
                AddLog(LogLevelKind.Info, $"已加载文件夹{selectedPath},共{Images.Count}张");
            }
        }

        [RelayCommand] //检测 获取结果 更新检测结果 传参
        private void Detect()
        {
            if(imageService.CurrentImage is not { } item)
            {
                return;
            }
            try
            {
                var sw= System.Diagnostics.Stopwatch.StartNew();
                List<PillResult> results = detectService.Detect(item.ImagePath, Params);
                sw.Stop();

                PillResults.Clear();
                foreach (var r in results)
                {
                    PillResults.Add(r);
                }

                int ok = results.Count(i => i.IsOK);
                int ng = results.Count(i => !i.IsOK && !i.IsIncomplete);
                int review = results.Count(i => i.IsIncomplete);

                // ★ 新增：自动轮播期间累计（结束时会打一条汇总）
                if (IsAutoScroll) { _autoPills += results.Count; _autoNg += ng; }

                ResultSummary = $"合格 {ok}   NG {ng}   复检 {review}  耗时{sw.ElapsedMilliseconds} ms";
                AddLog(LogLevelKind.Info, $"{item.ImageName}检测完成,合格 {ok},NG {ng},复检 {review},耗时{sw.ElapsedMilliseconds}ms");

                // ★ 新增：结果写库（一次检测 = 1 行图片 + N 行药片；失败只记日志，绝不影响检测）
                try
                {
                    dataService?.Save(item.ImageName, item.ImagePath, results, Params, "", sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    AddLog(LogLevelKind.Warn, "结果写库失败：" + ex.Message);
                }

                DetectionCompleted?.Invoke(item.ImagePath,results);
            }
            catch(HalconException ex)
            {
                AddLog(LogLevelKind.Error, $"{item.ImageName}({imageService.CurrentIndex})检测失败");
            }
        }

        // ★ 新增：查历史结果（从数据库读，每张图取最新一次）
        [RelayCommand]
        private void QueryHistory()
        {
            if (dataService == null) { AddLog(LogLevelKind.Warn, "数据库不可用，查不了历史"); return; }

            HistoryRows.Clear();
            foreach (ImageRow row in dataService.QueryImages(OnlyNg))
                HistoryRows.Add(row);

            HistorySummary = $"共 {HistoryRows.Count} 张   NG {HistoryRows.Sum(r => r.NgCount)} 颗";
            AddLog(LogLevelKind.Info, $"查询历史：{HistorySummary}" + (OnlyNg ? "（只看NG）" : ""));
        }

        // ★ 新增：导出"当前筛选后的汇总"（一行一张图）
        //   改：不再要求"先点查询" —— 直接按当前筛选条件查一次库再导出，顺便刷新表格
        [RelayCommand]
        private void ExportHistory()
        {
            if (dataService == null) { MessageBox.Show("数据库不可用"); return; }

            List<ImageRow> rows;
            try
            {
                rows = dataService.QueryImages(OnlyNg);
            }
            catch (Exception ex)
            {
                AddLog(LogLevelKind.Error, "查询失败：" + ex.Message);
                MessageBox.Show("读取数据库失败：" + ex.Message);
                return;
            }

            if (rows.Count == 0) { MessageBox.Show("数据库里还没有结果，先检测几张图"); return; }

            // 顺手把界面上的表格也刷成"马上要导出的内容"
            HistoryRows.Clear();
            foreach (ImageRow row in rows) HistoryRows.Add(row);
            HistorySummary = $"共 {rows.Count} 张   NG {rows.Sum(r => r.NgCount)} 颗";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = $"检测汇总_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                InitialDirectory = GetWritableExportDir()      // ★ 改：桌面写不了就自动换到能写的目录
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                CsvExporter.ExportImages(dlg.FileName, rows);
                AddLog(LogLevelKind.Info, $"已导出汇总 {rows.Count} 条：{dlg.FileName}");
                MessageBox.Show("导出完成：\n" + dlg.FileName);
            }
            catch (Exception ex)
            {
                AddLog(LogLevelKind.Error, "导出汇总失败：" + ex.Message);
                // ★ 新增：把真实错误弹出来（原来只写日志，容易看不见）
                MessageBox.Show($"导出失败：{ex.Message}\n\n目标路径：{dlg.FileName}\n当前用户：{Environment.UserName}",
                                "导出失败");
            }
        }

        // ★ 新增：导出"当前这张图的药片明细"（一行一颗）
        [RelayCommand]
        private void ExportCurrent()
        {
            if (PillResults.Count == 0) { MessageBox.Show("当前没有检测结果"); return; }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = $"{CurrentFileName}_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                InitialDirectory = GetWritableExportDir()      // ★ 改：桌面写不了就自动换到能写的目录
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                CsvExporter.ExportPills(dlg.FileName, CurrentFileName, PillResults);
                AddLog(LogLevelKind.Info, $"已导出当前图明细 {PillResults.Count} 颗：{dlg.FileName}");
            }
            catch (Exception ex)
            {
                AddLog(LogLevelKind.Error, "导出明细失败：" + ex.Message);
                // ★ 新增：把真实错误弹出来
                MessageBox.Show($"导出失败：{ex.Message}\n\n目标路径：{dlg.FileName}\n当前用户：{Environment.UserName}",
                                "导出失败");
            }
        }

        /// <summary>
        /// ★ 新增：找一个"能写"的目录，当导出弹窗的默认位置。
        /// 有些受限环境（比如当前这个会话）连桌面都写不了，那就退到 exe 所在目录，
        /// 免得用户选完桌面才被 Windows 弹"你没有权限在此位置保存"。
        /// </summary>
        private string GetWritableExportDir()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            foreach (string dir in new[] { desktop, AppContext.BaseDirectory })
            {
                try
                {
                    string probe = Path.Combine(dir, "pvi_write_probe.tmp");
                    File.WriteAllText(probe, "x");
                    File.Delete(probe);

                    if (dir != desktop)
                        AddLog(LogLevelKind.Warn, $"桌面不可写，导出默认目录改为：{dir}");
                    return dir;
                }
                catch { /* 这个目录不行，换下一个 */ }
            }
            return AppContext.BaseDirectory;
        }

        [RelayCommand(CanExecute = nameof(CanNextImage))]
        private void NextImage()
        {
            imageService.Next();
            RefreshImage();
            RefreshUI();
        }
        private bool CanNextImage()
        {
            return imageService.ImageCount > 0 && imageService.CurrentIndex < imageService.ImageCount - 1;
        }

        [RelayCommand(CanExecute = nameof(CanPreviousImage))]
        private void PreviousImage()
        {
            if (IsAutoScroll == true) IsAutoScroll = false;
            imageService.Previous();
            RefreshImage();
            RefreshUI();
        }
        private bool CanPreviousImage()
        {
            return imageService.CurrentIndex > 0;
        }
        #endregion

        #region OnXXChanged

        partial void OnSelectedFilterChanged(string value)
        {
            if (value == "全部")
            {
                LogsItemsView.Filter = null;
            }
            else if (value == "警告")
            {
                LogsItemsView.Filter = i => i is LogEntry log && log.LevelText == "警告";
            }
            else if (value == "错误")
            {
                LogsItemsView.Filter = i => i is LogEntry log && log.LevelText == "错误";
            }
            else if (value == "信息")
            {
                LogsItemsView.Filter = i => i is LogEntry log && log.LevelText == "信息";
            }
            else
            {
                MessageBox.Show("选项不存在","错误");
            }
        }
        partial void OnParamsChanged(DetectParams value)
        {
            Detect();
        }
        partial void OnSelectedImageChanged(ImageItem value)
        {
            if (value != null)
            {
                imageService.CurrentIndex = value.ImageIndex;
                RefreshImage();
                RefreshUI();
            }
        }
        partial void OnIsAutoScrollChanged(bool value)
        {
            if (value)
            {
                // ★ 新增：开始自动时清零统计
                _autoPills = 0; _autoNg = 0; _autoWatch.Restart();

                autoScrollTimer.Interval = TimeSpan.FromMilliseconds(TimerInterval);
                autoScrollTimer.Start();
            }
            else
            {
                autoScrollTimer.Stop();

                // ★ 新增：自动跑完（或手动停）时，打一条汇总日志
                if (_autoWatch.IsRunning)
                {
                    _autoWatch.Stop();
                    AddLog(LogLevelKind.Info,
                        $"自动检测结束：{_autoPills} 颗 / NG {_autoNg} / {_autoWatch.Elapsed.TotalSeconds:F1} s");
                }
            }
        }
        partial void OnTimerIntervalChanged(int value)
        {
            if(autoScrollTimer.IsEnabled)
            autoScrollTimer.Interval = TimeSpan.FromMilliseconds(value);
        }
        #endregion
        private void AutoScrollTimer_Tick(object? sender, EventArgs e)
        {
            if(imageService.ImageCount == 0)
            {
                autoScrollTimer.Stop();
                IsAutoScroll = false;
                return;
            }
            if (CanNextImage())
            {
                NextImage();
            }
            else
            {
                autoScrollTimer.Stop();
                IsAutoScroll = false;
            }
        }

        //日志
        public void AddLog(LogLevelKind level,string message,string tag = "")
        {
            logs.Add(new LogEntry { level = level, Message = message, Tag = tag });
            while (logs.Count > 1000) logs.RemoveAt(0);
        }

        public void LoadSetWindow() //刷新窗口
        {
            SavedParams = new ObservableCollection<paramProfile>(detectService.LoadAll());
        }
        private void RefreshUI()
        {
            ProgressText =imageService.ImageCount>0 ?  $"{imageService.CurrentIndex + 1}/{imageService.ImageCount}" : "0/0";
            FolderPath = string.IsNullOrEmpty(imageService.FolderPath) ? "无文件夹" : imageService.FolderPath;
            CurrentFileName = imageService.CurrentImage?.FileName ?? "无文件";

            nextImageCommand?.NotifyCanExecuteChanged();
            previousImageCommand?.NotifyCanExecuteChanged();
        }
        private void RefreshImage()
        {

            if (imageService.CurrentImage is not { } item) return;

            ShowImageRequested?.Invoke(item.ImagePath);
            Detect();
        }
    }
}
