using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using NLog;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Tailgrab.Common;
using Tailgrab.Configuration;
using Tailgrab.LineHandler;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;

namespace Tailgrab;

public class FileTailer
{
    /// <summary>
    /// A list of the Regex Line Matchers that are used to process log lines.
    /// </summary>
    static List<ILineHandler> HandlerList = new List<ILineHandler> { };

    /// <summary>
    /// A List of opened file paths to avoid opening the same file multiple times.
    /// </summary>
    static List<string> OpenedFiles = new List<string> { };
    static Dictionary<string, FileWatchItem> WatchedFiles = new Dictionary<string, FileWatchItem>();

    /// <summary>
    /// The path to the user's profile directory.
    /// </summary>
    internal static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// The path to the VRChat AppData directory.
    /// </summary>
    public static readonly string VRChatAppDataPath = Path.Combine(UserProfile, @"AppData", "LocalLow", "VRChat", "VRChat");
    public static readonly string VRChatAmplitudePath = Path.Combine(UserProfile, @"AppData", "Local", "Temp", "VRChat", "VRChat");

    static FileSystemWatcher LogWatcher = new FileSystemWatcher();
    static FileSystemWatcher AmpWatcher = new FileSystemWatcher();
    static Logger logger = LogManager.GetCurrentClassLogger();
    static ServiceRegistry? _serviceRegistry;

    // At the class level, add a dictionary to track active tail tasks
    static Dictionary<string, FileTailStatus> ActiveTailTasks = new Dictionary<string, FileTailStatus>();

    public static IReadOnlyDictionary<string, FileTailStatus> GetActiveTailTasks() => ActiveTailTasks;

    /// <summary>
    /// Watch the VRChat log directory by default and process logs.
    /// Show the TailgrabPanel UI on the STA thread before continuing to watch files.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        //Early in your program do something like this:
        NLog.GlobalDiagnosticsContext.Set("StartTime", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        string configFilePath = Path.Combine(CommonConst.APPLICATION_LOCAL_DATA_PATH, "NLog.config");
        LogManager.Setup().LoadConfigurationFromFile(configFilePath);

        // Basic command line parsing:
        // -l <FilePath>    : use explicit log folder/file path
        // -clear           : remove application registry settings and exit
        // -backup          : create a backup of the database and exit
        string? explicitPath = null;
        bool clearRegistry = false;
        bool upgrade = false;
        bool backup = false;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "-l", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                explicitPath = args[i + 1];
                i++;
            }
            else if (string.Equals(a, "-clear", StringComparison.OrdinalIgnoreCase))
            {
                clearRegistry = true;
            }
            else if (string.Equals(a, "-backup", StringComparison.OrdinalIgnoreCase))
            {
                backup = true;
            }
            else if (string.Equals(a, "-upgrade", StringComparison.OrdinalIgnoreCase))
            {
                upgrade = true;
            }
        }

        if (clearRegistry)
        {
            DeleteTailgrabRegistrySettings();

            // Exit application after clearing settings
            return;
        }

        _serviceRegistry = new ServiceRegistry();
        _serviceRegistry.StartAllServices();

        UpgradeApplication(_serviceRegistry);



        if (upgrade)
        {
            UpgradeApplication(_serviceRegistry);
        }

        if (backup)
        {
            CreateDatabaseBackup();

            // Exit application after creating backup
            return;
        }

        string filePath = GetLogsPath(args, explicitPath);
        if (!Directory.Exists(filePath))
        {
            logger.Info($"Missing VRChat log directory at '{filePath}'");
            return;
        }

        ConfigurationManager configurationManager = new ConfigurationManager(_serviceRegistry);
        configurationManager.LoadLineHandlersFromConfig(HandlerList);

        // Start the watcher task on a background thread so it doesn't block the STA UI thread
        logger.Info($"Starting file watcher and showing UI for: '{filePath}'");
        _ = Task.Run(() => WatchPath(filePath, _serviceRegistry));

        // Start the Amplitude Cache watcher task on a background thread
        string ampPath = VRChatAmplitudePath + Path.DirectorySeparatorChar;
        logger.Info($"Starting Amplitude Cache watcher for: '{ampPath}'");
        _ = Task.Run(() => WatchAmpCache(ampPath, _serviceRegistry));

        //SyncAvatarModerations(_serviceRegistry);

        // Check for updates before showing the main window
        _ = Task.Run(async () => await CheckForUpdatesAsync());

        BuildAppWindow(_serviceRegistry);

        // When the window closes, allow Main to complete. The watcher task will be abandoned; if desired add cancellation.
    }

    /// <summary>
    /// Threaded tailing of a file, reading new lines as they are added.
    /// Returns the FileTailStatus immediately and processes the file in the background.
    /// </summary>
    public static FileTailStatus? TailFileAsync(string filePath)
    {
        if (OpenedFiles.Contains(filePath))
        {
            return null;
        }

        var status = new FileTailStatus(filePath);
        logger.Info($"Tailing file: {filePath}");

        OpenedFiles.Add(filePath);

        // Start the file tailing process in the background
        _ = Task.Run(async () =>
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                {
                    // Start at the end of the file
                    long lastMaxOffset = fs.Length;
                    fs.Seek(lastMaxOffset, SeekOrigin.Begin);

                    WatchedFiles[filePath] = new FileWatchItem(lastMaxOffset);

                    while (!status.IsCancellationRequested)
                    {
                        // If the file size hasn't changed, wait
                        if (fs.Length == lastMaxOffset)
                        {
                            if (WatchedFiles.ContainsKey(filePath))
                            {
                                WatchedFiles[filePath].ElapsedTime += 1;
                                if (WatchedFiles[filePath].ElapsedTime >= 9000) // If we've been watching this file for 15 minutes without changes
                                {
                                    logger.Info($"Timeout waiting for new lines in '{filePath}'");
                                    break;
                                }
                            }

                            await Task.Delay(100, status.CancellationSource.Token).ConfigureAwait(false);
                            continue;
                        }

                        // Read and display new lines
                        string? line;
                        while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) != null)
                        {
                            if (status.IsCancellationRequested)
                                break;

                            foreach (ILineHandler handler in HandlerList)
                            {
                                if (handler.HandleLine(line))
                                {
                                    break;
                                }
                            }

                            status.IncrementLinesProcessed();
                        }

                        // Update the offset to the new end of the file
                        lastMaxOffset = fs.Length;

                        // Reset the watch counter for this file since we have new data
                        WatchedFiles.Remove(filePath);
                    }
                }

                logger.Info($"Stopped tailing file: {filePath}. Total lines processed: {status.LinesProcessed}");
            }
            catch (OperationCanceledException)
            {
                logger.Info($"Tailing cancelled for: {filePath}. Total lines processed: {status.LinesProcessed}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error tailing file: {filePath}");
            }
            finally
            {
                // Clean up
                OpenedFiles.Remove(filePath);
                WatchedFiles.Remove(filePath);
                ActiveTailTasks.Remove(filePath);
            }
        });

        return status;
    }

    /// <summary>
    /// Watch and dispatch File Tailing.
    /// </summary>
    public static async Task WatchPath(string path, ServiceRegistry _serviceRegistry)
    {
        LogWatcher.Path = Path.GetDirectoryName(path)!;
        LogWatcher.Filter = Path.GetFileName("output_log*.txt");
        LogWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;

        LogWatcher.Created += async (source, e) =>
        {
            var status = TailFileAsync(e.FullPath);
            if (status != null)
            {
                ActiveTailTasks[e.FullPath] = status;
            }
        };

        LogWatcher.Changed += async (source, e) =>
        {
            var status = TailFileAsync(e.FullPath);
            if (status != null)
            {
                ActiveTailTasks[e.FullPath] = status;
            }
        };

        LogWatcher.EnableRaisingEvents = true;

        // ensure existing files are tailed immediately
        try
        {
            string _todaysFile = $"output_log_{DateTime.Now:yyyy-MM-dd}_*.txt";
            logger.Debug($"Looking for existing log files matching: {_todaysFile}");
            var existing = Directory.GetFiles(LogWatcher.Path, _todaysFile);
            foreach (var f in existing)
            {
                logger.Debug($"Found File to Tail: {f}");
                _ = Task.Run(() =>
                {
                    var status = TailFileAsync(f);
                    if (status != null)
                    {
                        ActiveTailTasks[f] = status;
                    }
                });
            }
        }
        catch
        {
            /* ignore errors */
        }

        // Keep the application running to monitor changes
        await Task.Delay(Timeout.Infinite);
    }


    public static async Task ProcessAmplitudeCache(string filePath, ServiceRegistry ServiceRegistryInstance)
    {
        bool saveAvatars = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Discovered_Avatar_Caching, true);
        if (!saveAvatars) {  
              logger.Info($"Ignoring changes in Amplitude cache at '{filePath}'. Due to Discover Avatar Flag...");
              return;
        }

        // Resolve path: prefer explicit, then local repo config.json, then user config path
        if (!File.Exists(filePath))
        {
            logger.Warn($"AmplitudeCache file not found at '{filePath}'.");
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(filePath);

            // Parse the JSON and extract ROOT[0]/event_properties/avatarIdsEncountered
            var avatarIds = new List<string>();

            using (JsonDocument doc = JsonDocument.Parse(jsonString))
            {
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var first = root[0];
                    if (first.TryGetProperty("event_properties", out JsonElement eventProps))
                    {
                        if (eventProps.TryGetProperty("avatarIdsEncountered", out JsonElement avatarArray) && avatarArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in avatarArray.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String)
                                {
                                    avatarIds.Add(item.GetString()!);
                                }
                            }

                            PlayerManager.CacheAvatars(avatarIds);
                        }
                    }
                }
            }

            // Do something with the extracted avatar IDs; for now log the count and a sample
            if (avatarIds.Count > 0)
            {
                ServiceRegistryInstance.GetPlayerManager().CompactDatabase();
            }

        }
        catch
        {
            //logger.Error(ex, $"Failed to read or parse Amplitude cache file '{filePath}'.");
            return;
        }
    }

    public static async Task WatchAmpCache(string path, ServiceRegistry serviceRegistryInstance)
    {
        Console.OutputEncoding = Encoding.UTF8;

        logger.Info($"Tailgrab Version: {BuildInfo.GetInformationalVersion()}");

        AmpWatcher.Path = Path.GetDirectoryName(path)!;
        AmpWatcher.Filter = Path.GetFileName("amplitude.cache");
        AmpWatcher.NotifyFilter = NotifyFilters.LastWrite;

        AmpWatcher.Changed += async (source, e) =>
        {
            await ProcessAmplitudeCache(e.FullPath, serviceRegistryInstance);
        };

        AmpWatcher.EnableRaisingEvents = true;

        // Keep the application running to monitor changes
        await Task.Delay(Timeout.Infinite);
    }

    private static void UpgradeApplication(ServiceRegistry serviceRegistry)
    {       
        // Execute registry migrations
        ExecuteRegistryMigrations(serviceRegistry);

        // Create missing registry items with default values
        InitializeMissingRegistryItems();
    }

    /// <summary>
    /// Configures and executes registry migrations to bring the registry schema up to the current application version.
    /// </summary>
    private static void ExecuteRegistryMigrations(ServiceRegistry serviceRegistry)
    {
        var migrationManager = new Tailgrab.Common.RegistryMigrationManager();
        string currentAppVersion = BuildInfo.GetInformationalVersion().Split('+')[0].TrimStart('v');

        logger.Info($"Configuring registry migrations for application version {currentAppVersion}");

        // Register migrations in order from oldest to newest
        // Example: migrationManager.RegisterMigration("1.0.0", "1.1.0", MigrateFrom_1_0_0_To_1_1_0);


        // Migration from 1.1.0 to 1.1.3 - Add Ollama API settings
        migrationManager.RegisterMigration("1.1.0", "1.1.3", () =>
        {
            logger.Info("Migrating registry from 1.1.0-2 to 1.1.3: Adding Ollama API settings");

            try
            {
                // Move Registry Ollama API settings from secrets to plain text entries
                var ollamaEndpoint = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Endpoint);
                if (!string.IsNullOrEmpty(ollamaEndpoint))
                {
                    ConfigStore.DeleteSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Endpoint);
                    ConfigStore.PutStoredKeyString(Tailgrab.Common.CommonConst.Registry_Ollama_API_Endpoint, ollamaEndpoint);
                    logger.Info("Migrated Ollama API Endpoint from secrets to registry");
                }

                // Move Ollama API Model from secrets to plain text entry
                var ollamaModel = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Model);
                if (!string.IsNullOrEmpty(ollamaModel))
                {
                    ConfigStore.DeleteSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Model);
                    ConfigStore.PutStoredKeyString(Tailgrab.Common.CommonConst.Registry_Ollama_API_Model, ollamaModel);
                    logger.Info("Migrated Ollama API Model from secrets to registry");
                }

                // Move Ollama API Prompt from secrets to plain text entry
                var ollamaPrompt = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Prompt);
                if (!string.IsNullOrEmpty(ollamaPrompt))
                {
                    ConfigStore.DeleteSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Prompt);
                    ConfigStore.PutStoredKeyString(Tailgrab.Common.CommonConst.Registry_Ollama_API_Prompt, ollamaPrompt);
                    logger.Info("Migrated Ollama API Prompt from secrets to registry");
                }

                // Move Ollama API Image Prompt from secrets to plain text entry
                var ollamaImagePrompt = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Image_Prompt);
                if (!string.IsNullOrEmpty(ollamaImagePrompt))
                {
                    ConfigStore.DeleteSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Image_Prompt);
                    ConfigStore.PutStoredKeyString(Tailgrab.Common.CommonConst.Registry_Ollama_API_Image_Prompt, ollamaImagePrompt);
                    logger.Info("Migrated Ollama API Image Prompt from secrets to registry");
                }

                TailgrabDBContext dbContext = serviceRegistry.GetDBContext();
                if (dbContext != null)
                {
                    // Migrate the ProfileEvaluation table to add the new PromptMd5Checksum column, which is used to link evaluations to specific prompts.
                    try
                    {
                        dbContext.ExecuteSql("ALTER TABLE ProfileEvaluation RENAME TO ProfileEvaluation_OLD;");
                        dbContext.ExecuteSql("CREATE TABLE IF NOT EXISTS ProfileEvaluation (MD5Checksum TEXT NOT NULL, ProfileText BLOB, Evaluation\tBLOB, LastDateTime TEXT NOT NULL, isIgnored INTEGER NOT NULL DEFAULT 0, PromptMd5Checksum TEXT, CONSTRAINT PK_ProfileEvaluation PRIMARY KEY(MD5Checksum))");
                        dbContext.ExecuteSql("INSERT INTO ProfileEvaluation SELECT * FROM ProfileEvaluation WHERE 1=1");
                        dbContext.ExecuteSql("DROP TABLE IF EXISTS ProfileEvaluation_OLD");
                    }
                    catch 
                    { 
                        logger.Warn("Failed to migrate ProfileEvaluation table during registry migration. This may cause issues with profile evaluations. Consider backing up your database, deleting it, and allowing Tailgrab to create a new one if you encounter problems with profile evaluations after this update.");
                    }
                }
                
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed during registry migration from 1.1.0-2 to 1.1.3");
            }
        });

        // To add a new migration for a future version:
        // 1. Add a new RegisterMigration call with the appropriate version numbers
        // 2. Implement the migration action to handle version-specific changes
        // 3. The migration will automatically execute when users upgrade
        // Example:
        // migrationManager.RegisterMigration("1.1.3", "1.2.0", () =>
        // {
        //     logger.Info("Migrating registry from 1.1.3 to 1.2.0: Adding new feature settings");
        //     // Perform version-specific registry changes here
        //     // e.g., rename keys, remove obsolete keys, or transform data
        // });

        // Execute all pending migrations
        bool success = migrationManager.ExecuteMigrations(currentAppVersion);

        if (!success)
        {
            logger.Error("Registry migration failed. Application may not function correctly.");
        }
    }

    /// <summary>
    /// Initialize missing registry items with their default values.
    /// This ensures all required configuration keys exist without overwriting existing values.
    /// </summary>
    private static void InitializeMissingRegistryItems()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(Tailgrab.Common.CommonConst.ConfigRegistryPath);
            if (key == null)
            {
                logger.Warn("Failed to create or open registry key for configuration.");
                return;
            }

            // Helper function to set value only if it doesn't exist
            void SetDefaultIfMissing(string name, string defaultValue)
            {
                if (key.GetValue(name) == null && !string.IsNullOrEmpty(defaultValue))
                {
                    key.SetValue(name, defaultValue, RegistryValueKind.String);
                    logger.Info($"Initialized missing registry item: {name}");
                }
            }

            // Initialize Ollama API registry keys with defaults
            SetDefaultIfMissing(Tailgrab.Common.CommonConst.Registry_Ollama_API_Endpoint,
                Tailgrab.Common.CommonConst.Default_Ollama_API_Endpoint);
            SetDefaultIfMissing(Tailgrab.Common.CommonConst.Registry_Ollama_API_Model,
                Tailgrab.Common.CommonConst.Default_Ollama_API_Model);
            SetDefaultIfMissing(Tailgrab.Common.CommonConst.Registry_Ollama_API_Prompt,
                Tailgrab.Common.CommonConst.Default_Ollama_API_Prompt);
            SetDefaultIfMissing(Tailgrab.Common.CommonConst.Registry_Ollama_API_Image_Prompt,
                Tailgrab.Common.CommonConst.Default_Ollama_API_Image_Prompt);

            // Note: The following keys don't have default values and should be set by the user:
            // - Registry_VRChat_Web_UserName
            // - Registry_VRChat_Web_Password
            // - Registry_VRChat_Web_2FactorKey
            // - Registry_Ollama_API_Key
            // - Registry_Alert_Avatar
            // - Registry_Alert_Group
            // - Registry_Alert_Profile
            // - Registry_Group_Checksum
            // - Registry_Group_Gist
            // - Registry_Avatar_Checksum
            // - Registry_Avatar_Gist

            logger.Info("Registry initialization completed.");
        }
        catch (Exception ex)
        {
            logger.Warn(ex, "Failed to initialize missing registry items");
        }
    }

    private static string GetLogsPath(string[] args, string? explicitPath)
    {
        string filePath = explicitPath ?? (VRChatAppDataPath + Path.DirectorySeparatorChar);
        if (explicitPath == null)
        {
            if (args.Length == 0)
            {
                logger.Warn($"No path argument provided, defaulting to VRChat log directory: {filePath}");
            }
            else
            {
                logger.Warn($"No '-l' argument provided; ignoring other command line arguments and defaulting to VRChat log directory: {filePath}");
            }
        }
        else
        {
            logger.Info($"Using explicit path from -l: '{filePath}'");
        }

        return filePath;
    }

    private static void DeleteTailgrabRegistrySettings()
    {
        try
        {
            // Remove the Tailgrab subtree from HKCU\Software\DeviousFox
            using var baseKey = Registry.CurrentUser.OpenSubKey("Software\\DeviousFox", writable: true);
            if (baseKey != null)
            {
                try
                {
                    baseKey.DeleteSubKeyTree("Tailgrab", false);
                    logger.Info("Application registry settings cleared from HKCU\\Software\\DeviousFox\\Tailgrab");
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Failed to delete Tailgrab registry subtree");
                }
            }
            else
            {
                logger.Info("No registry settings found to clear.");
            }
        }
        catch (Exception ex)
        {
            logger.Warn(ex, "Failed while attempting to clear registry settings");
        }
    }

    /// <summary>
    /// Create a timestamped backup of the SQLite database.
    /// Exports all database tables to JSON files for easy recovery.
    /// </summary>
    private static void CreateDatabaseBackup()
    {
        try
        {
            // Get database context
            if (_serviceRegistry == null)
            {
                logger.Error("Service registry not initialized. Cannot create backup.");
                return;
            }

            var context = _serviceRegistry.GetDBContext();

            context.CreateDatabaseBackup();

        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to create database backup");
        }
    }

    /// <summary>
    /// Check GitHub for new releases and notify the user if a newer version is available.
    /// </summary>
    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Tailgrab-Update-Checker");

            var response = await httpClient.GetAsync("https://api.github.com/repos/jlong23/Tailgrab/releases/latest");

            if (!response.IsSuccessStatusCode)
            {
                logger.Debug($"Failed to check for updates. Status: {response.StatusCode}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagNameElement))
            {
                logger.Debug("No tag_name found in GitHub release response");
                return;
            }

            if (!root.TryGetProperty("name", out var releaseNameElement))
            {
                logger.Debug("No tag_name found in GitHub release response");
                return;
            }

            string latestVersion = tagNameElement.GetString() ?? "";
            string latestVersionName = releaseNameElement.GetString() ?? "";
            string currentVersion = BuildInfo.GetInformationalVersion();

            // Remove 'v' prefix if present for comparison
            latestVersion = latestVersion.TrimStart('v');
            currentVersion = currentVersion.TrimStart('v');

            // Parse and compare versions
            if (Version.TryParse(latestVersion, out var latest) && 
                Version.TryParse(currentVersion.Split('+')[0], out var current))
            {
                if (latest > current)
                {
                    logger.Info($"New version available: {latestVersion} (current: {currentVersion})");

                    // Show update notification on the UI thread
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        var dialog = new UpdateNotificationDialog(latestVersion, latestVersionName, currentVersion);
                        dialog.ShowDialog();
                    });
                }
                else
                {
                    logger.Debug($"Already on latest version: {currentVersion}");
                }
            }
            else
            {
                logger.Debug($"Failed to parse versions. Latest: {latestVersion}, Current: {currentVersion}");
            }
        }
        catch (Exception ex)
        {
            logger.Warn(ex, "Failed to check for updates");
        }
    }

    private static void BuildAppWindow(ServiceRegistry serviceRegistryInstance)
    {
        // Start WPF application and show the TailgrabPanel on this STA thread
        var app = new System.Windows.Application();

        // Dark theme resources
        var darkWindow = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));
        var darkControl = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48));
        var lightText = new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230));
        var accent = new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 44, 55));

        var highlightDark = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 109));
        var highlightDarkText = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 129));


        // Override common system brushes
        app.Resources[System.Windows.SystemColors.WindowBrushKey] = darkWindow;
        app.Resources[System.Windows.SystemColors.ControlBrushKey] = darkControl;
        app.Resources[System.Windows.SystemColors.HighlightBrushKey] = accent;
        app.Resources[System.Windows.SystemColors.ControlTextBrushKey] = lightText;
        app.Resources[System.Windows.SystemColors.WindowTextBrushKey] = lightText;
        app.Resources[System.Windows.SystemColors.HighlightTextBrushKey] = lightText;

        // Basic control styles
        var textBoxStyle = new Style(typeof(System.Windows.Controls.TextBox));
        textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));
        app.Resources[typeof(System.Windows.Controls.TextBox)] = textBoxStyle;

        var listViewStyle = new Style(typeof(System.Windows.Controls.ListView));
        listViewStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        listViewStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Controls.ListView)] = listViewStyle;

        // ListViewItem style - darker row highlight and mouse-over
        var listViewItemStyle = new Style(typeof(System.Windows.Controls.ListViewItem));
        listViewItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        listViewItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        listViewItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, System.Windows.Media.Brushes.Transparent));

        var selectedTrigger = new Trigger { Property = System.Windows.Controls.ListViewItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        selectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        listViewItemStyle.Triggers.Add(selectedTrigger);

        var mouseOverTrigger = new Trigger { Property = System.Windows.Controls.ListViewItem.IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        mouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        listViewItemStyle.Triggers.Add(mouseOverTrigger);

        app.Resources[typeof(System.Windows.Controls.ListViewItem)] = listViewItemStyle;

        // GridViewColumnHeader style for list headers
        var headerStyle = new Style(typeof(System.Windows.Controls.GridViewColumnHeader));
        headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));

        var headerMouseOverTrigger = new Trigger { Property = System.Windows.Controls.GridViewColumnHeader.IsMouseOverProperty, Value = true };
        headerMouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        headerMouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        headerStyle.Triggers.Add(headerMouseOverTrigger);

        var headerPressedTrigger = new Trigger { Property = System.Windows.Controls.GridViewColumnHeader.IsPressedProperty, Value = true };
        headerPressedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        headerPressedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        headerStyle.Triggers.Add(headerPressedTrigger);

        app.Resources[typeof(System.Windows.Controls.GridViewColumnHeader)] = headerStyle;

        // DataGrid dark theme styles for Config -> Avatars DB
        var dataGridStyle = new Style(typeof(System.Windows.Controls.DataGrid));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.DataGrid.RowBackgroundProperty, darkWindow));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.DataGrid.AlternatingRowBackgroundProperty, darkControl));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.DataGrid.GridLinesVisibilityProperty, System.Windows.Controls.DataGridGridLinesVisibility.None));
        app.Resources[typeof(System.Windows.Controls.DataGrid)] = dataGridStyle;

        var dgHeaderStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        dgHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        dgHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        dgHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));
        app.Resources[typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)] = dgHeaderStyle;

        var dgCellStyle = new Style(typeof(System.Windows.Controls.DataGridCell));
        dgCellStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        dgCellStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        dgCellStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, System.Windows.Media.Brushes.Transparent));
        var cellSelectedTrigger = new Trigger { Property = System.Windows.Controls.DataGridCell.IsSelectedProperty, Value = true };
        cellSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        cellSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        dgCellStyle.Triggers.Add(cellSelectedTrigger);
        app.Resources[typeof(System.Windows.Controls.DataGridCell)] = dgCellStyle;

        var dgRowStyle = new Style(typeof(System.Windows.Controls.DataGridRow));
        dgRowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        dgRowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        var rowSelectedTrigger = new Trigger { Property = System.Windows.Controls.DataGridRow.IsSelectedProperty, Value = true };
        rowSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        rowSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        dgRowStyle.Triggers.Add(rowSelectedTrigger);
        app.Resources[typeof(System.Windows.Controls.DataGridRow)] = dgRowStyle;

        // ScrollBar and Thumb styles
        var scrollBarStyle = new Style(typeof(System.Windows.Controls.Primitives.ScrollBar));
        scrollBarStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        app.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = scrollBarStyle;

        var thumbStyle = new Style(typeof(System.Windows.Controls.Primitives.Thumb));
        thumbStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        app.Resources[typeof(System.Windows.Controls.Primitives.Thumb)] = thumbStyle;

        var buttonStyle = new Style(typeof(System.Windows.Controls.Button));
        buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Controls.Button)] = buttonStyle;

        var tabControlStyle = new Style(typeof(System.Windows.Controls.TabControl));
        tabControlStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        tabControlStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        tabControlStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, darkControl));
        app.Resources[typeof(System.Windows.Controls.TabControl)] = tabControlStyle;

        // TabItem style - for individual tab headers
        var tabItemStyle = new Style(typeof(System.Windows.Controls.TabItem));
        tabItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        tabItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        tabItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, darkControl));
        tabItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(8, 4, 8, 4)));

        // Disabled tab - slightly lighter background
        var tabDisabledTrigger = new Trigger { Property = System.Windows.Controls.TabItem.IsEnabledProperty, Value = false };
        tabDisabledTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55, 55, 58))));
        tabDisabledTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(130, 130, 130))));
        tabItemStyle.Triggers.Add(tabDisabledTrigger);

        // Selected tab - highlighted dark theme
        var tabSelectedTrigger = new Trigger { Property = System.Windows.Controls.TabItem.IsSelectedProperty, Value = true };
        tabSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        tabSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        tabItemStyle.Triggers.Add(tabSelectedTrigger);

        // Mouse over tab - lighter highlight (only for enabled tabs)
        var tabMouseOverTrigger = new Trigger { Property = System.Windows.Controls.TabItem.IsMouseOverProperty, Value = true };
        tabMouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 63))));
        tabMouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        tabItemStyle.Triggers.Add(tabMouseOverTrigger);

        app.Resources[typeof(System.Windows.Controls.TabItem)] = tabItemStyle;

        var groupBoxStyle = new Style(typeof(System.Windows.Controls.GroupBox));
        groupBoxStyle.Setters.Add(new Setter(System.Windows.Window.BackgroundProperty, darkWindow));
        groupBoxStyle.Setters.Add(new Setter(System.Windows.Window.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Controls.GroupBox)] = groupBoxStyle;

        // ComboBox requires a custom template to properly theme all internal parts
        var comboBoxTemplate = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.ComboBox));
        var templateContent = @"
            <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                           xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                           TargetType='{x:Type ComboBox}'>
                <Grid>
                    <ToggleButton x:Name='ToggleButton' 
                                  Background='#FF2D2D30'
                                  BorderBrush='#FF007ACC'
                                  Foreground='#FFE6E6E6'
                                  IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'
                                  ClickMode='Press'>
                        <ToggleButton.Template>
                            <ControlTemplate TargetType='ToggleButton'>
                                <Border Background='{TemplateBinding Background}' 
                                        BorderBrush='{TemplateBinding BorderBrush}' 
                                        BorderThickness='1'>
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition />
                                            <ColumnDefinition Width='20' />
                                        </Grid.ColumnDefinitions>
                                        <Path Grid.Column='1' HorizontalAlignment='Center' VerticalAlignment='Center'
                                              Data='M 0 0 L 4 4 L 8 0 Z' Fill='#FFE6E6E6' />
                                    </Grid>
                                </Border>
                            </ControlTemplate>
                        </ToggleButton.Template>
                    </ToggleButton>
                    <ContentPresenter x:Name='ContentSite' 
                                      Content='{TemplateBinding SelectionBoxItem}'
                                      ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                                      ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'
                                      Margin='6,3,23,3'
                                      VerticalAlignment='Center'
                                      HorizontalAlignment='Left'
                                      IsHitTestVisible='False' />
                    <Popup x:Name='Popup' 
                           Placement='Bottom'
                           IsOpen='{TemplateBinding IsDropDownOpen}'
                           AllowsTransparency='True' 
                           Focusable='False'
                           PopupAnimation='Slide'>
                        <Grid MinWidth='{TemplateBinding ActualWidth}' MaxHeight='{TemplateBinding MaxDropDownHeight}'>
                            <Border Background='#FF2D2D30' BorderBrush='#FF007ACC' BorderThickness='1'>
                                <ScrollViewer>
                                    <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained' />
                                </ScrollViewer>
                            </Border>
                        </Grid>
                    </Popup>
                </Grid>
            </ControlTemplate>";

        var comboBoxStyle = new Style(typeof(System.Windows.Controls.ComboBox));
        comboBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        comboBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        comboBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));
        comboBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty,
            (System.Windows.Controls.ControlTemplate)System.Windows.Markup.XamlReader.Parse(templateContent)));
        app.Resources[typeof(System.Windows.Controls.ComboBox)] = comboBoxStyle;

        // Ensure ComboBox items and selected text use the dark theme when the control is not focused.
        var comboBoxItemStyle = new Style(typeof(System.Windows.Controls.ComboBoxItem));
        comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, System.Windows.Media.Brushes.Transparent));
        comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(4)));

        var comboSelectedTrigger = new Trigger { Property = System.Windows.Controls.ComboBoxItem.IsSelectedProperty, Value = true };
        comboSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 63))));
        comboSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        comboBoxItemStyle.Triggers.Add(comboSelectedTrigger);

        var comboHighlightedTrigger = new Trigger { Property = System.Windows.Controls.ComboBoxItem.IsHighlightedProperty, Value = true };
        comboHighlightedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 53))));
        comboHighlightedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        comboBoxItemStyle.Triggers.Add(comboHighlightedTrigger);

        app.Resources[typeof(System.Windows.Controls.ComboBoxItem)] = comboBoxItemStyle;

        // Style for the ComboBox toggle button
        var toggleButtonStyle = new Style(typeof(System.Windows.Controls.Primitives.ToggleButton));
        toggleButtonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        toggleButtonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        toggleButtonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));
        app.Resources[typeof(System.Windows.Controls.Primitives.ToggleButton)] = toggleButtonStyle;

        // Style for ComboBox Popup background
        var popupStyle = new Style(typeof(System.Windows.Controls.Primitives.Popup));
        popupStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        app.Resources[typeof(System.Windows.Controls.Primitives.Popup)] = popupStyle;

        var windowStyle = new Style(typeof(System.Windows.Window));
        windowStyle.Setters.Add(new Setter(System.Windows.Window.BackgroundProperty, darkWindow));
        windowStyle.Setters.Add(new Setter(System.Windows.Window.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Window)] = windowStyle;

        var panel = new TailgrabPanel(serviceRegistryInstance);

        try
        {
            string LayoutRegistryPath = "Software\\DeviousFox\\Tailgrab\\Layout";
            using (var key = Registry.CurrentUser.OpenSubKey(LayoutRegistryPath))
            {
                if (key != null)
                {
                    var width = key.GetValue("WindowWidth");
                    var height = key.GetValue("WindowHeight");

                    if (width != null && height != null)
                    {
                        panel.Width = Convert.ToDouble(width);
                        panel.Height = Convert.ToDouble(height);
                        logger.Debug($"Loaded window size: {panel.Width}x{panel.Height}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to load window size from registry.");
        }


        app.Run(panel);
    }
}


public class BuildInfo
{
    public static string GetAssemblyVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "0.0.0.0";
    }

    public static string GetInformationalVersion()
    {
        return Assembly.GetExecutingAssembly()
                       .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                       ?.InformationalVersion
                       ?? GetAssemblyVersion();
    }
}

public class FileWatchItem
{
    public long StartingSize { get; set; }
    public int ElapsedTime { get; set; }
    public FileWatchItem(long startingSize)
    {
        StartingSize = startingSize;
        ElapsedTime = 0;
    }
}

public class FileTailStatus
{
    public string FilePath { get; }
    public int LinesProcessed { get; private set; }
    public DateTime? LastLineProcessedTime { get; private set; }
    public DateTime StartTime { get; }
    public CancellationTokenSource CancellationSource { get; }

    public FileTailStatus(string filePath)
    {
        FilePath = filePath;
        LinesProcessed = 0;
        LastLineProcessedTime = null;
        StartTime = DateTime.Now;
        CancellationSource = new CancellationTokenSource();
    }

    public void IncrementLinesProcessed()
    {
        LinesProcessed++;
        LastLineProcessedTime = DateTime.Now;
    }

    public void RequestCancellation()
    {
        CancellationSource.Cancel();
    }

    public bool IsCancellationRequested => CancellationSource.Token.IsCancellationRequested;
}