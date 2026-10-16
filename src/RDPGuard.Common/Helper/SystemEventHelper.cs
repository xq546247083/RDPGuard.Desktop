using System.Diagnostics.Eventing.Reader;

namespace RDPGuard.Helper
{
    public static class SystemEventHelper
    {
        /// <summary>
        /// 清空 Windows 系统安全日志及远程桌面连接事件日志
        /// </summary>
        public static void ClearSystemEventLogs()
        {
            EventLogSession.GlobalSession.ClearLog("Security");
        }
    }
}
