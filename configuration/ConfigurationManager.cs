using System.IO;
using Tailgrab.Actions;
using Tailgrab.LineHandler;
using BuildSoft.VRChat.Osc;
using NLog;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tailgrab.Configuration
{
    public class ConfigurationManager
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();

        public static string GetConfigFilePath()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string configDirectory = Path.Combine(userProfile, ".tailgrab");
            if (!Directory.Exists(configDirectory))
            {
                Directory.CreateDirectory(configDirectory);
            }

            return Path.Combine(configDirectory, "config.json");
        }

        public static List<LineHandlerConfig> LoadConfig(string? configFilePath = null)
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

        public static List<ILineHandler> LoadLineHandlersFromConfig(List<ILineHandler> handlers)
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


                string? logOutputColor = null;
                if (configItem.LogOutputColor == "Default" || configItem.LogOutputColor == null)
                {
                    logOutputColor = "37m";
                }
                else
                {
                    logOutputColor = configItem.LogOutputColor;
                }

                //
                // Build the appropriate line handler based on the type
                AbstractLineHandler? handler = null;
                switch (configItem.HandlerTypeValue)
                {
                    case LineHandlerType.AvatarChange:
                        if (pattern == null)
                        {
                            pattern = AvatarChangeHandler.LOG_PATTERN;
                        }
                        handler = new AvatarChangeHandler(pattern);
                        break;

                    case LineHandlerType.AvatarUnpack:
                        if (pattern == null)
                        {
                            pattern = AvatarUnpackHandler.LOG_PATTERN;
                        }
                        handler = new AvatarUnpackHandler(pattern);
                        break;

                    case LineHandlerType.Logging:
                        if (pattern == null)
                        {
                            pattern = "";
                        }
                        handler = new LoggingLineHandler(pattern);
                        break;

                    case LineHandlerType.OnPlayerJoin:
                        if (pattern == null)
                        {
                            pattern = OnPlayerJoinHandler.LOG_PATTERN;
                        }
                        handler = new OnPlayerJoinHandler(pattern);
                        break;

                    case LineHandlerType.OnPlayerNetwork:
                        if (pattern == null)
                        {
                            pattern = OnPlayerNetworkHandler.LOG_PATTERN;
                        }
                        handler = new OnPlayerNetworkHandler(pattern);
                        break;

                    case LineHandlerType.PenNetwork:
                        if (pattern == null)
                        {
                            pattern = PenNetworkHandler.LOG_PATTERN;
                        }
                        handler = new PenNetworkHandler(pattern);
                        break;

                    case LineHandlerType.Print:
                        if (pattern == null)
                        {
                            pattern = PrintHandler.LOG_PATTERN;
                        }
                        handler = new PrintHandler(pattern);
                        break;

                    case LineHandlerType.Quit:
                        if (pattern == null)
                        {
                            pattern = QuitHandler.LOG_PATTERN;
                        }
                        handler = new QuitHandler(pattern);
                        break;

                    case LineHandlerType.Sticker:
                        if (pattern == null)
                        {
                            pattern = StickerHandler.LOG_PATTERN;
                        }
                        handler = new StickerHandler(pattern);
                        break;

                    case LineHandlerType.VTK:
                        if (pattern == null)
                        {
                            pattern = VTKHandler.LOG_PATTERN;
                        }
                        handler = new VTKHandler(pattern);
                        break;

                    case LineHandlerType.WarnKick:
                        if (pattern == null)
                        {
                            pattern = WarnKickHandler.LOG_PATTERN;
                        }
                        handler = new WarnKickHandler(pattern);
                        break;

                    default:
                        logger.Warn($"Unsupported line handler type in configuration: {configItem.HandlerTypeValue}, skipping this handler.");
                        continue;
                }

                if (handler != null)
                {
                    handler.LogOutputColor = logOutputColor!;
                    handler.LogOutput = configItem.LogOutput;
                    handler.Actions = ParseActionsFromConfig(configItem.Actions);
                    handlers.Add(handler);
                }
            }

            return handlers;
        }

        private static List<IAction> ParseActionsFromConfig(List<ActionBase> actionConfigs)
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
        Quit
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
        KeyPressAction
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "actionTypeValue")]
    [JsonDerivedType(typeof(OSCActionConfig), "OSCAction")]
    [JsonDerivedType(typeof(DelayActionConfig), "DelayAction")]
    [JsonDerivedType(typeof(KeyStrokeConfig), "KeyPressAction")]
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
}