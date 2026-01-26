using BuildSoft.VRChat.Osc;
using NLog;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        public string GetConfigFilePath()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string configDirectory = Path.Combine(userProfile, ".tailgrab");
            if (!Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            return Path.Combine(configDirectory, "config.json");
        }

        public List<LineHandlerConfig> LoadConfig(string? configFilePath = null)
        {
            logger.Debug("** Loading Configuration file");

            // Resolve path: prefer explicit, then local repo config.json, then user config path
            string path = configFilePath
                ?? (File.Exists("config.json") ? Path.GetFullPath("config.json") : GetConfigFilePath());

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
                if (config?.LineHandlers != null)
                {
                    return config.LineHandlers;
                }

                logger.Warn("Configuration file parsed but no 'lineHandlers' section found. Returning empty list.");
                return new List<LineHandlerConfig>();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to read or parse configuration file '{path}'.");
                return new List<LineHandlerConfig>();
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
                AbstractLineHandler? handler = null;
                switch (configItem.HandlerTypeValue)
                {
                    case LineHandlerType.AvatarChange:
                        handler = new AvatarChangeHandler(AvatarChangeHandler.LOG_PATTERN, _serviceRegistry);
                        break;

                    case LineHandlerType.AvatarUnpack:
                        handler = new AvatarUnpackHandler(AvatarUnpackHandler.LOG_PATTERN, _serviceRegistry);
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
        Logging,
        OnPlayerJoin,
        OnPlayerNetwork,
        PenNetwork,
        Print,
        Sticker,
        VTK,
        WarnKick,
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
                LineHandlerType.Logging => "Logging Handler",
                LineHandlerType.OnPlayerJoin => "On Player Join Handler",
                LineHandlerType.OnPlayerNetwork => "On Player Network Handler",
                LineHandlerType.PenNetwork => "Pen Network Handler",
                LineHandlerType.Print => "Print Handler",
                LineHandlerType.Sticker => "Sticker Handler",
                LineHandlerType.VTK => "VTK Handler",
                LineHandlerType.WarnKick => "Warn Kick Handler",
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
        TTSAction
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "actionTypeValue")]
    [JsonDerivedType(typeof(OSCActionConfig), "OSCAction")]
    [JsonDerivedType(typeof(DelayActionConfig), "DelayAction")]
    [JsonDerivedType(typeof(KeyStrokeConfig), "KeyPressAction")]
    [JsonDerivedType(typeof(TTSActionConfig), "TTSAction")]
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
}