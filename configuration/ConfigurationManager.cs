using Tailgrab.Actions;
using Tailgrab.LineHandler;
using BuildSoft.VRChat.Osc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using NLog;

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

        public static List<ILineHandler> LoadLineHandlersFromConfig( List<ILineHandler> handlers )
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build();

            var myConfigurations = new List<LineHandlerConfig>();
            configuration.GetSection("lineHandlers").Bind(myConfigurations);

            foreach (var configItem in myConfigurations)
            {
                if (configItem == null)
                {
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

                List<IAction> actions = ParseActionsFromConfig(configItem.Actions);


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
                handler.Actions = actions;
                handlers.Add(handler);
            }

            return handlers;
        }

        private static List<IAction> ParseActionsFromConfig(List<ActionBase> actionConfigs)
        {
            List<IAction> actions = new List<IAction>();
            foreach (var actionConfig in actionConfigs)
            {
                if (actionConfig == null)
                {
                    continue;
                }
                if (actionConfig.ActionTypeValue == ActionType.OSCAction)
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
                    continue;
                }

                if (actionConfig.ActionTypeValue == ActionType.DelayAction)
                {
                    var delayActionConfig = (DelayActionConfig)actionConfig;
                    actions.Add(new DelayAction(delayActionConfig.Milliseconds));
                    continue;
                }
            }

            return actions;
        }
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
}