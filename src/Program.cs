using Microsoft.EntityFrameworkCore;
using NLog;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Tailgrab;
using Tailgrab.Configuration;
using Tailgrab.LineHandler;
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
    /// Show the TailgrabPannel UI on the STA thread before continuing to watch files.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {

        // Basic command line parsing:
        // -l <FilePath>    : use explicit log folder/file path
        // -clear           : remove application registry settings and exit
        string? explicitPath = null;
        bool clearRegistry = false;
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
        }

        if (clearRegistry)
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

            // Exit application after clearing settings
            return;
        }

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

        _serviceRegistry = new ServiceRegistry();
        _serviceRegistry.StartAllServices();

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

        if (!Directory.Exists(filePath))
        {
            logger.Info($"Missing VRChat log directory at '{filePath}'");
            return;
        }

        ConfigurationManager configurationManager = new ConfigurationManager(_serviceRegistry);
        configurationManager.LoadLineHandlersFromConfig(HandlerList);

        // Start the watcher task on a background thread so it doesn't block the STA UI thread
        logger.Info($"Starting file watcher and showing UI for: '{filePath}'");
        _ = Task.Run(() => WatchPath(filePath));

        // Start the Amplitude Cache watcher task on a background thread
        string ampPath = VRChatAmplitudePath + Path.DirectorySeparatorChar;
        logger.Info($"Starting Amplitude Cache watcher for: '{ampPath}'");
        _ = Task.Run(() => WatchAmpCache(ampPath, _serviceRegistry));

        BuildAppWindow(_serviceRegistry);

        // When the window closes, allow Main to complete. The watcher task will be abandoned; if desired add cancellation.
    }

    private static void BuildAppWindow(ServiceRegistry serviceRegistryInstance)
    {
        // Start WPF application and show the TailgrabPannel on this STA thread
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

        var panel = new TailgrabPannel(serviceRegistryInstance);
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