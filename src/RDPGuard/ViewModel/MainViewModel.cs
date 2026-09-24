using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
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
        private readonly RdpEventWatcher _watcher = new();

        public MainViewModel()
        {
            SnackbarMessageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));

            // 初始化监听事件
            _watcher.OnRdpEventReceived += OnRdpEventReceived;
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
            if (string.IsNullOrWhiteSpace(ip)) return;
            ip = ip.Trim();

            var success = BannedIpRepository.BanIp(ip, "管理员手动封禁");
            if (success)
            {
                ShowMessage($"已成功封禁 IP: {ip}，防火墙入站规则已生效！");
            }
            else
            {
                ShowMessage($"封禁 IP {ip} 规则添加可能受限，已记录在黑名单中。");
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
            ShowMessage($"已解除对 IP: {ip} 的封禁");
            RefreshAll();
        }

        /// <summary>
        /// 手动输入 IP 执行封禁
        /// </summary>
        [RelayCommand]
        private void ManualBan()
        {
            if (string.IsNullOrWhiteSpace(ManualBanIpInput))
            {
                ShowMessage("请输入有效的 IP 地址！");
                return;
            }

            BanIp(ManualBanIpInput.Trim());
            ManualBanIpInput = string.Empty;
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
                ShowMessage(IsLaunchOnSysPowerOn ? "已成功设置计划任务开机自启（管理员权限）" : "设置自启动失败");
            }
            else
            {
                TaskSchedulerHelper.Delete(AppGlobal.AppName);
                ShowMessage("已移除计划任务自启动");
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
            ShowMessage("设置保存成功！");
        }

        /// <summary>
        /// 扫描 Windows 历史事件日志
        /// </summary>
        [RelayCommand]
        private async Task ScanHistoryAsync()
        {
            IsBusy = true;
            BusyMessage = "正在扫描 Windows 事件查看器中的远程登录历史日志...";

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
                ShowMessage($"扫描完成！已同步导入 {addedCount} 条远程登录记录。");
                RefreshAll();
            }
            catch (Exception ex)
            {
                ShowMessage($"扫描历史日志异常: {ex.Message}");
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
            var result = MessageBox.Show("确定要清空本地所有 RDP 登录审计记录吗？（注意：不会清除 Windows 系统自带日志）", "清空确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                RdpRecordRepository.ClearAllRecords();
                ShowMessage("已清空本地登录流水记录！");
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
            IsLaunchOnSysPowerOn = TaskSchedulerHelper.Get(AppGlobal.AppName) != null;

            RefreshAll();

            // 同步防火墙封禁规则（聚合规则与清理遗留单 IP 规则）
            Task.Run(() => BannedIpRepository.SyncFirewallRules());

            // 启动实时监听服务
            _watcher.Start();
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
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
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
                    if (!model.IsSuccess && AutoBanThreshold > 0)
                    {
                        var ipRecords = RdpRecordRepository.GetRecordsByIp(model.IpAddress);
                        var failCount = ipRecords.Count(r => !r.IsSuccess);
                        if (failCount >= AutoBanThreshold && !BannedIpRepository.IsBanned(model.IpAddress))
                        {
                            BannedIpRepository.BanIp(model.IpAddress, $"登录失败达 {failCount} 次，触发自动封禁");
                            ShowMessage($"警告！IP: {model.IpAddress} 登录失败达 {failCount} 次，已自动加入防火墙封禁！");
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
