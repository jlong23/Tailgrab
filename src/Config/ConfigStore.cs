using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;

namespace Tailgrab.Config
{
    public static class ConfigStore
    {
        private const string RegistryPath = "Software\\DeviousFox\\Tailgrab\\Config";

        public static void SaveSecret(string name, string value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (value == null) value = string.Empty;

            var bytes = Encoding.UTF8.GetBytes(value);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            var base64 = Convert.ToBase64String(protectedBytes);

            using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                key.SetValue(name, base64, RegistryValueKind.String);
            }
        }

        public static string? LoadSecret(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
            {
                if (key == null) return null;
                var base64 = key.GetValue(name) as string;
                if (string.IsNullOrEmpty(base64)) return null;
                try
                {
                    var protectedBytes = Convert.FromBase64String(base64);
                    var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(bytes);
                }
                catch
                {
                    return null;
                }
            }
        }

        public static void DeleteSecret(string name)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true))
            {
                if (key == null) return;
                key.DeleteValue(name, throwOnMissingValue: false);
            }
        }
    }
}
