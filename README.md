# RDPGuard.Desktop

一款基于 **.NET 10 + WPF + SQLite** 开发的 Windows 远程桌面（RDP）登录实时审计与智能防火墙防护工具。

---

## 🌟 核心功能

1. **实时记录所有远程登录**
   - 实时监听 Windows Security 安全事件日志（EventID `4624` 成功，`4625` 失败）以及远程连接管理器日志（EventID `1149` 认证成功）。
   - 无论远程登录成功还是失败，均完整提取：发生时间、远程 IP 地址、端口、登录用户名、所属域/计算机名、登录模式（远程交互 RDP / 网络）、NT 状态码与详细失败原因（如密码错误、用户名不存在、账户锁定等）。
   - 提供**历史日志扫描同步**功能，启动即可一键拉取系统近 30 天内的远程登录历史。

2. **相同 IP 聚合显示与统计分析**
   - 自动对来源相同的 IP 进行分组聚合统计。
   - 聚合展示每个 IP 的**总尝试次数**、**成功登录次数**、**失败次数**、**首次活跃时间**、**最后活跃时间**、**尝试过的用户名集合**（如 `administrator, root, test`）、最后状态与封禁状态。

3. **智能与手动防火墙封禁**
   - **手动封禁/解封**：在聚合列表、实时流水列表或黑名单管理页中，一键对恶意 IP 执行防火墙入站阻断规则（毫秒级生效），或一键解除封禁。
   - **自动暴力破解防护策略**：可配置当同一 IP 失败尝试达到阈值时（默认 5 次），自动加入 Windows 原生高级防火墙入站黑名单阻断。

4. **计划任务开机自启**
   - 基于 `Microsoft.Win32.TaskScheduler`，使用 Windows 任务计划程序注册自启动任务。
   - 以最高管理员权限（`HighestAvailable`）随用户登录时静默自启，避免每次开机弹出 UAC 提权确认框。

5. **系统托盘交互**
   - 基于 `Hardcodet.NotifyIcon.Wpf`，最小化自动驻留系统托盘，后台持续防护。
   - 悬浮控制面板（TrayPopup）：快捷查看已封禁 IP 数、今日尝试数、最近 5 条登录事件流，并支持开机自启开关、主窗口呼出与退出。

---

## 🏗️ 项目架构

```text
RDPGuard.Desktop/
│── RDPGuard.sln                     # 解决方案文件
│
├── src/
│   ├── RDPGuard.Common/             # 公共通用层
│   │   ├── AppGlobal.cs             # 全局信息配置
│   │   ├── Helper/
│   │   │   ├── TaskSchedulerHelper.cs  # 计划任务帮助类（开机管理员自启）
│   │   │   └── FirewallHelper.cs       # Windows 防火墙封禁/解封（COM + netsh 双保障）
│   │   ├── Model/
│   │   │   └── RdpEventModel.cs        # RDP 事件领域模型
│   │   └── Service/
│   │       └── RdpEventWatcher.cs      # Windows 事件日志实时订阅与历史检索服务
│   │
│   ├── RDPGuard.SQLite/             # 数据持久化层
│   │   ├── DbInitializer.cs         # 数据库自动初始化器
│   │   ├── RDPGuardDbContext.cs     # EF Core SQLite 上下文
│   │   ├── Entities/
│   │   │   ├── RdpLoginRecord.cs       # 登录流水记录实体
│   │   │   ├── BannedIp.cs             # 封禁 IP 黑名单实体
│   │   │   ├── Setting.cs              # 配置实体
│   │   │   └── AggregatedIpSummary.cs  # IP 聚合统计实体
│   │   └── Repositories/
│   │       ├── RdpRecordRepository.cs  # 登录流水与聚合查询仓储
│   │       ├── BannedIpRepository.cs   # 黑名单仓储（同步防火墙）
│   │       └── SettingRepository.cs    # 系统配置仓储
│   │
│   └── RDPGuard/                    # WPF 桌面主程序
│       ├── App.xaml / App.xaml.cs   # 应用入口（单例防重、异常捕获、托盘启动）
│       ├── app.manifest             # UAC requireAdministrator 清单
│       ├── Common/                  # 行为与命令扩展 (InvokeCommandActionEx 等)
│       ├── Convert/                 # WPF 转换器 (EnumToBoolean, ResultToBrush 等)
│       ├── Enums/                   # 选项卡等枚举定义
│       ├── Manager/
│       │   ├── Lactor.cs            # 窗口与 ViewModel 统一调度单例
│       │   └── NotifyIconManager.cs # 系统托盘管理器
│       ├── View/
│       │   ├── MainWindow.xaml      # 主界面（聚合概览、实时流水、黑名单、设置）
│       │   └── TrayPopupControl.xaml# 托盘右键悬浮窗口
│       ├── ViewModel/
│       │   ├── MainViewModel.cs     # 主界面 ViewModel (CommunityToolkit.Mvvm)
│       │   └── TrayPopupViewModel.cs# 托盘 ViewModel
│       └── Resources/
│           └── app.ico              # 应用与托盘图标
```

---

## 🛠️ 技术栈

- **框架**：.NET 10.0 (`net10.0-windows`)
- **界面**：WPF + MaterialDesignThemes 5.2.1
- **MVVM**：CommunityToolkit.Mvvm 8.4.0
- **数据库**：SQLite + Microsoft.EntityFrameworkCore.Sqlite
- **计划任务**：TaskScheduler 2.12.2
- **系统托盘**：Hardcodet.NotifyIcon.Wpf 2.0.1
