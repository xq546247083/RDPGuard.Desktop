namespace RDPGuard
{
    /// <summary>
    /// 全局信息
    /// </summary>
    public static class AppGlobal
    {
        /// <summary>
        /// 应用名
        /// </summary>
        public static string AppName = "RDPGuard";

        /// <summary>
        /// 应用中文名
        /// </summary>
        public static string AppChineseName = "RDP 远程登录监控与防护";

        /// <summary>
        /// 防火墙封禁规则前缀（对齐 IPBan 规则规范）
        /// </summary>
        public static string FirewallRulePrefix = "RDPGuard_Block_";
    }
}
