using NLog;
using System.IO;
using System.Media;
using System.Windows.Media;
using System.Windows.Threading;

namespace Tailgrab.Common
{
    /// <summary>
    /// Central sound playback helper. Call from anywhere in the solution:
    /// <code>SoundManager.PlaySound("Asterisk");</code>
    /// or
    /// <code>SoundManager.PlaySound("notification");</code>
    /// which will look for ./sounds/notification.wav|.mp3|.ogg
    /// </summary>
    public static class SoundManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly string[] allSystemSounds = { "*NONE", "Asterisk", "Beep", "Exclamation", "Warning", "Hand", "Error", "Question" };

        /// <summary>
        /// Enumerate available sound base filenames (without extension) from the ./sounds directory.
        /// Looks for files with extensions: .wav, .mp3, .ogg and returns a distinct, ordered list.
        /// </summary>
        public static List<string> GetAvailableSounds()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var soundsDir = Path.Combine(baseDir, "sounds");
                if (Directory.Exists(soundsDir))
                {

                    var exts = new[] { ".wav", ".mp3", ".ogg" };
                    var files = Directory.EnumerateFiles(soundsDir)
                        .Where(f => exts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                        .Select(f => Path.GetFileNameWithoutExtension(f))
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    files = allSystemSounds
                        .Concat(files)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return files;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to enumerate sounds directory");
            }

            return new List<string>();
        }

        public static void PlayAlertSound(string alertKey, AlertTypeEnum alertType )
        {
            string key = CommonConst.ConfigRegistryPath + "\\" + alertKey + "\\" + alertType.ToString();
            string soundSetting = ConfigStore.GetStoredKeyString(key, CommonConst.Sound_Alert_Key) ?? "Hand";
            PlaySound(soundSetting);
        }

        /// <summary>
        /// Play a system alert sound or a file under the local "sounds" directory.
        /// Recognised system names (case-insensitive): Asterisk, Beep, Exclamation, Hand, Question
        /// If the name does not match a system sound, it is treated as a base filename and looked up
        /// under the "sounds" directory (AppContext.BaseDirectory + "sounds"). Supported extensions
        /// (checked in order): .wav, .mp3, .ogg
        /// </summary>
        /// <param name="name">System code or base filename</param>
        public static void PlaySound(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            // System sounds
            switch (name.Trim().ToLowerInvariant())
            {
                case "asterisk":
                    SystemSounds.Asterisk.Play();
                    return;
                case "beep":
                    SystemSounds.Beep.Play();
                    return;
                case "exclamation":
                case "warning":
                    SystemSounds.Exclamation.Play();
                    return;
                case "hand":
                case "error":
                    SystemSounds.Hand.Play();
                    return;
                case "question":
                    SystemSounds.Question.Play();
                    return;
            }

            // Treat as filename under ./sounds
            try
            {
                var baseDir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var soundsDir = Path.Combine(baseDir, "sounds");

                string candidate = name;
                // If an absolute or relative path was passed, respect it
                if (Path.IsPathRooted(candidate))
                {
                    if (File.Exists(candidate))
                    {
                        PlayFile(candidate);
                        return;
                    }
                }
                else
                {
                    // search for file with supported extensions
                    var exts = new[] { ".wav", ".mp3", ".ogg" };
                    foreach (var ext in exts)
                    {
                        var path = Path.Combine(soundsDir, candidate + ext);
                        if (File.Exists(path))
                        {
                            PlayFile(path);
                            return;
                        }
                    }

                    // also allow candidate to already include extension inside sounds dir
                    var direct = Path.Combine(soundsDir, candidate);
                    if (File.Exists(direct))
                    {
                        PlayFile(direct);
                        return;
                    }
                }

                Logger.Warn($"Sound file not found for '{name}' in '{soundsDir}'");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to play sound '{name}'");
            }
        }

        private static void PlayFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".wav")
            {
                try
                {
                    // SoundPlayer supports WAV playback simply
                    Task.Run(() =>
                    {
                        try
                        {
                            using var sp = new SoundPlayer(path);
                            sp.Play();
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, $"Failed to play wav '{path}'");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"Failed to start playback for wav '{path}'");
                }
            }
            else
            {
                // For mp3/ogg attempt to play using WPF MediaPlayer on an STA thread with its own Dispatcher
                var t = new Thread(() =>
                {
                    try
                    {
                        var player = new MediaPlayer();
                        player.Open(new Uri(path));

                        // When playback ends or fails, shutdown the dispatcher to allow thread to exit
                        player.MediaEnded += (_, __) =>
                        {
                            try { player.Close(); } catch { }
                            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                        };
                        player.MediaFailed += (_, __) =>
                        {
                            try { player.Close(); } catch { }
                            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                        };

                        player.Play();

                        // Start a dispatcher loop to service MediaPlayer events
                        Dispatcher.Run();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Failed to play media '{path}'");
                    }
                });

                t.IsBackground = true;
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
            }
        }
    }
}
