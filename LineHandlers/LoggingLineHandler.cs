namespace Tailgrab.LineHandler;
public class LoggingLineHandler : AbstractLineHandler
{
    public LoggingLineHandler(string matchPattern) : base(matchPattern)
    {
    }

    public override bool HandleLine(string line)
    {
        if (regex.IsMatch(line))
        {
            if( LogOutput )
            {
                logger.Info($"{COLOR_PREFIX}{line}{COLOR_RESET}");
                //Console.WriteLine(line);                
            }
            return true;
        }
        return false;
    }
}