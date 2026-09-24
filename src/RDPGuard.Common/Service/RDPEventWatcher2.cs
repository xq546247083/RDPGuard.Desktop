using RDPGuard.Model;
using System.Diagnostics.Eventing.Reader;
using System.Xml.Linq;
using System.Xml.XPath;

namespace RDPGuard.Service
{
    /// <summary>
    /// RDP 远程登录事件监听与检索服务
    /// </summary>
    public class RDPEventWatcher2 : IDisposable
    {
        private EventLogWatcher? _securityWatcher;
        private EventLogWatcher? _tsWatcher;
        private bool _disposed;

        public event Action<RdpEventModel>? OnRdpEventReceived;

        /// <summary>
        /// 启动实时监听
        /// </summary>
        public void Start()
        {
            Stop();

            // 1. 监听安全日志 (4624 成功, 4625 失败)
            try
            {
                // 筛选 LogonType=10 (远程交互) 或 LogonType=3 (网络) 且有有效 IP
                const string securityQuery = "*[System[(EventID=4624 or EventID=4625)]]";
                var eventQuery = new EventLogQuery("Security", PathType.LogName, securityQuery)
                {
                    TolerateQueryErrors = true
                };

                _securityWatcher = new EventLogWatcher(eventQuery);
                _securityWatcher.EventRecordWritten += SecurityWatcher_EventRecordWritten;
                _securityWatcher.Enabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动 Security 日志监听失败: {ex.Message}");
            }

            // 2. 监听 TerminalServices 远程连接管理器日志 (1149 NLA 身份验证成功)
            try
            {
                const string tsLogName = "Microsoft-Windows-TerminalServices-RemoteConnectionManager/Operational";
                const string tsQuery = "*[System[(EventID=1149)]]";
                var eventQuery = new EventLogQuery(tsLogName, PathType.LogName, tsQuery)
                {
                    TolerateQueryErrors = true
                };

                _tsWatcher = new EventLogWatcher(eventQuery);
                _tsWatcher.EventRecordWritten += TsWatcher_EventRecordWritten;
                _tsWatcher.Enabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动 TS 日志监听失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        public void Stop()
        {
            if (_securityWatcher != null)
            {
                try
                {
                    _securityWatcher.Enabled = false;
                    _securityWatcher.Dispose();
                }
                catch { }
                _securityWatcher = null;
            }

            if (_tsWatcher != null)
            {
                try
                {
                    _tsWatcher.Enabled = false;
                    _tsWatcher.Dispose();
                }
                catch { }
                _tsWatcher = null;
            }
        }

        /// <summary>
        /// 扫描历史事件
        /// </summary>
        /// <param name="days">过去多少天</param>
        public List<RdpEventModel> ScanHistory(int days = 30)
        {
            var results = new List<RdpEventModel>();
            var startTime = DateTime.UtcNow.AddDays(-365);

            try
            {
                long ms = (long)days * 86400000L;
                var queryStr = days > 0
                    ? $"*[System[(EventID=4624 or EventID=4625) and TimeCreated[timediff(@SystemTime) <= {ms}]]]"
                    : "*[System[(EventID=4624 or EventID=4625)]]";

                var query = new EventLogQuery("Security", PathType.LogName, queryStr)
                {
                    TolerateQueryErrors = true,
                    ReverseDirection = true
                };

                using var reader = new EventLogReader(query);
                for (EventRecord? record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
                {
                    using (record)
                    {
                        var model = ParseSecurityRecord(record);
                        if (model != null)
                        {
                            results.Add(model);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取历史 Security 日志异常: {ex.Message}");
            }

            // 2. 读取 TS 日志
            try
            {
                long ms = (long)days * 86400000L;
                const string tsLogName = "Microsoft-Windows-TerminalServices-RemoteConnectionManager/Operational";
                var queryStr = days > 0
                    ? $"*[System[EventID=1149 and TimeCreated[timediff(@SystemTime) <= {ms}]]]"
                    : "*[System[EventID=1149]]";

                var query = new EventLogQuery(tsLogName, PathType.LogName, queryStr)
                {
                    TolerateQueryErrors = true,
                    ReverseDirection = true
                };

                using var reader = new EventLogReader(query);
                for (EventRecord? record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
                {
                    using (record)
                    {
                        var model = ParseTsRecord(record);
                        if (model != null)
                        {
                            results.Add(model);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取历史 TS 日志异常: {ex.Message}");
            }

            return results;
        }

        private void SecurityWatcher_EventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
        {
            if (e.EventRecord == null) return;
            try
            {
                var model = ParseSecurityRecord(e.EventRecord);
                if (model != null)
                {
                    OnRdpEventReceived?.Invoke(model);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析实时 Security 日志异常: {ex.Message}");
            }
        }

        private void TsWatcher_EventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
        {
            if (e.EventRecord == null) return;
            try
            {
                var model = ParseTsRecord(e.EventRecord);
                if (model != null)
                {
                    OnRdpEventReceived?.Invoke(model);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析实时 TS 日志异常: {ex.Message}");
            }
        }

        private static RdpEventModel? ParseSecurityRecord(EventRecord record)
        {
            var eventId = record.Id;
            if (eventId != 4624 && eventId != 4625)
            {
                return null;
            }

            var xmlString = record.ToXml();
            if (string.IsNullOrWhiteSpace(xmlString)) return null;

            var xdoc = XDocument.Parse(xmlString);
            var ns = xdoc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            string GetEventData(string name)
            {
                var element = xdoc.XPathSelectElement($"//*[local-name()='Data'][@Name='{name}']");
                return element?.Value?.Trim() ?? string.Empty;
            }

            var ip = GetEventData("IpAddress");
            if (string.IsNullOrWhiteSpace(ip) || ip == "-" || ip == "127.0.0.1" || ip == "::1" || ip == "0.0.0.0")
            {
                // 不是外部远程连接，跳过
                return null;
            }

            // 规范化 IP 地址格式（剔除 IPv6 映射前缀 ::ffff: 等）
            if (ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
            {
                ip = ip.Substring(7);
            }

            var isSuccess = (eventId == 4624);
            var logonTypeStr = GetEventData("LogonType");
            int.TryParse(logonTypeStr, out var logonType);

            // 成功登录必须是远程交互(10)或网络登录(3)；失败登录只要具有有效外部IP即视为远程尝试
            if (isSuccess && logonType != 10 && logonType != 3)
            {
                return null;
            }

            var userName = GetEventData("TargetUserName");
            if (string.IsNullOrWhiteSpace(userName) || userName.EndsWith("$"))
            {
                // 过滤计算机机器账户
                return null;
            }

            var domainName = GetEventData("TargetDomainName");
            var portStr = GetEventData("IpPort");
            int.TryParse(portStr, out var port);

            var status = GetEventData("Status");
            var subStatus = GetEventData("SubStatus");
            var failureReason = isSuccess ? "登录成功" : GetFailureReason(subStatus, status);

            var time = record.TimeCreated ?? DateTime.Now;

            return new RdpEventModel
            {
                RecordId = record.RecordId ?? 0,
                Timestamp = time.ToLocalTime(),
                IpAddress = ip,
                Port = port,
                UserName = userName,
                DomainName = domainName,
                IsSuccess = isSuccess,
                LogonType = logonType,
                LogonTypeDescription = logonType == 10 ? "远程交互 (RDP)" : "网络登录",
                Status = status,
                SubStatus = subStatus,
                FailureReason = failureReason,
                EventSource = isSuccess ? "Security 4624 (成功)" : "Security 4625 (失败)"
            };
        }

        private static RdpEventModel? ParseTsRecord(EventRecord record)
        {
            if (record.Id != 1149) return null;

            var xmlString = record.ToXml();
            if (string.IsNullOrWhiteSpace(xmlString)) return null;

            var xdoc = XDocument.Parse(xmlString);
            string GetParam(string name)
            {
                var element = xdoc.XPathSelectElement($"//*[local-name()='Data'][@Name='{name}']");
                return element?.Value?.Trim() ?? string.Empty;
            }

            var userName = GetParam("Param1");
            var domain = GetParam("Param2");
            var ip = GetParam("Param3");

            if (string.IsNullOrWhiteSpace(ip) || ip == "-" || ip == "127.0.0.1" || ip == "::1")
            {
                return null;
            }

            if (ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
            {
                ip = ip.Substring(7);
            }

            var time = record.TimeCreated ?? DateTime.Now;

            return new RdpEventModel
            {
                RecordId = record.RecordId ?? 0,
                Timestamp = time.ToLocalTime(),
                IpAddress = ip,
                Port = 3389,
                UserName = userName,
                DomainName = domain,
                IsSuccess = true,
                LogonType = 10,
                LogonTypeDescription = "RDP 身份验证",
                Status = "0x0",
                SubStatus = "0x0",
                FailureReason = "身份验证成功",
                EventSource = "TerminalServices 1149"
            };
        }

        private static string GetFailureReason(string subStatus, string status)
        {
            var code = !string.IsNullOrWhiteSpace(subStatus) && subStatus != "0x0" ? subStatus.ToLower() : status.ToLower();
            return code switch
            {
                "0xc000006a" => "密码错误",
                "0xc0000064" => "用户名不存在",
                "0xc000006e" => "账户名限制",
                "0xc000006f" => "登录时间限制",
                "0xc0000070" => "工作站限制",
                "0xc0000071" => "密码已过期",
                "0xc0000072" => "账户已被禁用",
                "0xc0000193" => "账户已过期",
                "0xc0000224" => "下次登录必须更改密码",
                "0xc0000234" => "账户已被锁定",
                _ => string.IsNullOrEmpty(code) ? "登录失败" : $"失败 ({code})"
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
