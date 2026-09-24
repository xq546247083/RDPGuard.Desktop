using System.Windows;

namespace RDPGuard.Common
{
    /// <summary>
    /// 扩展 CommandParameter，使 CommandParameter 可以带事件参数
    /// </summary>
    public class CommandParameterEx
    {
        public DependencyObject? Sender { get; set; }
        public EventArgs? EventArgs { get; set; }
        public object? Parameter { get; set; }
    }
}
