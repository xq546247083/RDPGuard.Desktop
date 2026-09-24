using System.Windows;

namespace RDPGuard.Common
{
    public static class ResourceHelper
    {
        public static string GetString(string key, params object[] args)
        {
            if (Application.Current?.TryFindResource(key) is string str)
            {
                return args.Length > 0 ? string.Format(str, args) : str;
            }
            return key;
        }

        public static T? GetResource<T>(string key)
        {
            if (Application.Current?.TryFindResource(key) is T res)
            {
                return res;
            }
            return default;
        }
    }
}
