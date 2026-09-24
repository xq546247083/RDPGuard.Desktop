namespace RDPGuard.Enums
{
    /// <summary>
    /// 主界面视图选项卡
    /// </summary>
    public enum AppTabType
    {
        /// <summary>
        /// 聚合显示（相同IP聚合统计）
        /// </summary>
        AggregatedIps,

        /// <summary>
        /// 实时日志流水（成功与失败记录）
        /// </summary>
        RealtimeLogs,

        /// <summary>
        /// 封禁黑名单
        /// </summary>
        BannedList,

        /// <summary>
        /// 设置与自启
        /// </summary>
        Settings
    }
}
