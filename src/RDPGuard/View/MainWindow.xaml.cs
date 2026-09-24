using RDPGuard.Manager;
using System.ComponentModel;
using System.Windows;

namespace RDPGuard
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = Lactor.MainViewModel;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 点击关闭时最小化到系统托盘，保持后台持续监听和防护
            e.Cancel = true;
            Hide();
        }
    }
}
