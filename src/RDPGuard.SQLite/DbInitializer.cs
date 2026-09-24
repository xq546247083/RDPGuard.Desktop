namespace RDPGuard
{
    /// <summary>
    /// 数据库初始化器
    /// </summary>
    public static class DbInitializer
    {
        /// <summary>
        /// 初始化数据库
        /// </summary>
        public static void Initialize()
        {
            using var context = new RDPGuardDbContext();
            context.Database.EnsureCreated();
        }
    }
}
