using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using NLog;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Tailgrab;
using Tailgrab.Clients.VRChat;
using Tailgrab.Configuration;
using Tailgrab.LineHandler;
using Tailgrab.Models;
using Tailgrab.PlayerManagement;


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


    /// <summary>
    /// Threaded tailing of a file, reading new lines as they are added.
    /// </summary>
    public static async Task TailFileAsync(string filePath)
    {
        if (OpenedFiles.Contains(filePath))
        {
            return;
        }
        OpenedFiles.Add(filePath);

        logger.Info($"Tailing file: {filePath}. Press Ctrl+C to stop.");

        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
        {
            // Start at the end of the file
            long lastMaxOffset = fs.Length;
            fs.Seek(lastMaxOffset, SeekOrigin.Begin);

            while (true)
            {
                // If the file size hasn't changed, wait
                if (fs.Length == lastMaxOffset)
                {
                    await Task.Delay(100); // Adjust delay as needed
                    continue;
                }

                // Read and display new lines
                string? line;
                while ((line = await sr.ReadLineAsync()) != null)
                {
                    foreach (ILineHandler handler in HandlerList)
                    {
                        if (handler.HandleLine(line))
                        {
                            break;
                        }
                    }
                }

                // Update the offset to the new end of the file
                lastMaxOffset = fs.Length;
            }
        }
    }

    /// <summary>
    /// Watch and dispatch File Tailing.
    /// </summary>
    public static async Task WatchPath(string path)
    {
        LogWatcher.Path = Path.GetDirectoryName(path)!;
        LogWatcher.Filter = Path.GetFileName("output_log*.txt");
        LogWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;

        LogWatcher.Created += async (source, e) =>
        {
            await TailFileAsync(e.FullPath);
        };

        LogWatcher.Changed += async (source, e) =>
        {
            await TailFileAsync(e.FullPath);
        };

        LogWatcher.EnableRaisingEvents = true;

        // ensure existing files are tailed immediately
        try
        {
            string _todaysFile = $"output_log_{DateTime.Now:yyyy_MM_dd}*.txt";
            var existing = Directory.GetFiles(LogWatcher.Path, _todaysFile);
            foreach (var f in existing)
            {
                _ = Task.Run(() => TailFileAsync(f));
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

                            ServiceRegistryInstance.GetAvatarManager().CacheAvatars(avatarIds);
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

    /// <summary>
    /// Watch the VRChat log directory by default and process logs.
    /// Show the TailgrabPanel UI on the STA thread before continuing to watch files.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {

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

        CreateResourceDirectory();

        _serviceRegistry = new ServiceRegistry();
        _serviceRegistry.StartAllServices();

        if ( upgrade )
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

        AvatarBosGistListManager avatarGistMgr = new AvatarBosGistListManager(_serviceRegistry);
        _ = Task.Run(() => avatarGistMgr.ProcessAvatarGistList());

        GroupBosGistListManager groupGistMgr = new GroupBosGistListManager(_serviceRegistry);
        _ = Task.Run(() => groupGistMgr.ProcessGroupGistList());

        ConfigurationManager configurationManager = new ConfigurationManager(_serviceRegistry);
        configurationManager.LoadLineHandlersFromConfig(HandlerList);

        // Start the watcher task on a background thread so it doesn't block the STA UI thread
        logger.Info($"Starting file watcher and showing UI for: '{filePath}'");
        _ = Task.Run(() => WatchPath(filePath));

        // Start the Amplitude Cache watcher task on a background thread
        string ampPath = VRChatAmplitudePath + Path.DirectorySeparatorChar;
        logger.Info($"Starting Amplitude Cache watcher for: '{ampPath}'");
        _ = Task.Run(() => WatchAmpCache(ampPath, _serviceRegistry));

        //SyncAvatarModerations(_serviceRegistry);

        BuildAppWindow(_serviceRegistry);

        // When the window closes, allow Main to complete. The watcher task will be abandoned; if desired add cancellation.
    }

    private static void UpgradeApplication(ServiceRegistry serviceRegistry)
    {
        logger.Warn($"Starting application upgrade process...");
        
        // Migrate database schema while preserving data
        serviceRegistry.GetDBContext().Database.Migrate();
        
        // Create missing registry items with default values
        InitializeMissingRegistryItems();

        logger.Warn($"Completed application upgrade process...");
    }

    /// <summary>
    /// Initialize missing registry items with their default values.
    /// This ensures all required configuration keys exist without overwriting existing values.
    /// </summary>
    private static void InitializeMissingRegistryItems()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(Tailgrab.Common.Common.ConfigRegistryPath);
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
            SetDefaultIfMissing(Tailgrab.Common.Common.Registry_Ollama_API_Endpoint, 
                Tailgrab.Common.Common.Default_Ollama_API_Endpoint);
            SetDefaultIfMissing(Tailgrab.Common.Common.Registry_Ollama_API_Model, 
                Tailgrab.Common.Common.Default_Ollama_API_Model);
            SetDefaultIfMissing(Tailgrab.Common.Common.Registry_Ollama_API_Prompt, 
                Tailgrab.Common.Common.Default_Ollama_API_Prompt);
            SetDefaultIfMissing(Tailgrab.Common.Common.Registry_Ollama_API_Image_Prompt, 
                Tailgrab.Common.Common.Default_Ollama_API_Image_Prompt);

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

    private static void SyncAvatarModerations(ServiceRegistry serviceRegistry)
    {
        try
        {
            TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
            VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();
            if( dBContext != null && vrcClient != null )
            {
                List<VRChat.API.Model.AvatarModeration> moderations = vrcClient.GetAvatarModerations();
                foreach (VRChat.API.Model.AvatarModeration mod in moderations)
                {
                    AvatarInfo? existing = dBContext.AvatarInfos.FirstOrDefault(a => a.AvatarId == mod.TargetAvatarId);
                    if (existing != null)
                    {
                        if (existing.IsBos)
                        {
                            logger.Debug($"Avatar {existing.AvatarId} is already marked as BOS in the database. Skipping update.");
                            continue; // already marked as BOS, no update needed
                        }

                        logger.Debug($"Marking Avatar {existing.AvatarId} is as BOS in the database.");
                        existing.IsBos = true;
                        existing.UpdatedAt = mod.Created;
                        dBContext.SaveChanges();

                    }
                    else
                    {
                        dBContext.AvatarInfos.Add(new AvatarInfo
                        {
                            AvatarName = "From Moderation API",
                            AvatarId = mod.TargetAvatarId,
                            IsBos = true,
                            CreatedAt = mod.Created,
                            UpdatedAt = mod.Created
                        });
                        dBContext.SaveChanges();

                        logger.Debug($"Adding missing Avatar {mod.TargetAvatarId} is as BOS in the database.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to clear the database");
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
            var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
            var databasePath = Path.Combine(dataDir, "avatars.sqlite");

            if (!File.Exists(databasePath))
            {
                logger.Warn($"Database file not found at '{databasePath}'. Nothing to backup.");
                return;
            }

            // Create backup directory with timestamp
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupDirName = $"backup_{timestamp}";
            var backupDir = Path.Combine(dataDir, backupDirName);
            Directory.CreateDirectory(backupDir);

            logger.Info($"Creating database backup in: '{backupDirName}'");

            // Get database context
            if (_serviceRegistry == null)
            {
                logger.Error("Service registry not initialized. Cannot create backup.");
                return;
            }

            var context = _serviceRegistry.GetDBContext();

            // Export each table to JSON
            int totalRecords = 0;

            // Export AvatarInfo table
            logger.Info("Exporting AvatarInfo table...");
            var avatars = context.AvatarInfos.ToList();
            var avatarsJson = JsonSerializer.Serialize(avatars, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(backupDir, "AvatarInfo.json"), avatarsJson);
            logger.Info($"  Exported {avatars.Count} avatar records");
            totalRecords += avatars.Count;

            // Export GroupInfo table
            logger.Info("Exporting GroupInfo table...");
            var groups = context.GroupInfos.ToList();
            var groupsJson = JsonSerializer.Serialize(groups, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(backupDir, "GroupInfo.json"), groupsJson);
            logger.Info($"  Exported {groups.Count} group records");
            totalRecords += groups.Count;

            // Export UserInfo table
            logger.Info("Exporting UserInfo table...");
            var users = context.UserInfos.ToList();
            var usersJson = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(backupDir, "UserInfo.json"), usersJson);
            logger.Info($"  Exported {users.Count} user records");
            totalRecords += users.Count;

            // Export ProfileEvaluation table
            logger.Info("Exporting ProfileEvaluation table...");
            var profiles = context.ProfileEvaluations.ToList();
            var profilesJson = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(backupDir, "ProfileEvaluation.json"), profilesJson);
            logger.Info($"  Exported {profiles.Count} profile evaluation records");
            totalRecords += profiles.Count;

            // Export ImageEvaluation table
            logger.Info("Exporting ImageEvaluation table...");
            var images = context.ImageEvaluations.ToList();
            var imagesJson = JsonSerializer.Serialize(images, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(backupDir, "ImageEvaluation.json"), imagesJson);
            logger.Info($"  Exported {images.Count} image evaluation records");
            totalRecords += images.Count;

            // Create backup metadata file
            var metadata = new
            {
                BackupTimestamp = timestamp,
                BackupDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ApplicationVersion = BuildInfo.GetInformationalVersion(),
                DatabasePath = databasePath,
                TotalRecords = totalRecords,
                Tables = new[]
                {
                    new { TableName = "AvatarInfo", RecordCount = avatars.Count },
                    new { TableName = "GroupInfo", RecordCount = groups.Count },
                    new { TableName = "UserInfo", RecordCount = users.Count },
                    new { TableName = "ProfileEvaluation", RecordCount = profiles.Count },
                    new { TableName = "ImageEvaluation", RecordCount = images.Count }
                }
            };
            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(backupDir, "_backup_metadata.json"), metadataJson);

            // Calculate total backup size
            var backupDirInfo = new DirectoryInfo(backupDir);
            var totalSize = backupDirInfo.GetFiles().Sum(f => f.Length);

            logger.Info($"Database backup completed successfully:");
            logger.Info($"  Location: '{backupDir}'");
            logger.Info($"  Total records: {totalRecords}");
            logger.Info($"  Backup size: {totalSize / 1024.0:F2} KB");

            // Clean up old backups (keep only last 10)
            CleanupOldBackups(dataDir, 10);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to create database backup");
        }
    }

    /// <summary>
    /// Clean up old backup directories, keeping only the specified number of most recent backups.
    /// </summary>
    private static void CleanupOldBackups(string dataDir, int keepCount)
    {
        try
        {
            var backupDirs = Directory.GetDirectories(dataDir, "backup_*")
                .Select(d => new DirectoryInfo(d))
                .OrderByDescending(d => d.CreationTime)
                .ToList();

            if (backupDirs.Count > keepCount)
            {
                var dirsToDelete = backupDirs.Skip(keepCount);
                foreach (var dir in dirsToDelete)
                {
                    logger.Info($"Deleting old backup directory: '{dir.Name}'");
                    dir.Delete(recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warn(ex, "Failed to clean up old backup directories");
        }
    }

    private static void CreateResourceDirectory()
    {
        // Ensure Resources/tailgrab.ico is present in the application folder. If missing, write a small embedded PNG as the icon file.
        try
        {
            var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(dataDir);

            var resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");
            Directory.CreateDirectory(resourcesDir);

            var iconPath = Path.Combine(resourcesDir, "tailgrab.ico");
            if (!File.Exists(iconPath))
            {
                // A tiny 1x1 PNG (transparent). We'll write it to the .ico path so WPF can load it as an ImageSource.
                var base64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAAWgmWQ0AAAAASUVORK5CYII=";
                var bytes = Convert.FromBase64String(base64Png);
                File.WriteAllBytes(iconPath, bytes);
                logger.Info($"Wrote placeholder icon to: {iconPath}");
            }
        }
        catch (Exception ex)
        {
            logger.Warn(ex, "Failed to ensure Resources/tailgrab.ico exists");
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
        var accent = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));

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
        selectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 63))));
        selectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        listViewItemStyle.Triggers.Add(selectedTrigger);

        var mouseOverTrigger = new Trigger { Property = System.Windows.Controls.ListViewItem.IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 53))));
        listViewItemStyle.Triggers.Add(mouseOverTrigger);

        app.Resources[typeof(System.Windows.Controls.ListViewItem)] = listViewItemStyle;

        // GridViewColumnHeader style for list headers
        var headerStyle = new Style(typeof(System.Windows.Controls.GridViewColumnHeader));
        headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));
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
        cellSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 63))));
        cellSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        dgCellStyle.Triggers.Add(cellSelectedTrigger);
        app.Resources[typeof(System.Windows.Controls.DataGridCell)] = dgCellStyle;

        var dgRowStyle = new Style(typeof(System.Windows.Controls.DataGridRow));
        dgRowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        dgRowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        var rowSelectedTrigger = new Trigger { Property = System.Windows.Controls.DataGridRow.IsSelectedProperty, Value = true };
        rowSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 63))));
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
        app.Resources[typeof(System.Windows.Controls.TabControl)] = tabControlStyle;

        var groupBoxStyle = new Style(typeof(System.Windows.Controls.GroupBox));
        groupBoxStyle.Setters.Add(new Setter(System.Windows.Window.BackgroundProperty, darkWindow));
        groupBoxStyle.Setters.Add(new Setter(System.Windows.Window.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Controls.GroupBox)] = groupBoxStyle;

        var comboBoxStyle = new Style(typeof(System.Windows.Controls.ComboBox));
        comboBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        comboBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Controls.ComboBox)] = comboBoxStyle;

        // Ensure ComboBox items and selected text use the dark theme when the control is not focused.
        var comboBoxItemStyle = new Style(typeof(System.Windows.Controls.ComboBoxItem));
        comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, System.Windows.Media.Brushes.Transparent));

        var comboSelectedTrigger = new Trigger { Property = System.Windows.Controls.ComboBoxItem.IsSelectedProperty, Value = true };
        comboSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        comboSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));

        comboBoxItemStyle.Triggers.Add(comboSelectedTrigger);
        app.Resources[typeof(System.Windows.Controls.ComboBoxItem)] = comboBoxItemStyle;

        var windowStyle = new Style(typeof(System.Windows.Window));
        windowStyle.Setters.Add(new Setter(System.Windows.Window.BackgroundProperty, darkWindow));
        windowStyle.Setters.Add(new Setter(System.Windows.Window.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Window)] = windowStyle;

        var panel = new TailgrabPanel(serviceRegistryInstance);
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