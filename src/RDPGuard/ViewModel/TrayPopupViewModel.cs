using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RDPGuard.Entities;
using RDPGuard.Helper;
using RDPGuard.Manager;
using RDPGuard.Repositories;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace RDPGuard.ViewModel
{
    /// <summary>
    /// 托盘悬浮窗口 ViewModel
    /// </summary>
    public partial class TrayPopupViewModel : ObservableObject
    {
        public TrayPopupViewModel()
        {
            ReLoad();
        }

        #region 绑定属性

        /// <summary>
        /// 是否开机自启动
        /// </summary>
        [ObservableProperty]
        private bool isLaunchOnSysPowerOn;

        /// <summary>
        /// 当前封禁的 IP 数量
        /// </summary>
        [ObservableProperty]
        private int bannedCount;

        /// <summary>
        /// 今日登录尝试次数
        /// </summary>
        [ObservableProperty]
        private int todayAttemptsCount;

        /// <summary>
        /// 最近的 5 条远程登录记录
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<RdpLoginRecord> recentRecords = new();

        #endregion

        #region 命令

        /// <summary>
        /// 切换计划任务开机自启动（最高管理员权限）
        /// </summary>
        [RelayCommand]
        private void ToggleAutoStart()
        {
            if (IsLaunchOnSysPowerOn)
            {
                var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
                IsLaunchOnSysPowerOn = TaskSchedulerHelper.AddLaunchTask(AppGlobal.AppName, exePath);
            }
            else
            {
                TaskSchedulerHelper.Delete(AppGlobal.AppName);
            }
        }

        /// <summary>
        /// 打开主窗口
        /// </summary>
        [RelayCommand]
        private void OpenMainWindow()
        {
            Lactor.OpenMainWindow();
            NotifyIconManager.Close();
        }

        /// <summary>
        /// 退出应用
        /// </summary>
        [RelayCommand]
        private void Exit()
        {
            Environment.Exit(0);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 重新载入托盘状态
        /// </summary>
        public void ReLoad()
        {
            try
            {
                // 查询开机自启计划任务
                IsLaunchOnSysPowerOn = TaskSchedulerHelper.Get(AppGlobal.AppName) != null;

                // 统计数据
                var activeBans = BannedIpRepository.GetAllActive();
                BannedCount = activeBans.Count;

                var recent = RdpRecordRepository.GetRecentRecords(5);
                RecentRecords = new ObservableCollection<RdpLoginRecord>(recent);

                var today = DateTime.Today;
                TodayAttemptsCount = RdpRecordRepository.GetRecentRecords(1000).Count(r => r.Timestamp >= today);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReLoad 托盘数据异常: {ex.Message}");
            }
        }

        #endregion
    }
}
