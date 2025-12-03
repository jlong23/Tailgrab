using NLog;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Tailgrab.Configuration;
using Tailgrab.LineHandler;
using Tailgrab.PlayerManagement;

public class FileTailer
{
    /// <summary>
    /// A list of the Regex Line Matchers that are used to process log lines.
    /// </summary>
    static List<ILineHandler> HandlerList = new List<ILineHandler>{};

    /// <summary>
    /// A List of opened file paths to avoid opening the same file multiple times.
    /// </summary>
    static List<string> OpenedFiles = new List<string>{};

    /// <summary>
    /// The path to the user's profile directory.
    /// </summary>
    internal static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// The path to the VRChat AppData directory.
    /// </summary>
    public static readonly string VRChatAppDataPath = Path.Combine(UserProfile, @"AppData", "LocalLow", "VRChat", "VRChat");

    static FileSystemWatcher Watcher = new FileSystemWatcher();

    public static Logger logger = LogManager.GetCurrentClassLogger();


    /// <summary>
    /// Threaded tailing of a file, reading new lines as they are added.
    /// </summary>
    public static async Task TailFileAsync(string filePath)
    {
        if( OpenedFiles.Contains(filePath) )
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
    public static async Task WatchPath( string path )
    {
        Watcher.Path = Path.GetDirectoryName(path)!;
        Watcher.Filter = Path.GetFileName("output_log*.txt");
        Watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

        Watcher.Created += async (source, e) =>
        {
            await TailFileAsync(e.FullPath);
        };

        Watcher.Changed += async (source, e) =>
        {
            await TailFileAsync(e.FullPath);
        };

        Watcher.EnableRaisingEvents = true;

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
        Console.OutputEncoding = Encoding.UTF8;

        logger.Info($"Tailgrab Version: {BuildInfo.GetInformationalVersion()}");
        
        string filePath = VRChatAppDataPath + Path.DirectorySeparatorChar;
        if (args.Length == 0)
        {
            logger.Warn($"No path argument provided, defaulting to VRChat log directory: {filePath}");
        } 
        else
        {
            filePath = args[0];   
        }
        
        if (!Directory.Exists(filePath))
        {
            logger.Info($"Missing VRChat log directory at '{filePath}'");
            return;
        }

        Process[] processlist = Process.GetProcesses();

        foreach (Process process in processlist)
        {
            if (!String.IsNullOrEmpty(process.MainWindowTitle))
            {
                logger.Info($"Process: {process.ProcessName} ID: {process.Id} Window title: {process.MainWindowTitle}");
            }
        }

        ConfigurationManager.LoadLineHandlersFromConfig(HandlerList);
        logger.Info($"Starting file watcher and showing UI for: '{filePath}'");

        // Ensure Resources/tailgrab.ico is present in the application folder. If missing, write a small embedded PNG as the icon file.
        try
        {
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

        // Start the watcher task on a background thread so it doesn't block the STA UI thread
        _ = Task.Run(() => WatchPath(filePath));

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

        var windowStyle = new Style(typeof(System.Windows.Window));
        windowStyle.Setters.Add(new Setter(System.Windows.Window.BackgroundProperty, darkWindow));
        windowStyle.Setters.Add(new Setter(System.Windows.Window.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Window)] = windowStyle;

        var panel = new TailgrabPannel();
        app.Run(panel);

        // When the window closes, allow Main to complete. The watcher task will be abandoned; if desired add cancellation.
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