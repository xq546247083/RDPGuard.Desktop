using Hardcodet.Wpf.TaskbarNotification;
using System.Drawing;
using System.Reflection;
using System.Windows;

namespace RDPGuard.Manager;

/// <summary>
/// 托盘管理器
/// </summary>
public static class NotifyIconManager
{
    private static TaskbarIcon? taskbarIcon;

    /// <summary>
    /// 初始化托盘图标
    /// </summary>
    public static void Init()
    {
        taskbarIcon = new TaskbarIcon();

        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RDPGuard.Resources.app.ico");
            if (stream != null)
            {
                taskbarIcon.Icon = new Icon(stream);
            }
            else
            {
                taskbarIcon.Icon = SystemIcons.Shield;
            }
        }
        catch
        {
            taskbarIcon.Icon = SystemIcons.Shield;
        }

        taskbarIcon.ToolTipText = AppGlobal.AppChineseName;
        taskbarIcon.TrayPopup = Locator.TrayPopupControl;
        taskbarIcon.TrayMouseDoubleClick += TaskbarIcon_TrayMouseDoubleClick;
        taskbarIcon.TrayRightMouseUp += TaskbarIcon_TrayRightMouseUp;

        PreLoadNotifyUI();
    }

    /// <summary>
    /// 关闭托盘弹窗
    /// </summary>
    public static void Close()
    {
        taskbarIcon?.CloseTrayPopup();
    }

    /// <summary>
    /// 双击系统托盘图标
    /// </summary>
    private static void TaskbarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Locator.OpenMainWindow();
    }

    /// <summary>
    /// 右键点击系统托盘图标弹出悬浮窗口
    /// </summary>
    private static void TaskbarIcon_TrayRightMouseUp(object sender, RoutedEventArgs e)
    {
        taskbarIcon?.ShowTrayPopup();
    }

    /// <summary>
    /// 预热托盘 UI，避免初次弹出卡顿
    /// </summary>
    private static void PreLoadNotifyUI()
    {
        Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            var dummyWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Content = Locator.TrayPopupControl
            };

            dummyWindow.Show();
            dummyWindow.Content = null;
            dummyWindow.Close();

            if (taskbarIcon != null)
            {
                taskbarIcon.TrayPopup = Locator.TrayPopupControl;
            }
        }));
    }
}
