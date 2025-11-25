using System.Reflection;
using System.Text;
using BuildSoft.VRChat.Osc;
using Tailgrab.Actions;
using Tailgrab.LineHandler;
using NLog;

public class FileTailer
{
    /// <summary>
    /// A list of the Regex Line Matchers that are used to process log lines.
    /// </summary>
    static List<ILineHandler> handlers = new List<ILineHandler>{};

    /// <summary>
    /// A List of opened file paths to avoid opening the same file multiple times.
    /// </summary>
    static List<string> openedFiles = new List<string>{};

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
    /// Load and Initialize all the Line Handlers from the regular-expressions.txt file.
    /// </summary>
    public static void InitializeMatchsets()
    {
        using (FileStream fs = new FileStream("./regular-expressions.txt", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
        {
            while (true && sr != null)
            {
                string? line = sr.ReadLine();
                if (line == null)
                {
                    break;
                }

                handlers.Add( new LoggingLineHandler(line));
            }   

            handlers.Add( new OnPlayerJoinHandler(OnPlayerJoinHandler.LOG_PATTERN) );
            handlers.Add( new OnPlayerNetworkHandler(OnPlayerNetworkHandler.LOG_PATTERN) );
            handlers.Add( new StickerHandler(StickerHandler.LOG_PATTERN) );
            handlers.Add( new PrintHandler(PrintHandler.LOG_PATTERN) );
            handlers.Add( new AvatarChangeHandler(AvatarChangeHandler.LOG_PATTERN) );
            handlers.Add( new AvatarUnpackHandler(AvatarUnpackHandler.LOG_PATTERN) );
            handlers.Add( new WarnKickHandler(WarnKickHandler.LOG_PATTERN) );
            handlers.Add( new PenNetworkHandler(PenNetworkHandler.LOG_PATTERN) );
            handlers.Add( new QuitHandler(QuitHandler.LOG_PATTERN) );

            //handlers.Add( new VTKHandler(VTKHandler.LOG_PATTERN) );
            ILineHandler handler = new VTKHandler(VTKHandler.LOG_PATTERN);
            handler.AddAction( new OSCAction("/avatar/parameters/Ear/Right_Angle", OscType.Float, "20.0" ));
            handler.AddAction( new DelayAction(500) );
            handler.AddAction( new OSCAction("/avatar/parameters/Ear/Right_Angle", OscType.Float, "0.0" ));
            handler.LogColor("31;1m"); // Bright Red
            handlers.Add( handler );

        }
    }

    /// <summary>
    /// Threaded tailing of a file, reading new lines as they are added.
    /// </summary>
    public static async Task TailFileAsync(string filePath)
    {
        if( openedFiles.Contains(filePath) )
        {
            return;
        }
        openedFiles.Add(filePath);

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
                    foreach (ILineHandler handler in handlers)
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
        
        string filePath = VRChatAppDataPath + @"\\";
        if (args.Length == 0)
        {
            logger.Warn("No path argument provided, defaulting to VRChat log directory.");
            Console.WriteLine("Usage: dotnet run <filePath>");
            Console.WriteLine($"Running without arguments will watch the VRChat log directory at '{filePath}'");
        } else
        {
            filePath = args[0];   
        }
        
        if (!Directory.Exists(filePath))
        {
            logger.Info($"WatchZing VRChat log directory at '{filePath}'");
            return;
        }

        InitializeMatchsets();
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