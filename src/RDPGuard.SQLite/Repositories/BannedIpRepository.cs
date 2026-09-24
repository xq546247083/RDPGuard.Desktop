using RDPGuard.Entities;
using RDPGuard.Helper;

namespace RDPGuard.Repositories
{
    /// <summary>
    /// IP 封禁仓储
    /// </summary>
    public static class BannedIpRepository
    {
        private static readonly object LockObj = new();

        /// <summary>
        /// 封禁 IP（同步更新防火墙与数据库）
        /// </summary>
        public static bool BanIp(string ip, string reason = "手动封禁")
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            ip = ip.Trim();

            lock (LockObj)
            {
                // 调用系统防火墙封禁
                var fwSuccess = FirewallHelper.BlockIp(ip, reason);

                using var context = new RDPGuardDbContext();
                var existing = context.BannedIps.FirstOrDefault(b => b.IpAddress == ip && b.IsActive);
                if (existing != null)
                {
                    existing.Reason = reason;
                    existing.BanTime = DateTime.Now;
                }
                else
                {
                    context.BannedIps.Add(new BannedIp
                    {
                        IpAddress = ip,
                        BanTime = DateTime.Now,
                        Reason = reason,
                        IsActive = true
                    });
                }

                context.SaveChanges();
                return fwSuccess;
            }
        }

        /// <summary>
        /// 解封 IP（同步解除防火墙与更新数据库）
        /// </summary>
        public static bool UnbanIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            ip = ip.Trim();

            lock (LockObj)
            {
                FirewallHelper.UnblockIp(ip);

                using var context = new RDPGuardDbContext();
                var items = context.BannedIps.Where(b => b.IpAddress == ip && b.IsActive).ToList();
                foreach (var item in items)
                {
                    item.IsActive = false;
                    item.UnbanTime = DateTime.Now;
                }

                context.SaveChanges();
                return true;
            }
        }

        /// <summary>
        /// 查询 IP 是否处于封禁状态
        /// </summary>
        public static bool IsBanned(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            ip = ip.Trim();

            using var context = new RDPGuardDbContext();
            var inDb = context.BannedIps.Any(b => b.IpAddress == ip && b.IsActive);
            if (inDb) return true;

            // 检查系统防火墙中是否已被封禁
            return FirewallHelper.IsIpBlocked(ip);
        }

        /// <summary>
        /// 获取所有生效中的封禁记录
        /// </summary>
        public static List<BannedIp> GetAllActive()
        {
            using var context = new RDPGuardDbContext();
            return context.BannedIps.Where(b => b.IsActive).OrderByDescending(b => b.BanTime).ToList();
        }

        /// <summary>
        /// 获取所有封禁历史记录
        /// </summary>
        public static List<BannedIp> GetAll()
        {
            using var context = new RDPGuardDbContext();
            return context.BannedIps.OrderByDescending(b => b.BanTime).ToList();
        }

        /// <summary>
        /// 全量同步本地数据库黑名单到系统防火墙（按 1000 IP 聚合并清理遗留规则）
        /// </summary>
        public static bool SyncFirewallRules()
        {
            lock (LockObj)
            {
                using var context = new RDPGuardDbContext();
                var activeIps = context.BannedIps.Where(b => b.IsActive).Select(b => b.IpAddress).ToList();
                return FirewallHelper.SyncAllBlockedIps(activeIps);
            }
        }
    }
}
