using System.Diagnostics;

namespace RDPGuard.Helper
{
    /// <summary>
    /// Windows 防火墙操作帮助类
    /// </summary>
    public static class FirewallHelper
    {
        private const string _clsidFwPolicy2 = "{E2B3C97F-6AE1-41AC-817A-F6F92166D7DD}";
        private const string _clsidFwRule = "{2C5BC43E-3369-4C33-AB0C-BE9469677AF4}";
        private static readonly object _syncLock = new();

        /// <summary>
        /// 生成规则名称
        /// </summary>
        public static string GetRuleName(string ip)
        {
            var sanitizedIp = ip.Replace(":", "_").Replace("/", "_").Trim();
            return $"{AppGlobal.FirewallRulePrefix}{sanitizedIp}";
        }

        /// <summary>
        /// 封禁 IP
        /// </summary>
        public static bool BlockIp(string ip, string reason = "")
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return false;
            }

            ip = ip.Trim();
            var ruleName = GetRuleName(ip);

            lock (_syncLock)
            {
                try
                {
                    // 1. 尝试使用 COM 接口（极速无闪烁）
                    var typePolicy = Type.GetTypeFromCLSID(new Guid(_clsidFwPolicy2));
                    var typeRule = Type.GetTypeFromCLSID(new Guid(_clsidFwRule));

                    if (typePolicy != null && typeRule != null)
                    {
                        dynamic? policy = Activator.CreateInstance(typePolicy);
                        dynamic? rule = Activator.CreateInstance(typeRule);

                        if (policy != null && rule != null)
                        {
                            try
                            {
                                // 先删除旧的相同规则（若存在）
                                policy?.Rules.Remove(ruleName);
                            }
                            catch { }

                            rule?.Name = ruleName;
                            rule?.Description = $"RDPGuard 拦截恶意远程登录: {reason} [时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}]";
                            rule?.Action = 0; // NET_FW_ACTION_BLOCK = 0
                            rule?.Direction = 1; // NET_FW_RULE_DIR_IN = 1
                            rule?.Enabled = true;
                            rule?.InterfaceTypes = "All";
                            rule?.RemoteAddresses = ip;
                            rule?.Profiles = 0x7FFFFFFF; // NET_FW_PROFILE2_ALL

                            policy?.Rules.Add(rule);
                            return true;
                        }
                    }
                }
                catch
                {
                }

                return RunNetsh($"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=block remoteip={ip} description=\"RDPGuard Auto Block\"");
            }
        }

        /// <summary>
        /// 解封 IP
        /// </summary>
        public static bool UnblockIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return false;
            }

            ip = ip.Trim();
            var ruleName = GetRuleName(ip);

            lock (_syncLock)
            {
                try
                {
                    var typePolicy = Type.GetTypeFromCLSID(new Guid(_clsidFwPolicy2));
                    if (typePolicy != null)
                    {
                        dynamic? policy = Activator.CreateInstance(typePolicy);
                        if (policy != null)
                        {
                            policy.Rules.Remove(ruleName);
                            return true;
                        }
                    }
                }
                catch
                {
                }

                return RunNetsh($"advfirewall firewall delete rule name=\"{ruleName}\"");
            }
        }

        /// <summary>
        /// 判断 IP 是否已被封禁
        /// </summary>
        public static bool IsIpBlocked(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return false;
            }

            ip = ip.Trim();
            var ruleName = GetRuleName(ip);

            lock (_syncLock)
            {
                try
                {
                    var typePolicy = Type.GetTypeFromCLSID(new Guid(_clsidFwPolicy2));
                    if (typePolicy != null)
                    {
                        dynamic? policy = Activator.CreateInstance(typePolicy);
                        if (policy != null)
                        {
                            dynamic? existing = policy.Rules.Item(ruleName);
                            if (existing != null && (bool)existing?.Enabled)
                            {
                                return true;
                            }
                        }
                    }
                }
                catch
                {
                }

                // 降级使用 netsh 探测
                try
                {
                    var psi = new ProcessStartInfo("netsh", $"advfirewall firewall show rule name=\"{ruleName}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        var output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit();
                        if (p.ExitCode == 0 && output.Contains(ruleName))
                        {
                            return true;
                        }
                    }
                }
                catch { }

                return false;
            }
        }

        private static bool RunNetsh(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
