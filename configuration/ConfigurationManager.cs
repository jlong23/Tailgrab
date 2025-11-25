using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Tailgrab.LineHandler;

namespace Tailgrab.Configuration
{
    public class ConfigurationManager
    {
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

        public static ILineHandler loadLineHandlersFromConfig( LineHandlerConfig lineHandlerConfig )
        {
            List<ILineHandler> handlers = new List<ILineHandler>();

            return handlers.FirstOrDefault() ?? throw new Exception("No line handlers found in configuration.");
        }
    }

    public enum LineHandlerType
    {
        Logging,
        OnPlayerJoin,
        OnPlayerNetwork,
        Sticker,
        Print,
        AvatarChange,
        AvatarUnpack,
        WarnKick,
        PenNetwork,
        VTK
    }


    public class LineHandlerConfig
    {
        public LineHandlerType HandlerType { get; set; }
        public string Pattern { get; set; }
        public List<string> Actions { get; set; }

        public LineHandlerConfig(LineHandlerType handlerType, string pattern, List<string> actions)
        {
            HandlerType = handlerType;
            Pattern = pattern;
            Actions = actions;
        }
    }


    public class ActionConfig
    {
        public string ActionType { get; set; }
        public Dictionary<string, string> Parameters { get; set; }

        public ActionConfig(string actionType, Dictionary<string, string> parameters)
        {
            ActionType = actionType;
            Parameters = parameters;
        }
    }
}