namespace RDPGuard.Model
{
    /// <summary>
    /// RDP 远程登录事件模型
    /// </summary>
    public class RdpEventModel
    {
        public long RecordId { get; set; }
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string DomainName { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public int LogonType { get; set; }
        public string LogonTypeDescription { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SubStatus { get; set; } = string.Empty;
        public string EventSource { get; set; } = string.Empty;
    }
}
