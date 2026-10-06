using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HalconDotNet;

namespace PVI_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            vm = new MainViewModel();
            this.DataContext = vm;

            vm.ShowImageRequested += ShowImage;
            vm.DetectionCompleted += OnDetectionCompleted;
        }
        #region 检测标记
        private void OnDetectionCompleted(string path, List<PillResult> pills)
        {
            // ★ 先把底图重画一遍：HALCON 窗口不会自动擦，不重画的话上一次的框会留在上面
            //   用当前 image part 重画，所以缩放/平移不会丢
            if (_currentImage != null) HalconView.HalconWindow.DispObj(_currentImage);

            DrawBoxes(pills.Where(i => i.IsOK), "green");
            DrawBoxes(pills.Where(i => !i.IsOK && !i.IsIncomplete), "red");
            DrawBoxes(pills.Where(i => i.IsIncomplete), "yellow");
        }

        private void DrawBoxes(IEnumerable<PillResult> pills, string color)
        {
            var list = pills.ToList();
            if (list.Count == 0) return;

            HOperatorSet.GenRectangle2(out HObject boxes,
        new HTuple(list.Select(x => x.Row).ToArray()),
        new HTuple(list.Select(x => x.Col).ToArray()),
        new HTuple(list.Select(x => x.AngleDeg * Math.PI / 180.0).ToArray()),
        new HTuple(list.Select(x => x.Length / 2.0).ToArray()),
        new HTuple(list.Select(x => x.Width / 2.0).ToArray()));

            HalconView.HalconWindow.SetDraw("margin");
            HalconView.HalconWindow.SetColor(color);
            HalconView.HalconWindow.SetLineWidth(2);
            HalconView.HalconWindow.DispObj(boxes);
        }
        #endregion
        MainViewModel vm;
        HImage _currentImage;
        private void ShowImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return;
            try
            {
                HImage CurrentImage = new HImage(imagePath);
                _currentImage?.Dispose();
                
                _currentImage = CurrentImage;

                HalconView.HalconWindow.DispObj(CurrentImage);
                HalconView.SetFullImagePart();
            }
            catch (HalconException ex)
            {
                MessageBox.Show($"无法显示图像: {ex.GetErrorMessage}", "Error");
            }
        }


        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>快捷键：R=重新检测，←/→=上/下一页（也支持 PageUp/PageDown）</summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 正在输入框/下拉框里操作时不要抢键（否则参数页打 "r" 会触发重检）
            if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is ComboBox) return;

            switch (e.Key)
            {
                case Key.R:
                    if (vm.DetectCommand.CanExecute(null)) vm.DetectCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Left:
                case Key.PageUp:
                    if (vm.PreviousImageCommand.CanExecute(null)) vm.PreviousImageCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Right:
                case Key.PageDown:
                    if (vm.NextImageCommand.CanExecute(null)) vm.NextImageCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }

        private void set(object sender, RoutedEventArgs e)
        {
            vm.LoadSetWindow();                                     
            profileDialog profileDialog = new profileDialog(vm) { Owner = this };
            profileDialog.ShowDialog();
        }
    }
}