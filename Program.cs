using System.Reflection;
using System.Text;
using Tailgrab.LineHandler;
using NLog;
using Tailgrab.Configuration;

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

        Console.WriteLine($"Tailing file: {filePath}. Press Ctrl+C to stop.");

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
    /// </summary>
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        logger.Info($"Tailgrab Version: {BuildInfo.GetInformationalVersion()}");
        
        string filePath = VRChatAppDataPath + Path.DirectorySeparatorChar;
        if (args.Length == 0)
        {
            logger.Warn("No path argument provided, defaulting to VRChat log directory: {filePath}");
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

        ConfigurationManager.LoadLineHandlersFromConfig(HandlerList);
        logger.Info($"Watching for log changes from: '{filePath}'");
        await WatchPath(filePath);
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