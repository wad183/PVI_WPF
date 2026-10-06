using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PVI_WPF
{
    /// <summary>
    /// profileDialog.xaml 的交互逻辑
    /// </summary>
    public partial class profileDialog : Window
    {
        internal profileDialog(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
