namespace Tailgrab.LineHandler;

using System.Text.RegularExpressions;
using Tailgrab.Common;
using Tailgrab.PlayerManagement;

public class PrintHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[API\]\WRequesting\WGet\Wprints/(prnt_[\d\w\W]+)\W\{\{\}\}";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_FILEURL = 3;


    public PrintHandler(string matchPattern, ServiceRegistry serviceRegistry) : base(matchPattern, serviceRegistry)
    {
        logger.Info($"** Print Handler:  Regular Expression: {Pattern}");        
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if( m.Success )
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            string fileURL = m.Groups[VRC_FILEURL].Value;
            _serviceRegistry.GetPlayerManager().AddPrintData( fileURL );
            if ( LogOutput )
            {
                logger.Info($"{COLOR_PREFIX}Print : {fileURL}{COLOR_RESET.GetAnsiEscape()}");
            }
            ExecuteActions();
            return true;
        }
        return false;
    }
}