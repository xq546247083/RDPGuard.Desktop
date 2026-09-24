namespace RDPGuard.Entities
{
    /// <summary>
    /// IP 聚合统计实体（用于满足需求：相同的 IP 聚合显示）
    /// </summary>
    public class AggregatedIpSummary
    {
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// 登录总次数
        /// </summary>
        public int TotalAttempts { get; set; }

        /// <summary>
        /// 成功次数
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// 失败次数
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// 首次尝试时间
        /// </summary>
        public DateTime FirstAttemptTime { get; set; }

        /// <summary>
        /// 最后尝试时间
        /// </summary>
        public DateTime LastAttemptTime { get; set; }

        /// <summary>
        /// 涉及用户名（如 "administrator, test"）
        /// </summary>
        public string AttemptedUserNames { get; set; } = string.Empty;

        /// <summary>
        /// 最后一次状态描述
        /// </summary>
        public string LastReason { get; set; } = string.Empty;

        /// <summary>
        /// 当前是否已被封禁
        /// </summary>
        public bool IsBlocked { get; set; }
    }
}
