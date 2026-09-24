using Microsoft.EntityFrameworkCore;
using RDPGuard.Entities;

namespace RDPGuard.Repositories
{
    /// <summary>
    /// RDP 登录日志仓储
    /// </summary>
    public static class RdpRecordRepository
    {
        private static readonly object LockObj = new();

        /// <summary>
        /// 添加单条登录记录（自动排重：同 RecordId 和时间）
        /// </summary>
        public static bool AddRecord(RdpLoginRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.IpAddress)) return false;

            lock (LockObj)
            {
                using var context = new RDPGuardDbContext();
                // 检查是否已存在同记录
                var exists = context.RdpLoginRecords.Any(r =>
                    r.EventRecordId == record.EventRecordId &&
                    r.Timestamp == record.Timestamp &&
                    r.IpAddress == record.IpAddress);

                if (exists) return false;

                context.RdpLoginRecords.Add(record);
                context.SaveChanges();
                return true;
            }
        }

        /// <summary>
        /// 批量添加记录（用于历史扫描初次导入）
        /// </summary>
        public static int AddRecords(IEnumerable<RdpLoginRecord> records)
        {
            lock (LockObj)
            {
                using var context = new RDPGuardDbContext();
                var addedCount = 0;
                var list = records.ToList();
                if (list.Count == 0) return 0;

                // 提取本次批量的时间范围和已有的 recordId
                var minTime = list.Min(r => r.Timestamp);
                var maxTime = list.Max(r => r.Timestamp);

                var existingKeys = context.RdpLoginRecords
                    .Where(r => r.Timestamp >= minTime && r.Timestamp <= maxTime)
                    .Select(r => new { r.EventRecordId, r.Timestamp, r.IpAddress })
                    .ToHashSet();

                var toAdd = new List<RdpLoginRecord>();
                foreach (var item in list)
                {
                    var key = new { item.EventRecordId, item.Timestamp, item.IpAddress };
                    if (!existingKeys.Contains(key))
                    {
                        toAdd.Add(item);
                        existingKeys.Add(key);
                        addedCount++;
                    }
                }

                if (toAdd.Count > 0)
                {
                    context.RdpLoginRecords.AddRange(toAdd);
                    context.SaveChanges();
                }

                return addedCount;
            }
        }

        /// <summary>
        /// 获取最近的登录流水记录
        /// </summary>
        public static List<RdpLoginRecord> GetRecentRecords(int count = 500, string? keyword = null)
        {
            using var context = new RDPGuardDbContext();
            IQueryable<RdpLoginRecord> query = context.RdpLoginRecords;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(r => r.IpAddress.Contains(keyword) || r.UserName.ToLower().Contains(keyword));
            }

            return query.OrderByDescending(r => r.Timestamp).Take(count).ToList();
        }

        /// <summary>
        /// 获取某 IP 的所有流水明细
        /// </summary>
        public static List<RdpLoginRecord> GetRecordsByIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return new List<RdpLoginRecord>();
            ip = ip.Trim();

            using var context = new RDPGuardDbContext();
            return context.RdpLoginRecords
                .Where(r => r.IpAddress == ip)
                .OrderByDescending(r => r.Timestamp)
                .ToList();
        }

        /// <summary>
        /// 聚合查询：按 IP 聚合显示统计数据
        /// </summary>
        public static List<AggregatedIpSummary> GetAggregatedSummaries(string? keyword = null)
        {
            using var context = new RDPGuardDbContext();

            // 1. 获取所有生效的封禁 IP
            var activeBannedIps = context.BannedIps
                .Where(b => b.IsActive)
                .Select(b => b.IpAddress)
                .ToHashSet();

            // 2. 基础流水查询
            IQueryable<RdpLoginRecord> query = context.RdpLoginRecords;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(r => r.IpAddress.Contains(keyword) || r.UserName.ToLower().Contains(keyword));
            }

            var records = query.ToList();

            // 3. 内存中聚合分组计算
            var grouped = records
                .GroupBy(r => r.IpAddress)
                .Select(g =>
                {
                    var sorted = g.OrderBy(r => r.Timestamp).ToList();
                    var last = sorted.Last();
                    var first = sorted.First();
                    var usernames = sorted
                        .Where(r => !string.IsNullOrWhiteSpace(r.UserName))
                        .Select(r => r.UserName)
                        .Distinct()
                        .ToList();

                    return new AggregatedIpSummary
                    {
                        IpAddress = g.Key,
                        TotalAttempts = sorted.Count,
                        SuccessCount = sorted.Count(r => r.IsSuccess),
                        FailedCount = sorted.Count(r => !r.IsSuccess),
                        FirstAttemptTime = first.Timestamp,
                        LastAttemptTime = last.Timestamp,
                        AttemptedUserNames = string.Join(", ", usernames.Take(5)) + (usernames.Count > 5 ? $" 等({usernames.Count}个)" : ""),
                        LastReason = last.IsSuccess ? "登录成功" : (string.IsNullOrWhiteSpace(last.FailureReason) ? "登录失败" : last.FailureReason),
                        IsBlocked = activeBannedIps.Contains(g.Key)
                    };
                })
                .OrderByDescending(s => s.LastAttemptTime)
                .ToList();

            return grouped;
        }

        /// <summary>
        /// 获取指定 IP 在指定时间之后的登录失败次数（用于滑动时间窗口判定）
        /// </summary>
        public static int GetRecentFailureCountByIp(string ipAddress, DateTime sinceTime)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return 0;

            lock (LockObj)
            {
                using var context = new RDPGuardDbContext();
                return context.RdpLoginRecords.Count(r => r.IpAddress == ipAddress && !r.IsSuccess && r.Timestamp >= sinceTime);
            }
        }

        /// <summary>
        /// 清空所有流水记录
        /// </summary>
        public static void ClearAllRecords()
        {
            lock (LockObj)
            {
                using var context = new RDPGuardDbContext();
                context.RdpLoginRecords.ExecuteDelete();
            }
        }
    }
}
