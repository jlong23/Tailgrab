using Tailgrab.Actions;
using System.Text.RegularExpressions;
using NLog;

namespace Tailgrab.LineHandler
{
    public interface ILineHandler
    {
        void AddAction(IAction action);
       
        bool HandleLine(string line);

        void LogOutputColor( string color );
    }


    public abstract class AbstractLineHandler: ILineHandler
    {
        protected string Pattern { get; }
        protected Regex regex;
        public List<IAction> Actions = new List<IAction>();
        public bool LogOutput { get; set; } = true;
        public string LogOutputColor { get; set; } = "37m"; // Default to white
        public string COLOR_PREFIX => $"\u001b[{LogOutputColor}";
        public static readonly string COLOR_RESET = "\u001b[0m";
        public static Logger logger = LogManager.GetCurrentClassLogger();


        protected AbstractLineHandler(string matchPattern)
        {
            Pattern = matchPattern;
            regex = new Regex(Pattern);
        }   

        public abstract bool HandleLine(string line);

        public void AddAction(IAction action)
        {
            Actions.Add(action);
        }

        protected void ExecuteActions()
        {
            foreach (var action in Actions)
            {
                action.PerformAction();
            }
        }

        void ILineHandler.LogOutputColor(string color)
        {
            LogOutputColor = color;
        }
    }
    
}
