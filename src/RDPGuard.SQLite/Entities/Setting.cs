namespace RDPGuard.Entities
{
    /// <summary>
    /// 系统配置
    /// </summary>
    public class Setting
    {
        public long Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
