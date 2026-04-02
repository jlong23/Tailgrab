using NLog;
using System.IO;
using Tailgrab.Common;

namespace tailgrab.Common
{
    public class TestImageManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static List<string> GetAvailableImages()
        {
            try
            {
                var soundsDir = Path.Combine(CommonConst.APPLICATION_LOCAL_DATA_PATH, "test-images");
                if (Directory.Exists(soundsDir))
                {

                    var exts = new[] { ".png", ".jpg", ".gif", ".webp" };
                    var files = Directory.EnumerateFiles(soundsDir)
                        .Where(f => exts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                        .Select(f => Path.GetFileNameWithoutExtension(f))
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    files = files
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return files;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to enumerate sounds directory");
            }

            return [];
        }
    }
}
