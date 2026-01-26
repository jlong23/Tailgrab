using Tailgrab.Common;

namespace Tailgrab.LineHandler;
public class LoggingLineHandler : AbstractLineHandler
{
    public LoggingLineHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
    }

    public override bool HandleLine(string line)
    {
        if (regex.IsMatch(line))
        {
            if( LogOutput )
            {
                logger.Info($"{COLOR_PREFIX}{line}{COLOR_RESET.GetAnsiEscape()}");
            }
            return true;
        }
        return false;
    }
}