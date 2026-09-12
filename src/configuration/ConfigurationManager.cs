using BuildSoft.VRChat.Osc;
using NLog;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tailgrab.Actions;
using Tailgrab.Common;
using Tailgrab.LineHandler;

namespace Tailgrab.Configuration
{
    public class ConfigurationManager
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly ServiceRegistry _serviceRegistry;

        public ConfigurationManager(ServiceRegistry serviceRegistry)
        {
            if (serviceRegistry == null)
            {
                throw new ArgumentNullException(nameof(serviceRegistry), "ServiceRegistry cannot be null");
            }

            _serviceRegistry = serviceRegistry;
        }

        public static string GetConfigFilePath()
        {
            return Path.Combine(CommonConst.APPLICATION_LOCAL_DATA_PATH, "config.json");
        }

        public (bool isValid, string? errorMessage) ValidateRegexPattern(string? pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return (true, null);
            }

            try
            {
                _ = new Regex(pattern);
                return (true, null);
            }
            catch (ArgumentException ex)
            {
                return (false, $"Invalid regex pattern: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Regex validation error: {ex.Message}");
            }
        }

        public static (bool success, string? errorMessage) SaveConfig(List<LineHandlerConfig> handlers)
        {
            try
            {
                // Resolve path: prefer explicit, then local repo config.json, then user config path
                string path = (File.Exists("config.json") ? Path.GetFullPath("config.json") : GetConfigFilePath());

                // Ensure directory exists
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var config = new TailgrabConfig { LineHandlers = handlers };

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                options.Converters.Add(new JsonStringEnumConverter());

                string jsonString = JsonSerializer.Serialize(config, options);
                File.WriteAllText(path, jsonString);

                logger.Info($"Configuration saved successfully to '{path}'");
                return (true, null);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save configuration");
                return (false, $"Failed to save configuration: {ex.Message}");
            }
        }

        public List<LineHandlerConfig> LoadConfig()
        {
            // Resolve path: prefer explicit, then local repo config.json, then user config path
            string path = (File.Exists("config.json") ? Path.GetFullPath("config.json") : GetConfigFilePath());

            if (!File.Exists(path))
            {
                logger.Warn($"Configuration file not found at '{path}'. Returning empty configuration list.");
                return new List<LineHandlerConfig>();
            }

            try
            {
                string jsonString = File.ReadAllText(path);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                // ensure enums and polymorphic derived types deserialize from strings
                options.Converters.Add(new JsonStringEnumConverter());

                TailgrabConfig? config = JsonSerializer.Deserialize<TailgrabConfig>(jsonString, options);

                ValidateConfiguration(config);

                if (config?.LineHandlers != null)
                {
                    logger.Info($"Configuration loaded successfully from '{path}'");

                    return config.LineHandlers;
                }

                logger.Warn("Configuration file parsed but no 'lineHandlers' section found. Returning empty list.");
                return new List<LineHandlerConfig>();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to read or parse configuration file '{path}'.");
                System.Windows.MessageBox.Show($"Error loading configuration file: {ex.Message}; Correct config.json and restart application", "Configuration Load Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return new List<LineHandlerConfig>();
            }
        }

        private static void ValidateConfiguration(TailgrabConfig? config)
        {
            bool _isDirty = false;
            // Validate and ensure exactly one instance of each LineHandlerType
            if (config?.LineHandlers != null)
            {
                var typeCounts = new Dictionary<LineHandlerType, int>();
                var validHandlers = new List<LineHandlerConfig>();

                // Count occurrences of each type
                foreach (LineHandlerConfig handlerConfig in config.LineHandlers)
                {
                    if (typeCounts.ContainsKey(handlerConfig.HandlerTypeValue))
                    {
                        typeCounts[handlerConfig.HandlerTypeValue]++;
                        logger.Warn($"Duplicate LineHandlerType '{handlerConfig.HandlerTypeValue}' found in configuration. Keeping first occurrence.");
                        _isDirty = true;
                    }
                    else
                    {
                        typeCounts[handlerConfig.HandlerTypeValue] = 1;
                        validHandlers.Add(handlerConfig);
                    }
                }

                // Add missing handler types with defaults
                foreach (LineHandlerType handlerType in Enum.GetValues(typeof(LineHandlerType)))
                {
                    if (!typeCounts.ContainsKey(handlerType))
                    {
                        logger.Warn($"LineHandlerType '{handlerType}' not found in configuration. Adding default handler.");

                        LineHandlerConfig handler = new LineHandlerConfig()
                        {
                            Enabled = true,
                            HandlerTypeValue = handlerType,
                            PatternTypeValue = PatternType.Default,
                            Pattern = null,
                            LogOutput = true,
                            LogOutputColor = "0m",
                            Actions = new List<ActionBase>()
                        };

                        // Disable Logging handler by default to prevent excessive console output
                        if ( handlerType == LineHandlerType.Logging)
                        {
                            handler.Enabled = false;
                        }

                        validHandlers.Add(handler);
                        _isDirty = true;
                    }
                }

                config.LineHandlers = validHandlers;

                if( _isDirty ) 
                {
                    SaveConfig(config.LineHandlers);
                }
            }
        }

        public List<ILineHandler> LoadLineHandlersFromConfig(List<ILineHandler> handlers)
        {

            List<LineHandlerConfig> configs = LoadConfig();

            foreach (var configItem in configs)
            {
                if (configItem == null)
                {
                    continue;
                }

                if (configItem.Enabled == false)
                {
                    logger.Warn($"Line handler of type {configItem.HandlerTypeValue} is disabled in configuration, skipping this handler.");
                    continue;
                }

                string? pattern = null;
                if (configItem.PatternTypeValue == PatternType.Default || configItem.Pattern == null)
                {
                    pattern = null;
                }
                else
                {
                    pattern = configItem.Pattern;
                }


                AnsiColor logOutputColor = AnsiColor.White;
                if (configItem.LogOutputColor == "Default" || configItem.LogOutputColor == null)
                {
                    logOutputColor = AnsiColor.White;
                }
                else
                {
                    if (Enum.TryParse<AnsiColor>(configItem.LogOutputColor, true, out var parsedColor))
                    {
                        logOutputColor = parsedColor;
                    }
                    else
                    {
                        logger.Warn($"Invalid LogOutputColor '{configItem.LogOutputColor}' specified. Defaulting to White.");
                        logOutputColor = AnsiColor.White;
                    }
                }

                //
                // Build the appropriate line handler based on the type
                AbstractLineHandler? handler = CreateLineHandlerFromType(configItem.HandlerTypeValue);
                if (handler != null)
                {
                    if (pattern != null)
                    {
                        handler.Pattern = pattern;
                    }
                    handler.LogOutputColor = logOutputColor!;
                    handler.LogOutput = configItem.LogOutput;
                    handler.Actions = ParseActionsFromConfig(configItem.Actions);
                    handlers.Add(handler);
                }
            }

            return handlers;
        }

        private AbstractLineHandler? CreateLineHandlerFromType(LineHandlerType handlerType)
        {
            AbstractLineHandler? handler = null;
            switch (handlerType)
            {
                case LineHandlerType.AvatarChange:
                    handler = new AvatarChangeHandler(AvatarChangeHandler.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.AvatarUnpack:
                    handler = new AvatarUnpackHandler(AvatarUnpackHandler.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.Emoji:
                    handler = new EmojiHandler(EmojiHandler.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.Logging:
                    handler = new LoggingLineHandler("", _serviceRegistry);
                    break;
                case LineHandlerType.OnPlayerJoin:
                    handler = new OnPlayerJoinHandler(OnPlayerJoinHandler.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.OnPlayerNetwork:
                    handler = new OnPlayerNetworkHandler(OnPlayerNetworkHandler.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.PenNetwork:
                    handler = new PenNetworkHandler(PenNetworkHandler.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.Print:
                    handler = new PrintHandler(PrintHandler.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.Quit:
                    handler = new QuitHandler(QuitHandler.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.Sticker:
                    handler = new StickerHandler(StickerHandler.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.UserSelected:
                    handler = new QuickMenuSelectedUser(QuickMenuSelectedUser.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.VTK:
                    handler = new VTKHandler(VTKHandler.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.WarnKick:
                    handler = new WarnKickHandler(WarnKickHandler.LOG_PATTERN, _serviceRegistry);
                    break;
                case LineHandlerType.WorldChange:
                    handler = new WorldChangeHandler(WorldChangeHandler.LOG_PATTERN, _serviceRegistry);
                    break;
            }

            return handler;
        }


        private List<IAction> ParseActionsFromConfig(List<ActionBase> actionConfigs)
        {
            List<IAction> actions = new List<IAction>();
            foreach (var actionConfig in actionConfigs)
            {
                if (actionConfig.GetType() == typeof(OSCActionConfig))
                {
                    var oscActionConfig = (OSCActionConfig)actionConfig;
                    if (oscActionConfig.ParameterName == null)
                    {
                        logger.Warn("OSC Action configuration is missing required field; 'parameterName', skipping this action.");
                        continue;
                    }
                    if (oscActionConfig.Value == null)
                    {
                        logger.Warn("OSC Action configuration is missing required field; 'value', skipping this action.");
                        continue;
                    }

                    actions.Add(new OSCAction(oscActionConfig.ParameterName, oscActionConfig.OscValueType, oscActionConfig.Value));
                }

                if (actionConfig.GetType() == typeof(DelayActionConfig))
                {
                    var delayActionConfig = (DelayActionConfig)actionConfig;
                    actions.Add(new DelayAction(delayActionConfig.Milliseconds));
                }

                if (actionConfig.GetType() == typeof(PlaySoundActionConfig))
                {
                    var playSoundActionConfig = (PlaySoundActionConfig)actionConfig;
                    if (playSoundActionConfig.SoundFile == null)
                    {
                        logger.Warn("PlaySound Action configuration is missing required field; 'soundFile', skipping this action.");
                        continue;
                    }
                    actions.Add(new PlaySoundAction(playSoundActionConfig.SoundFile));
                }

                if (actionConfig.GetType() == typeof(KeyStrokeConfig))
                {
                    var keyStrokeConfig = (KeyStrokeConfig)actionConfig;
                    if (keyStrokeConfig.WindowTitle == null)
                    {
                        logger.Warn("Keystrokes Action configuration is missing required field; 'windowTitle', skipping this action.");
                        continue;
                    }
                    if (keyStrokeConfig.Keys == null)
                    {
                        logger.Warn("Keystrokes Action configuration is missing required field; 'keys', skipping this action.");
                        continue;
                    }

                    actions.Add(new KeystrokesAction(keyStrokeConfig.WindowTitle, keyStrokeConfig.Keys));
                }
                
                if (actionConfig.GetType() == typeof(TTSActionConfig))
                {
                    var ttsActionConfig = (TTSActionConfig)actionConfig;
                    if (ttsActionConfig.Text == null)
                    {
                        logger.Warn("TTS Action configuration is missing required field; 'text', skipping this action.");
                        continue;
                    }

                    TTSAction ttsAcction = new TTSAction(ttsActionConfig.Text, ttsActionConfig.Volume, ttsActionConfig.Rate);
                    ttsAcction.LocalSvcRegistry = _serviceRegistry;

                    actions.Add(ttsAcction);
                }

            }

            return actions;
        }
    }

    public class TailgrabConfig
    {
        public List<LineHandlerConfig> LineHandlers { get; set; } = new List<LineHandlerConfig>();
    }


    public enum LineHandlerType
    {
        AvatarChange,
        AvatarUnpack,
        Emoji,
        Logging,
        OnPlayerJoin,
        OnPlayerNetwork,
        PenNetwork,
        Print,
        Sticker,
        VTK,
        WarnKick,
        UserSelected,
        WorldChange,
        Quit
    }

    public static class LineHandlerTypeExtensions
    {
        public static string GetDescription(this LineHandlerType handlerType)
        {
            return handlerType switch
            {
                LineHandlerType.AvatarChange => "Avatar Change Handler",
                LineHandlerType.AvatarUnpack => "Avatar Unpack Handler",
                LineHandlerType.Emoji => "Emoji Handler",
                LineHandlerType.Logging => "Logging Handler",
                LineHandlerType.OnPlayerJoin => "On Player Join Handler",
                LineHandlerType.OnPlayerNetwork => "On Player Network Handler",
                LineHandlerType.PenNetwork => "Pen Network Handler",
                LineHandlerType.Print => "Print Handler",
                LineHandlerType.Sticker => "Sticker Handler",
                LineHandlerType.VTK => "VTK Handler",
                LineHandlerType.WarnKick => "Warn Kick Handler",
                LineHandlerType.UserSelected => "User Selected Handler",
                LineHandlerType.WorldChange => "World Change Handler",
                LineHandlerType.Quit => "Quit Handler",
                _ => "Unknown Handler",
            };
        }
    }

    public enum PatternType
    {
        Default,
        Override
    }


    public class LineHandlerConfig
    {
        public LineHandlerType HandlerTypeValue { get; set; }
        public bool Enabled { get; set; } = true;
        public PatternType PatternTypeValue { get; set; }
        public string? Pattern { get; set; }
        public bool LogOutput { get; set; } = true;
        public string LogOutputColor { get; set; } = "0m";
        public List<ActionBase> Actions { get; set; } = new List<ActionBase>();
    }


    public enum ActionType
    {
        OSCAction,
        DelayAction,
        KeyPressAction,
        TTSAction,
        PlaySoundAction,
        HTTPGetAction,
        HTTPPostAction,

    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "actionTypeValue")]
    [JsonDerivedType(typeof(OSCActionConfig), "OSCAction")]
    [JsonDerivedType(typeof(DelayActionConfig), "DelayAction")]
    [JsonDerivedType(typeof(KeyStrokeConfig), "KeyPressAction")]
    [JsonDerivedType(typeof(TTSActionConfig), "TTSAction")]
    [JsonDerivedType(typeof(PlaySoundActionConfig), "PlaySoundAction")]
    [JsonDerivedType(typeof(HTTPGetActionConfig), "HTTPGetAction")]
    [JsonDerivedType(typeof(HTTPPostActionConfig), "HTTPPostAction")]
    public abstract class ActionBase
    {
        public ActionType ActionTypeValue { get; set; }
    }


    public class OSCActionConfig : ActionBase
    {
        public string? ParameterName { get; set; }
        public OscType OscValueType { get; set; } = OscType.Float;
        public string? Value { get; set; }

        public OSCActionConfig()
        {
            ActionTypeValue = ActionType.OSCAction;
        }
    }

    public class DelayActionConfig : ActionBase
    {
        public int Milliseconds { get; set; } = 1000;
        public int DelayMilliseconds { get; internal set; }

        public DelayActionConfig()
        {
            ActionTypeValue = ActionType.DelayAction;
        }
    }

    public class PlaySoundActionConfig : ActionBase
    {
        public string? SoundFile { get; set; }

        public PlaySoundActionConfig()
        {
            ActionTypeValue = ActionType.PlaySoundAction;
        }
    }

    public class KeyStrokeConfig : ActionBase
    {
        public string? WindowTitle { get; set; }
        public string? Keys { get; set; }

        public KeyStrokeConfig()
        {
            ActionTypeValue = ActionType.KeyPressAction;
        }
    }

    public class TTSActionConfig : ActionBase
    {
        public string? Text { get; set; }
        public int Volume { get; set; } = 100;
        public int Rate { get; set; } = 0;

        public TTSActionConfig()
        {
            ActionTypeValue = ActionType.TTSAction;
        }
    }

    public class HTTPGetActionConfig : ActionBase
    {
        public string? Url { get; set; }

        public string? cookies { get; set; } = string.Empty;
        public int timeout { get; set; } = 10000;

        public HTTPGetActionConfig()
        {
            ActionTypeValue = ActionType.HTTPGetAction;
        }
    }

    public class HTTPPostActionConfig : ActionBase
    {
        public string? Url { get; set; }
        public string? cookies { get; set; } = string.Empty;
        public string? contentType { get; set; } = "application/json";
        public string payload { get; set; } = string.Empty;
        public int timeout { get; set; } = 10000;

        public HTTPPostActionConfig()
        {
            ActionTypeValue = ActionType.HTTPPostAction;
        }
    }
}