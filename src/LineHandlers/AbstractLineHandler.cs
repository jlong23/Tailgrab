using NLog;
using System.Text.RegularExpressions;
using Tailgrab.Actions;
using Tailgrab.Common;

namespace Tailgrab.LineHandler
{
    public interface ILineHandler
    {
        void AddAction(IAction action);
       
        bool HandleLine(string line);

        void LogOutputColor(AnsiColor color );
    }


    public abstract class AbstractLineHandler: ILineHandler
    {
        private string _Pattern;

        protected ServiceRegistry _serviceRegistry;


        public virtual string Pattern
        {
            get { 
                return _Pattern; 
            }
            set {
                _Pattern = value;
                regex = new Regex(_Pattern);
            } 
        }

        public virtual ServiceRegistry ServiceRegistry
        {
            get { 
                return _serviceRegistry; 
            }
            set { 
                if( value == null)
                {
                    throw new ArgumentNullException("ServiceRegistry cannot be null");
                }
                _serviceRegistry = value; 
            }
        }

        protected Regex regex;
        public List<IAction> Actions = new List<IAction>();
        public bool LogOutput { get; set; } = true;
        public AnsiColor LogOutputColor { get; set; } = AnsiColor.White; // Default to white
        public string COLOR_PREFIX => LogOutputColor.GetAnsiEscape();
        public static readonly AnsiColor COLOR_RESET = AnsiColor.Reset;
        public static Logger logger = LogManager.GetCurrentClassLogger();


        protected AbstractLineHandler(string matchPattern, ServiceRegistry serviceRegistry)
        {
            _Pattern = matchPattern;
            regex = new Regex(_Pattern);
            _serviceRegistry = serviceRegistry ?? throw new ArgumentNullException("ServiceRegistry cannot be null");
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

        void ILineHandler.LogOutputColor(AnsiColor color)
        {
            LogOutputColor = color;
        }
    }
    
}
