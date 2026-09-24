using Microsoft.EntityFrameworkCore;
using RDPGuard.Entities;

namespace RDPGuard
{
    /// <summary>
    /// RDPGuard 数据库上下文
    /// </summary>
    public class RDPGuardDbContext : DbContext
    {
        public DbSet<RdpLoginRecord> RdpLoginRecords { get; set; } = null!;
        public DbSet<BannedIp> BannedIps { get; set; } = null!;
        public DbSet<Setting> Settings { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                if (!Directory.Exists(dbDirectory))
                {
                    Directory.CreateDirectory(dbDirectory);
                }

                var dbPath = Path.Combine(dbDirectory, "rdpguard.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 登录流水表
            modelBuilder.Entity<RdpLoginRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.IpAddress);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.EventRecordId);
                entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(64);
                entity.Property(e => e.UserName).HasMaxLength(128);
                entity.Property(e => e.DomainName).HasMaxLength(128);
                entity.Property(e => e.LogonTypeDescription).HasMaxLength(64);
                entity.Property(e => e.FailureReason).HasMaxLength(256);
            });

            // 封禁表
            modelBuilder.Entity<BannedIp>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.IpAddress);
                entity.HasIndex(e => e.IsActive);
                entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(64);
                entity.Property(e => e.Reason).HasMaxLength(256);
            });

            // 配置表
            modelBuilder.Entity<Setting>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Key).IsUnique();
                entity.Property(e => e.Key).IsRequired().HasMaxLength(64);
                entity.Property(e => e.Value).IsRequired();
            });
        }
    }
}
