using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HalconDotNet;

namespace PVI_WPF
{
    internal partial class MainViewModel : ObservableObject
    {
        #region 字段
        ImageService imageService;
        DispatcherTimer autoScrollTimer;

        DetectService detectService = new DetectService();
        public ObservableCollection<LogEntry> logs { get; } = new();

        public event Action<string>? ShowImageRequested;
        public event Action<string,List<PillResult>>? DetectionCompleted;
        #endregion

        #region 状态机
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


        [ObservableProperty] private ObservableCollection<PillResult> _pillResults = new();
        [ObservableProperty] private string _resultSummary = "未检测";

        [ObservableProperty] private DetectParams _params = new();//当前配置

        [ObservableProperty] private string _newName = "";            
        [ObservableProperty] private paramProfile? _slelctedParams;    
        [ObservableProperty] private ObservableCollection<paramProfile> _savedParams = new();  
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
        }
        #endregion

        #region Command
        [RelayCommand]
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
            
        }

        [RelayCommand]
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
            
        }

        [RelayCommand]
        private void LoadSavedParams()
        {
            if (SlelctedParams == null) {return; }

            DetectParams? loaded = detectService.Load(SlelctedParams.Name);
            if (loaded == null) { MessageBox.Show("该配置无法读取"); return; }

            Params = loaded;
            AddLog(LogLevelKind.Info, $"已加载配置:{SlelctedParams.Name}");
        }


        [RelayCommand]
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

        [RelayCommand]
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

                ResultSummary = $"合格 {ok}   NG {ng}   复检 {review}  耗时{sw.ElapsedMilliseconds} ms";
                AddLog(LogLevelKind.Info, $"{item.ImageName}检测完成,合格 {ok},NG {ng},复检 {review},耗时{sw.ElapsedMilliseconds}ms");
                DetectionCompleted?.Invoke(item.ImagePath,results);
            }
            catch(HalconException ex)
            {
                AddLog(LogLevelKind.Error, $"{item.ImageName}({imageService.CurrentIndex})检测失败");
            }
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
                autoScrollTimer.Interval = TimeSpan.FromMilliseconds(TimerInterval);
                autoScrollTimer.Start();
            }
            else
            {
                autoScrollTimer.Stop();
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
