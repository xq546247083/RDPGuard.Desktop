using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace RDPGuard.Helper
{
    /// <summary>
    /// Windows 防火墙操作类（聚合规则模式，对齐 IPBan 架构）
    /// 每条规则最多容纳 1000 个 IP（RDPGuard_Block_0, RDPGuard_Block_1000 等），统一分组为 RDPGuard。
    /// </summary>
    public static class FirewallHelper
    {
        /// <summary>
        /// 每条防火墙规则允许容纳的最大 IP 地址数量（对齐 IPBan 的 MaxIpAddressesPerRule）
        /// </summary>
        public const int MaxIpAddressesPerRule = 1000;

        private const string ClsidFwPolicy2 = "{E2B3C97F-6AE1-41AC-817A-F6F92166D7DD}";
        private const string ClsidFwRule = "{2C5BC43E-3369-4C33-AB0C-BE9469677AF4}";
        private static readonly object PolicyLock = new();

        private static string RulePrefix => AppGlobal.FirewallRulePrefix; // "RDPGuard_Block_"
        private static string RuleGroup => AppGlobal.AppName;             // "RDPGuard"

        #region 公共接口

        /// <summary>
        /// 封禁单个 IP（自动归入聚合防火墙规则中）
        /// </summary>
        public static bool BlockIp(string ip, string reason = "")
        {
            if (!TryNormalizeIp(ip, out var normalizedIp))
            {
                return false;
            }

            lock (PolicyLock)
            {
                try
                {
                    var policyObj = CreatePolicy();
                    if (policyObj != null)
                    {
                        var ruleMap = LoadExistingRules(policyObj);

                        // 1. 检查是否已经在某个规则中
                        foreach (var kvp in ruleMap)
                        {
                            if (kvp.Value.Contains(normalizedIp))
                            {
                                return true; // 已经存在，无需重复添加
                            }
                        }

                        // 2. 寻找还有空位（< MaxIpAddressesPerRule）的现有规则
                        foreach (var kvp in ruleMap.OrderBy(k => k.Key))
                        {
                            if (kvp.Value.Count < MaxIpAddressesPerRule)
                            {
                                kvp.Value.Add(normalizedIp);
                                var ruleName = $"{RulePrefix}{kvp.Key}";
                                UpdateRuleRemoteAddresses(policyObj, ruleName, kvp.Value);
                                return true;
                            }
                        }

                        // 3. 现有规则全满或暂无规则，新建下一槽位规则
                        var nextIndex = ruleMap.Count == 0 ? 0 : ruleMap.Keys.Max() + MaxIpAddressesPerRule;
                        var newRuleName = $"{RulePrefix}{nextIndex}";
                        CreateBlockRule(policyObj, newRuleName, new List<string> { normalizedIp }, reason);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FirewallHelper.BlockIp COM 异常]: {ex.Message}");
                }

                // COM 失败时降级走 netsh
                return NetshBlockFallback(normalizedIp);
            }
        }

        /// <summary>
        /// 解封单个 IP（从所属的聚合规则中剔除）
        /// </summary>
        public static bool UnblockIp(string ip)
        {
            if (!TryNormalizeIp(ip, out var normalizedIp))
            {
                return false;
            }

            lock (PolicyLock)
            {
                try
                {
                    var policyObj = CreatePolicy();
                    if (policyObj != null)
                    {
                        var ruleMap = LoadExistingRules(policyObj);

                        foreach (var kvp in ruleMap)
                        {
                            if (kvp.Value.Remove(normalizedIp))
                            {
                                var ruleName = $"{RulePrefix}{kvp.Key}";
                                if (kvp.Value.Count == 0)
                                {
                                    // 规则已无任何 IP，安全移除该条规则
                                    DeleteRule(policyObj, ruleName);
                                }
                                else
                                {
                                    // 更新剩余 IP 列表
                                    UpdateRuleRemoteAddresses(policyObj, ruleName, kvp.Value);
                                }
                                return true;
                            }
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FirewallHelper.UnblockIp COM 异常]: {ex.Message}");
                }

                return NetshUnblockFallback(normalizedIp);
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

            lock (PolicyLock)
            {
                try
                {
                    var policyObj = CreatePolicy();
                    if (policyObj != null)
                    {
                        var ruleMap = LoadExistingRules(policyObj);
                        return ruleMap.Values.Any(ips => ips.Contains(normalizedIp));
                    }
                }
                catch { }

                return false;
            }
        }

        /// <summary>
        /// 获取当前防火墙所有被封禁的 IP 集合
        /// </summary>
        public static HashSet<string> GetAllBlockedIps()
        {
            lock (PolicyLock)
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var policyObj = CreatePolicy();
                    if (policyObj != null)
                    {
                        var ruleMap = LoadExistingRules(policyObj);
                        foreach (var set in ruleMap.Values)
                        {
                            foreach (var ip in set)
                            {
                                result.Add(ip);
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
        /// 全量同步指定 IP 列表到防火墙（对齐 IPBan 批量聚合机制）
        /// 1. 按 1000 个一组划分规则槽位（RDPGuard_Block_0, RDPGuard_Block_1000...）
        /// 2. 自动删除多余或旧单 IP 遗留规则
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

            lock (PolicyLock)
            {
                try
                {
                    var policyObj = CreatePolicy();
                    if (policyObj == null) return false;

                    dynamic policy = policyObj;

                    // 1. 清理旧版可能遗留的非标准单条规则（如 RDPGuard_Blocked_* 或未聚合规则）
                    CleanLegacyRulesInternal(policyObj);

                    // 2. 按 1000 IP 批量分块
                    var chunkIndex = 0;
                    for (int i = 0; i < validIps.Count; i += MaxIpAddressesPerRule)
                    {
                        var chunk = validIps.Skip(i).Take(MaxIpAddressesPerRule).ToList();
                        var ruleName = $"{RulePrefix}{i}";

                        dynamic? existingRule = null;
                        try
                        {
                            existingRule = policy.Rules.Item(ruleName);
                        }
                        catch { }

                        if (existingRule != null)
                        {
                            existingRule.RemoteAddresses = string.Join(",", chunk);
                            existingRule.Enabled = true;
                            ReleaseComObject(existingRule);
                        }
                        else
                        {
                            CreateBlockRule(policyObj, ruleName, chunk, "全量同步封禁黑名单");
                        }

                        chunkIndex = i + MaxIpAddressesPerRule;
                    }

                    // 3. 删除多余的槽位规则（例如从 3000 降到 1000 时，清理 Block_2000, Block_3000）
                    DeleteRulesBeyondIndex(policyObj, validIps.Count == 0 ? 0 : chunkIndex);

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FirewallHelper.SyncAllBlockedIps 异常]: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 清理旧版残余的单 IP 规则
        /// </summary>
        public static void CleanLegacyRules()
        {
            lock (PolicyLock)
            {
                try
                {
                    var policyObj = CreatePolicy();
                    if (policyObj != null)
                    {
                        CleanLegacyRulesInternal(policyObj);
                    }
                }
                catch { }
            }
        }

        #endregion

        #region COM 内部操作

        private static object? CreatePolicy()
        {
            var type = Type.GetTypeFromCLSID(new Guid(ClsidFwPolicy2));
            return type != null ? Activator.CreateInstance(type) : null;
        }

        private static object? CreateRule()
        {
            var type = Type.GetTypeFromCLSID(new Guid(ClsidFwRule));
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

        /// <summary>
        /// 加载所有符合 RDPGuard_Block_{index} 规范的现有规则与对应 IP 集合
        /// </summary>
        private static Dictionary<int, HashSet<string>> LoadExistingRules(object policyObj)
        {
            var dict = new Dictionary<int, HashSet<string>>();
            var prefixRegex = new Regex($@"^{Regex.Escape(RulePrefix)}(?<num>\d+)$", RegexOptions.IgnoreCase);
            dynamic policy = policyObj;

            foreach (dynamic rule in policy.Rules)
            {
                try
                {
                    string name = (string)rule.Name;
                    var match = prefixRegex.Match(name);
                    if (match.Success && int.TryParse(match.Groups["num"].Value, CultureInfo.InvariantCulture, out var index))
                    {
                        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        string remote = (string)(rule.RemoteAddresses ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(remote) && remote != "*")
                        {
                            foreach (var part in remote.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            {
                                var clean = part.Trim();
                                if (!string.IsNullOrEmpty(clean))
                                {
                                    set.Add(clean);
                                }
                            }
                        }
                        dict[index] = set;
                    }
                }
                finally
                {
                    ReleaseComObject(rule);
                }
            }

            return dict;
        }

        private static void CreateBlockRule(object policyObj, string ruleName, List<string> ips, string reason)
        {
            dynamic policy = policyObj;
            dynamic? rule = CreateRule();
            if (rule == null) return;

            try
            {
                // 先尝试删除可能已有的同名规则
                try { policy.Rules.Remove(ruleName); } catch { }

                rule.Name = ruleName;
                rule.Description = $"RDPGuard 拦截恶意远程登录 [组: {RuleGroup}] [原因: {reason}]";
                rule.Grouping = RuleGroup;
                rule.Action = 0; // NET_FW_ACTION_BLOCK
                rule.Direction = 1; // NET_FW_RULE_DIR_IN
                rule.Enabled = true;
                rule.InterfaceTypes = "All";
                rule.RemoteAddresses = string.Join(",", ips);
                rule.Profiles = 0x7FFFFFFF; // NET_FW_PROFILE2_ALL
                rule.EdgeTraversal = false;

                policy.Rules.Add(rule);
            }
            finally
            {
                ReleaseComObject(rule);
            }
        }

        private static void UpdateRuleRemoteAddresses(object policyObj, string ruleName, IEnumerable<string> ips)
        {
            dynamic policy = policyObj;
            dynamic? rule = null;
            try
            {
                rule = policy.Rules.Item(ruleName);
                if (rule != null)
                {
                    rule.RemoteAddresses = string.Join(",", ips);
                    rule.Enabled = true;
                }
            }
            catch { }
            finally
            {
                ReleaseComObject(rule);
            }
        }

        private static void DeleteRule(object policyObj, string ruleName)
        {
            dynamic policy = policyObj;
            try
            {
                policy.Rules.Remove(ruleName);
            }
            catch { }
        }

        private static void DeleteRulesBeyondIndex(object policyObj, int startIndex)
        {
            dynamic policy = policyObj;
            var prefixRegex = new Regex($@"^{Regex.Escape(RulePrefix)}(?<num>\d+)$", RegexOptions.IgnoreCase);
            var toDelete = new List<string>();

            foreach (dynamic rule in policy.Rules)
            {
                try
                {
                    string name = (string)rule.Name;
                    var match = prefixRegex.Match(name);
                    if (match.Success && int.TryParse(match.Groups["num"].Value, CultureInfo.InvariantCulture, out var index))
                    {
                        if (index >= startIndex)
                        {
                            toDelete.Add(name);
                        }
                    }
                }
                finally
                {
                    ReleaseComObject(rule);
                }
            }

            foreach (var name in toDelete)
            {
                try { policy.Rules.Remove(name); } catch { }
            }
        }

        private static void CleanLegacyRulesInternal(object policyObj)
        {
            dynamic policy = policyObj;
            var toDelete = new List<string>();
            var standardRegex = new Regex($@"^{Regex.Escape(RulePrefix)}\d+$", RegexOptions.IgnoreCase);

            foreach (dynamic rule in policy.Rules)
            {
                try
                {
                    string name = (string)rule.Name;
                    if (name.StartsWith("RDPGuard_Blocked_", StringComparison.OrdinalIgnoreCase) ||
                        (name.StartsWith(RulePrefix, StringComparison.OrdinalIgnoreCase) && !standardRegex.IsMatch(name)))
                    {
                        toDelete.Add(name);
                    }
                }
                finally
                {
                    ReleaseComObject(rule);
                }
            }

            foreach (var name in toDelete)
            {
                try { policy.Rules.Remove(name); } catch { }
            }
        }

        #endregion

        #region netsh 兜底机制

        private static bool NetshBlockFallback(string ip)
        {
            // 当 COM 失败时，降级使用 netsh 操作
            var ruleName = $"{RulePrefix}0";
            return RunNetsh($"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=block remoteip={ip} group=\"{RuleGroup}\" description=\"RDPGuard Auto Block\"");
        }

        private static bool NetshUnblockFallback(string ip)
        {
            return RunNetsh($"advfirewall firewall delete rule name=\"{RulePrefix}0\"");
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
            catch { }

            return false;
        }

        #endregion

        #region 工具方法

        private static bool TryNormalizeIp(string? ip, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(ip)) return false;

            ip = ip.Trim();
            if (ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
            {
                ip = ip[7..];
            }

            // 过滤无效或本机环回地址
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
