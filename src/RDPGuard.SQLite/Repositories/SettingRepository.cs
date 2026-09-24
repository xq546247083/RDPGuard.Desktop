using RDPGuard.Entities;

namespace RDPGuard.Repositories
{
    /// <summary>
    /// 配置仓储
    /// </summary>
    public static class SettingRepository
    {
        public static string Get(string key, string defaultValue = "")
        {
            using var context = new RDPGuardDbContext();
            var setting = context.Settings.FirstOrDefault(s => s.Key == key);
            return setting?.Value ?? defaultValue;
        }

        public static void Set(string key, string value)
        {
            using var context = new RDPGuardDbContext();
            var setting = context.Settings.FirstOrDefault(s => s.Key == key);
            if (setting != null)
            {
                setting.Value = value;
            }
            else
            {
                context.Settings.Add(new Setting { Key = key, Value = value });
            }
            context.SaveChanges();
        }

        public static bool GetBool(string key, bool defaultValue = false)
        {
            var str = Get(key, defaultValue ? "true" : "false");
            return bool.TryParse(str, out var res) ? res : defaultValue;
        }

        public static void SetBool(string key, bool value)
        {
            Set(key, value ? "true" : "false");
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            var str = Get(key, defaultValue.ToString());
            return int.TryParse(str, out var res) ? res : defaultValue;
        }

        public static void SetInt(string key, int value)
        {
            Set(key, value.ToString());
        }
    }
}
