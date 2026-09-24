namespace RDPGuard.Entities
{
    /// <summary>
    /// 被封禁的 IP 实体
    /// </summary>
    public class BannedIp
    {
        public long Id { get; set; }

        /// <summary>
        /// 封禁的 IP
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// 封禁时间
        /// </summary>
        public DateTime BanTime { get; set; }

        /// <summary>
        /// 封禁原因
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 是否处于生效状态
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 解封时间
        /// </summary>
        public DateTime? UnbanTime { get; set; }
    }
}
