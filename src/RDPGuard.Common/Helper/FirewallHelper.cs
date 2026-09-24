using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RDPGuard.Helper
{
    /// <summary>
    /// Windows 防火墙操作类
    /// </summary>
    public static class FirewallHelper
    {
        private const string _clsidFwPolicy2 = "{E2B3C97F-6AE1-41AC-817A-F6F92166D7DD}";
        private const string _clsidFwRule = "{2C5BC43E-3369-4C33-AB0C-BE9469677AF4}";
        private static readonly object _policyLock = new();

        #region 公共接口

        /// <summary>
        /// 封禁单个 IP
        /// </summary>
        public static bool BlockIp(string ip, string reason = "")
        {
            if (!TryNormalizeIp(ip, out var normalizedIp))
            {
                return false;
            }

            lock (_policyLock)
            {
                try
                {
                    var policyObj = CreatePolicy();
                    if (policyObj != null)
                    {
                        dynamic policy = policyObj;
                        dynamic? existingRule = null;
                        try
                        {
                            existingRule = policy.Rules.Item(AppGlobal.FirewallRuleName);
                        }
                        catch { }

                        if (existingRule != null)
                        {
                            try
                            {
                                var ips = ParseRemoteAddresses((string)(existingRule.RemoteAddresses ?? string.Empty));
                                if (!ips.Contains(normalizedIp))
                                {
                                    ips.Add(normalizedIp);
                                    existingRule.RemoteAddresses = string.Join(",", ips);
                                    existingRule.Enabled = true;
                                }
                                return true;
                            }
                            finally
                            {
                                ReleaseComObject(existingRule);
                            }
                        }
                        else
                        {
                            CreateBlockRule(policyObj, new List<string> { normalizedIp }, reason);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FirewallHelper.BlockIp COM 异常]: {ex.Message}");
                }

                return false;
            }
        }

        /// <summary>
        /// 解封单个 IP
        /// </summary>
        public static bool UnblockIp(string ip)
        {
            if (!TryNormalizeIp(ip, out var normalizedIp))
            {
                return false;
            }

            lock (_policyLock)
            {
                try
                {
                    var policyObj = CreatePolicy();
                    if (policyObj != null)
                    {
                        dynamic policy = policyObj;
                        dynamic? existingRule = null;
                        try
                        {
                            existingRule = policy.Rules.Item(AppGlobal.FirewallRuleName);
                        }
                        catch { }

                        if (existingRule != null)
                        {
                            try
                            {
                                var ips = ParseRemoteAddresses((string)(existingRule.RemoteAddresses ?? string.Empty));
                                if (ips.Remove(normalizedIp))
                                {
                                    if (ips.Count == 0)
                                    {
                                        policy.Rules.Remove(AppGlobal.FirewallRuleName);
                                    }
                                    else
                                    {
                                        existingRule.RemoteAddresses = string.Join(",", ips);
                                    }
                                }
                                return true;
                            }
                            finally
                            {
                                ReleaseComObject(existingRule);
                            }
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FirewallHelper.UnblockIp COM 异常]: {ex.Message}");
                }

                return false;
            }
        }

        /// <summary>
        /// 判断指定 IP 是否在防火墙阻止列表中
        /// </summary>
        public static bool IsIpBlocked(string ip)
        {
            if (!TryNormalizeIp(ip, out var normalizedIp))
            {
                return false;
            }

            lock (_policyLock)
            {
                try
                {
                    var policyObj = CreatePolicy();
                    if (policyObj != null)
                    {
                        dynamic policy = policyObj;
                        dynamic? existingRule = null;
                        try
                        {
                            existingRule = policy.Rules.Item(AppGlobal.FirewallRuleName);
                        }
                        catch { }

                        if (existingRule != null)
                        {
                            try
                            {
                                var ips = ParseRemoteAddresses((string)(existingRule.RemoteAddresses ?? string.Empty));
                                return ips.Contains(normalizedIp);
                            }
                            finally
                            {
                                ReleaseComObject(existingRule);
                            }
                        }
                    }
                }
                catch { }

                return false;
            }
        }

        /// <summary>
        /// 获取当前防火墙规则中所有被封禁的 IP 集合
        /// </summary>
        public static HashSet<string> GetAllBlockedIps()
        {
            lock (_policyLock)
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var policyObj = CreatePolicy();
                    if (policyObj != null)
                    {
                        dynamic policy = policyObj;
                        dynamic? existingRule = null;
                        try
                        {
                            existingRule = policy.Rules.Item(AppGlobal.FirewallRuleName);
                        }
                        catch { }

                        if (existingRule != null)
                        {
                            try
                            {
                                return ParseRemoteAddresses((string)(existingRule.RemoteAddresses ?? string.Empty));
                            }
                            finally
                            {
                                ReleaseComObject(existingRule);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FirewallHelper.GetAllBlockedIps 异常]: {ex.Message}");
                }
                return result;
            }
        }

        /// <summary>
        /// 全量同步指定 IP 列表到防火墙
        /// </summary>
        public static bool SyncAllBlockedIps(IEnumerable<string> bannedIps)
        {
            var validIps = new List<string>();
            foreach (var raw in bannedIps)
            {
                if (TryNormalizeIp(raw, out var norm))
                {
                    validIps.Add(norm);
                }
            }

            validIps = validIps.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            lock (_policyLock)
            {
                try
                {
                    var policyObj = CreatePolicy();
                    if (policyObj == null) return false;

                    dynamic policy = policyObj;
                    dynamic? existingRule = null;
                    try
                    {
                        existingRule = policy.Rules.Item(AppGlobal.FirewallRuleName);
                    }
                    catch { }

                    if (validIps.Count == 0)
                    {
                        // 若黑名单为空，清理规则
                        if (existingRule != null)
                        {
                            ReleaseComObject(existingRule);
                            policy.Rules.Remove(AppGlobal.FirewallRuleName);
                        }
                        return true;
                    }

                    if (existingRule != null)
                    {
                        existingRule.RemoteAddresses = string.Join(",", validIps);
                        existingRule.Enabled = true;
                        ReleaseComObject(existingRule);
                    }
                    else
                    {
                        CreateBlockRule(policyObj, validIps, "全量同步封禁黑名单");
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FirewallHelper.SyncAllBlockedIps 异常]: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region COM 内部操作

        private static object? CreatePolicy()
        {
            var type = Type.GetTypeFromCLSID(new Guid(_clsidFwPolicy2));
            return type != null ? Activator.CreateInstance(type) : null;
        }

        private static object? CreateRule()
        {
            var type = Type.GetTypeFromCLSID(new Guid(_clsidFwRule));
            return type != null ? Activator.CreateInstance(type) : null;
        }

        private static void ReleaseComObject(object? obj)
        {
            if (obj != null && Marshal.IsComObject(obj))
            {
                try
                {
                    Marshal.FinalReleaseComObject(obj);
                }
                catch { }
            }
        }

        private static HashSet<string> ParseRemoteAddresses(string remote)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(remote) && remote != "*")
            {
                foreach (var part in remote.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var clean = part.Trim();
                    if (string.IsNullOrEmpty(clean)) continue;

                    // Windows 防火墙导出的 IP 地址格式带掩码，例如：
                    // 192.168.110.1/255.255.255.255、192.168.110.1/32、fe80::1/128 等
                    var slashIndex = clean.IndexOf('/');
                    if (slashIndex > 0)
                    {
                        clean = clean[..slashIndex].Trim();
                    }

                    if (TryNormalizeIp(clean, out var normalized))
                    {
                        set.Add(normalized);
                    }
                }
            }
            return set;
        }

        private static void CreateBlockRule(object policyObj, List<string> ips, string reason)
        {
            dynamic policy = policyObj;
            dynamic? rule = CreateRule();
            if (rule == null) return;

            try
            {
                try { policy.Rules.Remove(AppGlobal.FirewallRuleName); } catch { }

                rule.Name = AppGlobal.FirewallRuleName;
                rule.Description = $"RDPGuard 拦截恶意远程登录 [组: {AppGlobal.FirewallRuleGroupName}] [原因: {reason}]";
                rule.Grouping = AppGlobal.FirewallRuleGroupName;
                rule.Action = 0;
                rule.Direction = 1;
                rule.Enabled = true;
                rule.InterfaceTypes = "All";
                rule.RemoteAddresses = string.Join(",", ips);
                rule.Profiles = 0x7FFFFFFF;
                rule.EdgeTraversal = false;

                policy.Rules.Add(rule);
            }
            finally
            {
                ReleaseComObject(rule);
            }
        }

        #endregion

        #region 工具方法

        private static bool TryNormalizeIp(string? ip, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(ip)) return false;

            ip = ip.Trim();

            // 若包含掩码后缀 (如 /255.255.255.255 或 /32)，剥离掩码提取真实 IP
            var slashIndex = ip.IndexOf('/');
            if (slashIndex > 0)
            {
                ip = ip[..slashIndex].Trim();
            }

            if (ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
            {
                ip = ip[7..];
            }

            if (ip == "127.0.0.1" || ip == "::1" || ip == "0.0.0.0" || ip == "-")
            {
                return false;
            }

            normalized = ip;
            return true;
        }

        #endregion
    }
}
