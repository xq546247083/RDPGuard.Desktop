namespace RDPGuard.Entities
{
    /// <summary>
    /// RDP 登录日志记录
    /// </summary>
    public class RdpLoginRecord
    {
        public long Id { get; set; }

        /// <summary>
        /// Windows 事件记录 ID
        /// </summary>
        public long EventRecordId { get; set; }

        /// <summary>
        /// 登录尝试时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 远程 IP 地址
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// 远程端口
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 尝试登录的用户名
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 域名或计算机名
        /// </summary>
        public string DomainName { get; set; } = string.Empty;

        /// <summary>
        /// 是否登录成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 登录类型 (10: RemoteInteractive, 3: Network)
        /// </summary>
        public int LogonType { get; set; }

        /// <summary>
        /// 登录类型说明
        /// </summary>
        public string LogonTypeDescription { get; set; } = string.Empty;

        /// <summary>
        /// 失败原因 / 状态描述
        /// </summary>
        public string FailureReason { get; set; } = string.Empty;

        /// <summary>
        /// NT 状态码
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// NT 子状态码
        /// </summary>
        public string SubStatus { get; set; } = string.Empty;

        /// <summary>
        /// 事件来源
        /// </summary>
        public string EventSource { get; set; } = string.Empty;
    }
}
