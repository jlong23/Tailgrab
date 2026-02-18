using Microsoft.Win32;
using NLog;
using System.Security.Cryptography;
using System.Text;

namespace Tailgrab.Common
{
    public static class ConfigStore
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();

        public static void SaveSecret(string name, string value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (value == null) value = string.Empty;

            var bytes = Encoding.UTF8.GetBytes(value);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            var base64 = Convert.ToBase64String(protectedBytes);

            using (var key = Registry.CurrentUser.CreateSubKey(CommonConst.ConfigRegistryPath))
            {
                key.SetValue(name, base64, RegistryValueKind.String);
            }
        }

        public static string? LoadSecret(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            using (var key = Registry.CurrentUser.OpenSubKey(CommonConst.ConfigRegistryPath))
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
            using (var key = Registry.CurrentUser.OpenSubKey(CommonConst.ConfigRegistryPath, writable: true))
            {
                if (key == null) return;
                key.DeleteValue(name, throwOnMissingValue: false);
            }
        }

        public static string? GetStoredUri(string keyName)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(CommonConst.ConfigRegistryPath))
                {
                    if (key == null)
                    {
                        logger.Debug($"Registry key does not exist {keyName}");
                        return null;
                    }

                    string? value = key.GetValue(keyName) as string;
                    if (string.IsNullOrEmpty(value))
                    {
                        logger.Debug($"No Value stored in registry. {keyName}");
                        return null;
                    }

                    return value;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to read value from registry.");
                return null;
            }
        }

        public static void PutStoredUri(string keyName, string keyValue)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(CommonConst.ConfigRegistryPath))
                {
                    key.SetValue(keyName, keyValue, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to save value to registry. {keyName}");
            }
        }
    }
}
