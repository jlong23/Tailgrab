using NLog;
using System.IO;
using System.Text;
using Tailgrab.Common;

namespace Tailgrab.LineHandler
{
    public class FileTailer :IDisposable
    {
        private static readonly Logger _Logger = LogManager.GetCurrentClassLogger();

        private Task? _logWatchTask;

        /// <summary>
        /// A list of the Regex Line Matchers that are used to process log lines.
        /// </summary>
        private static List<ILineHandler> _iLineHandlerList = new List<ILineHandler> { };

        /// <summary>
        /// A List of opened file paths to avoid opening the same file multiple times.
        /// </summary>
        private static List<string> _openedFiles = new List<string> { };
        private static Dictionary<string, FileWatchItem> _watchedFiles = new Dictionary<string, FileWatchItem>();
        private static Dictionary<string, FileTailStatus> _activeTailTasks = new Dictionary<string, FileTailStatus>();

        /// <summary>
        /// Returns a read-only dictionary of the currently active tail tasks, mapping file paths to their corresponding FileTailStatus.
        /// </summary>
        public IReadOnlyDictionary<string, FileTailStatus> GetActiveTailTasks() => _activeTailTasks;

        private static FileSystemWatcher _logWatcher = new FileSystemWatcher();

        private readonly ServiceRegistry _serviceRegistry;

        public FileTailer( ServiceRegistry serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public void Start(string? explicitPath = null)
        {
            string filePath = GetLogsPath(explicitPath);
            if (!Directory.Exists(filePath))
            {
                _Logger.Info($"Missing VRChat log directory at '{filePath}'");
                return;
            }
            _serviceRegistry.GetConfigurationManager().LoadLineHandlersFromConfig(_iLineHandlerList);

            // Start the watcher task on a background thread so it doesn't block the STA UI thread
            _Logger.Info($"Starting VR Chat log watcher for: '{filePath}'");
            _logWatchTask = Task.Run(() => WatchPath(filePath, _serviceRegistry));
        }

        public void Dispose()
        {
            foreach ( var item in _activeTailTasks.Values)
            {
                item.RequestCancellation();
            }
        }

        private static string GetLogsPath(string? explicitPath)
        {
            string[] args = Environment.GetCommandLineArgs();
            string filePath = explicitPath ?? (CommonConst.VRCHAT_APP_LOG_PATH + Path.DirectorySeparatorChar);
            if (explicitPath == null)
            {
                if (args.Length == 0)
                {
                    _Logger.Warn($"No path argument provided, defaulting to VRChat log directory: {filePath}");
                }
                else
                {
                    _Logger.Warn($"No '-l' argument provided; ignoring other command line arguments and defaulting to VRChat log directory: {filePath}");
                }
            }
            else
            {
                _Logger.Info($"Using explicit path from -l: '{filePath}'");
            }

            return filePath;
        }

        /// <summary>
        /// Watch and dispatch File Tailing.
        /// </summary>
        public async Task WatchPath(string sourcePath, ServiceRegistry _serviceRegistry)
        {
            _logWatcher.Path = Path.GetDirectoryName(sourcePath)!;
            _logWatcher.Filter = Path.GetFileName("output_log*.txt");
            _logWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;

            _logWatcher.Created += async (source, e) =>
            {
                var status = TailFileAsync(e.FullPath);
                if (status != null)
                {
                    _activeTailTasks[e.FullPath] = status;
                }
            };

            _logWatcher.Changed += async (source, e) =>
            {
                var status = TailFileAsync(e.FullPath);
                if (status != null)
                {
                    _activeTailTasks[e.FullPath] = status;
                }
            };

            _logWatcher.EnableRaisingEvents = true;

            // ensure existing files are tailed immediately
            try
            {
                string _todaysFile = $"output_log_{DateTime.Now:yyyy-MM-dd}_*.txt";
                _Logger.Debug($"Looking for existing log files matching: {_todaysFile}");
                var existing = Directory.GetFiles(_logWatcher.Path, _todaysFile);
                foreach (var f in existing)
                {
                    _Logger.Debug($"Found File to Tail: {f}");
                    _ = Task.Run(() =>
                    {
                        var status = TailFileAsync(f);
                        if (status != null)
                        {
                            _activeTailTasks[f] = status;
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


        /// <summary>
        /// Threaded tailing of a file, reading new lines as they are added.
        /// Returns the FileTailStatus immediately and processes the file in the background.
        /// </summary>
        public static FileTailStatus? TailFileAsync(string sourceFile)
        {
            if (_openedFiles.Contains(sourceFile))
            {
                return null;
            }

            var status = new FileTailStatus(sourceFile);
            _Logger.Info($"Tailing file: {sourceFile}");

            _openedFiles.Add(sourceFile);

            // Start the file tailing process in the background
            _ = Task.Run(async () =>
            {
                try
                {
                    using (FileStream fs = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        // Start at the end of the file
                        long lastMaxOffset = fs.Length;
                        fs.Seek(lastMaxOffset, SeekOrigin.Begin);

                        _watchedFiles[sourceFile] = new FileWatchItem(lastMaxOffset);

                        while (!status.IsCancellationRequested)
                        {
                            // If the file size hasn't changed, wait
                            if (fs.Length == lastMaxOffset)
                            {
                                if (_watchedFiles.ContainsKey(sourceFile))
                                {
                                    _watchedFiles[sourceFile].ElapsedTime += 1;
                                    if (_watchedFiles[sourceFile].ElapsedTime >= 9000) // If we've been watching this file for 15 minutes without changes
                                    {
                                        _Logger.Info($"Timeout waiting for new lines in '{sourceFile}'");
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

                                //
                                // Dispatch the line to all registered handlers, stop on match
                                foreach (ILineHandler handler in _iLineHandlerList)
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
                            _watchedFiles.Remove(sourceFile);
                        }
                    }

                    _Logger.Info($"Stopped tailing file: {sourceFile}. Total lines processed: {status.LinesProcessed}");
                }
                catch (OperationCanceledException)
                {
                    _Logger.Info($"Tailing cancelled for: {sourceFile}. Total lines processed: {status.LinesProcessed}");
                }
                catch (Exception ex)
                {
                    _Logger.Error(ex, $"Error tailing file: {sourceFile}");
                }
                finally
                {
                    // Clean up
                    _openedFiles.Remove(sourceFile);
                    _watchedFiles.Remove(sourceFile);
                    _activeTailTasks.Remove(sourceFile);
                }
            });

            return status;
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
}