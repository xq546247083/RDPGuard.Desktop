using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using RDPGuard.Common;
using RDPGuard.Entities;
using RDPGuard.Enums;
using RDPGuard.Helper;
using RDPGuard.Manager;
using RDPGuard.Model;
using RDPGuard.Repositories;
using RDPGuard.Service;
using System.Collections.ObjectModel;
using System.Windows;

namespace RDPGuard.ViewModel
{
    /// <summary>
    /// 主界面 ViewModel
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly RDPEventWatcher _watcher = new();

        public MainViewModel()
        {
            SnackbarMessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

            _watcher.OnRdpEventReceived += OnRdpEventReceived;
            _watcher.Start();
        }

        #region 基础属性与选项卡

        [ObservableProperty]
        private string appTitle = $"{AppGlobal.AppChineseName} v1.0";

        [ObservableProperty]
        private AppTabType currentTab = AppTabType.AggregatedIps;

        [ObservableProperty]
        private string searchKeyword = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string busyMessage = string.Empty;

        public SnackbarMessageQueue SnackbarMessageQueue { get; }

        #endregion

        #region 核心数据集合

        /// <summary>
        /// 相同 IP 聚合显示列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<AggregatedIpSummary> aggregatedIps = new();

        /// <summary>
        /// 选中的聚合 IP
        /// </summary>
        [ObservableProperty]
        private AggregatedIpSummary? selectedAggregatedIp;

        /// <summary>
        /// 实时登录流水（包含成功和失败）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<RdpLoginRecord> realtimeRecords = new();

        /// <summary>
        /// 当前封禁黑名单
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<BannedIp> bannedIps = new();

        #endregion

        #region 设置与统计

        /// <summary>
        /// 开机自启动
        /// </summary>
        [ObservableProperty]
        private bool isLaunchOnSysPowerOn;

        /// <summary>
        /// 自动封禁阈值（失败超过此次数自动封禁，0 表示不自动封禁）
        /// </summary>
        [ObservableProperty]
        private int autoBanThreshold = 5;

        /// <summary>
        /// 自动封禁时间窗口（分钟，在此时间段内失败达到阈值即触发封禁，0 表示不限制时间窗口）
        /// </summary>
        [ObservableProperty]
        private int autoBanWindowMinutes = 10;

        /// <summary>
        /// 手动添加封禁的 IP 输入框
        /// </summary>
        [ObservableProperty]
        private string manualBanIpInput = string.Empty;

        /// <summary>
        /// 统计：总 IP 计数
        /// </summary>
        [ObservableProperty]
        private int totalUniqueIps;

        /// <summary>
        /// 统计：总拦截或失败次数
        /// </summary>
        [ObservableProperty]
        private int totalFailedCount;

        /// <summary>
        /// 统计：已封禁数
        /// </summary>
        [ObservableProperty]
        private int totalBannedCount;

        #endregion

        #region 业务命令

        /// <summary>
        /// 切换选项卡
        /// </summary>
        [RelayCommand]
        private void SwitchTab(AppTabType tab)
        {
            CurrentTab = tab;
        }

        /// <summary>
        /// 搜索过滤
        /// </summary>
        [RelayCommand]
        private void Search()
        {
            LoadAggregatedIps();
            LoadRealtimeRecords();
        }

        /// <summary>
        /// 重置搜索
        /// </summary>
        [RelayCommand]
        private void ClearSearch()
        {
            SearchKeyword = string.Empty;
            LoadAggregatedIps();
            LoadRealtimeRecords();
        }

        /// <summary>
        /// 封禁指定 IP
        /// </summary>
        [RelayCommand]
        private void BanIp(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return;

            ip = ip.Trim();
            var title = ResourceHelper.GetString("Str.Dialog.ConfirmBanTitle");
            var msg = ResourceHelper.GetString("Str.Dialog.ConfirmBanMsg", ip);
            var result = MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            var reason = ResourceHelper.GetString("Str.BanReason.Manual");
            var success = BannedIpRepository.BanIp(ip, reason);
            if (success)
            {
                ShowMessage(ResourceHelper.GetString("Str.Notify.BanSuccess", ip));
            }
            else
            {
                ShowMessage(ResourceHelper.GetString("Str.Notify.BanLimited", ip));
            }

            RefreshAll();
        }

        /// <summary>
        /// 解封指定 IP
        /// </summary>
        [RelayCommand]
        private void UnbanIp(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return;
            ip = ip.Trim();

            BannedIpRepository.UnbanIp(ip);
            ShowMessage(ResourceHelper.GetString("Str.Notify.UnbanSuccess", ip));
            RefreshAll();
        }

        /// <summary>
        /// 手动输入 IP 执行封禁（触发确认弹窗）
        /// </summary>
        [RelayCommand]
        private void ManualBan()
        {
            if (string.IsNullOrWhiteSpace(ManualBanIpInput))
            {
                ShowMessage(ResourceHelper.GetString("Str.Notify.InputValidIp"));
                return;
            }

            var ip = ManualBanIpInput.Trim();
            ManualBanIpInput = string.Empty;
            BanIp(ip);
        }

        /// <summary>
        /// 切换开机自启动
        /// </summary>
        [RelayCommand]
        private void ToggleAutoStart()
        {
            if (IsLaunchOnSysPowerOn)
            {
                var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
                IsLaunchOnSysPowerOn = TaskSchedulerHelper.AddLaunchTask(AppGlobal.AppName, exePath);
                ShowMessage(IsLaunchOnSysPowerOn ? ResourceHelper.GetString("Str.Notify.AutoStartSuccess") : ResourceHelper.GetString("Str.Notify.AutoStartFailed"));
            }
            else
            {
                TaskSchedulerHelper.Delete(AppGlobal.AppName);
                ShowMessage(ResourceHelper.GetString("Str.Notify.AutoStartRemoved"));
            }
            Lactor.TrayPopupViewModel.ReLoad();
        }

        /// <summary>
        /// 保存配置（如自动封禁阈值）
        /// </summary>
        [RelayCommand]
        private void SaveSettings()
        {
            SettingRepository.SetInt("AutoBanThreshold", AutoBanThreshold);
            SettingRepository.SetInt("AutoBanWindowMinutes", AutoBanWindowMinutes);
            ShowMessage(ResourceHelper.GetString("Str.Dialog.SaveSuccessMsg"));
        }

        /// <summary>
        /// 扫描 Windows 历史事件日志
        /// </summary>
        [RelayCommand]
        private async Task ScanHistoryAsync()
        {
            IsBusy = true;
            BusyMessage = ResourceHelper.GetString("Str.Notify.ScanningBusy");

            try
            {
                var events = await Task.Run(() => _watcher.ScanHistory());
                var records = events.Select(e => new RdpLoginRecord
                {
                    EventRecordId = e.RecordId,
                    Timestamp = e.Timestamp,
                    IpAddress = e.IpAddress,
                    Port = e.Port,
                    UserName = e.UserName,
                    DomainName = e.DomainName,
                    IsSuccess = e.IsSuccess,
                    LogonType = e.LogonType,
                    LogonTypeDescription = e.LogonTypeDescription,
                    FailureReason = e.FailureReason,
                    Status = e.Status,
                    SubStatus = e.SubStatus,
                    EventSource = e.EventSource
                }).ToList();

                var addedCount = await Task.Run(() => RdpRecordRepository.AddRecords(records));
                ShowMessage(ResourceHelper.GetString("Str.Notify.ScanSuccess", addedCount));
                RefreshAll();
            }
            catch (Exception ex)
            {
                ShowMessage(ResourceHelper.GetString("Str.Notify.ScanError", ex.Message));
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        [RelayCommand]
        private void ClearAllRecords()
        {
            var title = ResourceHelper.GetString("Str.Dialog.ConfirmClearHistoryTitle");
            var msg = ResourceHelper.GetString("Str.Dialog.ConfirmClearHistoryMsg");
            var result = MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                RdpRecordRepository.ClearAllRecords();
                ShowMessage(ResourceHelper.GetString("Str.Setting.ClearAllLogs"));
                RefreshAll();
            }
        }

        /// <summary>
        /// 刷新所有数据
        /// </summary>
        [RelayCommand]
        public void RefreshAll()
        {
            LoadAggregatedIps();
            LoadRealtimeRecords();
            LoadBannedList();
            UpdateStats();
            Lactor.TrayPopupViewModel.ReLoad();
        }

        #endregion

        #region 数据加载与实时事件处理

        public void LoadAllData()
        {
            // 加载配置
            AutoBanThreshold = SettingRepository.GetInt("AutoBanThreshold", 5);
            AutoBanWindowMinutes = SettingRepository.GetInt("AutoBanWindowMinutes", 10);
            IsLaunchOnSysPowerOn = TaskSchedulerHelper.Get(AppGlobal.AppName) != null;

            RefreshAll();
        }

        private void LoadAggregatedIps()
        {
            var summaries = RdpRecordRepository.GetAggregatedSummaries(SearchKeyword);
            AggregatedIps = new ObservableCollection<AggregatedIpSummary>(summaries);
            TotalUniqueIps = summaries.Count;
        }

        private void LoadRealtimeRecords()
        {
            var records = RdpRecordRepository.GetRecentRecords(500, SearchKeyword);
            RealtimeRecords = new ObservableCollection<RdpLoginRecord>(records);
        }

        private void LoadBannedList()
        {
            var banned = BannedIpRepository.GetAllActive();
            BannedIps = new ObservableCollection<BannedIp>(banned);
            TotalBannedCount = banned.Count;
        }

        private void UpdateStats()
        {
            TotalFailedCount = RdpRecordRepository.GetRecentRecords(5000).Count(r => !r.IsSuccess);
        }

        private void OnRdpEventReceived(RdpEventModel model)
        {
            // 收到实时远程登录事件
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                var record = new RdpLoginRecord
                {
                    EventRecordId = model.RecordId,
                    Timestamp = model.Timestamp,
                    IpAddress = model.IpAddress,
                    Port = model.Port,
                    UserName = model.UserName,
                    DomainName = model.DomainName,
                    IsSuccess = model.IsSuccess,
                    LogonType = model.LogonType,
                    LogonTypeDescription = model.LogonTypeDescription,
                    FailureReason = model.FailureReason,
                    Status = model.Status,
                    SubStatus = model.SubStatus,
                    EventSource = model.EventSource
                };

                var added = RdpRecordRepository.AddRecord(record);
                if (added)
                {
                    // 自动封禁策略检测
                    if (!model.IsSuccess && AutoBanThreshold > 0 && !BannedIpRepository.IsBanned(model.IpAddress))
                    {
                        var sinceTime = AutoBanWindowMinutes > 0 ? DateTime.Now.AddMinutes(-AutoBanWindowMinutes) : DateTime.MinValue;
                        var failCount = RdpRecordRepository.GetRecentFailureCountByIp(model.IpAddress, sinceTime);
                        if (failCount >= AutoBanThreshold)
                        {
                            var reason = AutoBanWindowMinutes > 0 ? ResourceHelper.GetString("Str.BanReason.AutoBanWindow", AutoBanWindowMinutes, failCount) : ResourceHelper.GetString("Str.BanReason.TotalFail", failCount);
                            BannedIpRepository.BanIp(model.IpAddress, reason);

                            var notifyMsg = AutoBanWindowMinutes > 0 ? ResourceHelper.GetString("Str.Notify.AutoBanWindow", model.IpAddress, AutoBanWindowMinutes, failCount) : ResourceHelper.GetString("Str.Notify.DefaultFail", model.IpAddress, failCount);
                            ShowMessage(notifyMsg);
                        }
                    }

                    // 刷新视图
                    RefreshAll();
                }
            }));
        }

        private void ShowMessage(string msg)
        {
            SnackbarMessageQueue.Enqueue(msg);
        }

        #endregion
    }
}
