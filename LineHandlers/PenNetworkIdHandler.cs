namespace Tailgrab.LineHandler;

using System.Text;
using System.Text.RegularExpressions;
using Tailgrab.PlayerManagement;
using NLog;

public class PenNetworkHandler : AbstractLineHandler
{

    public static readonly string LOG_PATTERN = @"([\d]{4}.[\d]{2}.[\d]{2}\W[\d]{2}:[\d]{2}:[\d]{2})\W(Log[\W]{8}|Debug[\W]{6})-\W\W\[NetworkProcessing\] Received ownership transfer of ([\d]+) from ([\d]+) to ([\d]+)";
    public static readonly int VRC_DATETIME = 1;
    public static readonly int VRC_LOGTYPE = 2;
    public static readonly int VRC_OBJECT_NETWORK_ID = 3;
    public static readonly int VRC_FROM_NETWORK_ID = 4;
    public static readonly int VRC_TO_NETWORK_ID = 5;

    private static Dictionary<int, string> penNetworkMap = new Dictionary<int, string>();

    public PenNetworkHandler(string matchPattern) : base(matchPattern)
    {
        using (FileStream fs = new FileStream("./pen-network-id.csv", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
        {
            Console.WriteLine($"Loading Pen Network ID mappings...");                
            while (true && sr != null)
            {
                string? line = sr.ReadLine();
                if (line == null)
                {
                    break;
                }

                string[] parts = line.Split(',');
                int networkId = int.Parse(parts[0]);
                string penColor = parts[1]; 
                penNetworkMap[networkId] = penColor;
                logger.Debug($"Mapped Pen Color '{penColor}' to Network ID {networkId}");
            }   
        }
    }

    public override bool HandleLine(string line)
    {
        Match m = regex.Match(line);
        if( m.Success )
        {
            string timestamp = m.Groups[VRC_DATETIME].Value;
            int objectId = int.Parse( m.Groups[VRC_OBJECT_NETWORK_ID].Value );
            int fromUserId = int.Parse( m.Groups[VRC_FROM_NETWORK_ID].Value );
            int toUserId = int.Parse( m.Groups[VRC_TO_NETWORK_ID].Value );

            if (penNetworkMap.TryGetValue(objectId, out string? penColor))
            {
                string fromPlayerName = "Unknown";
                string toPlayerName = "Unknown";
                if( PlayerManager.GetPlayerByNetworkId(fromUserId) is Player fromPlayer )
                {
                    fromPlayerName = fromPlayer.DisplayName;
                    PlayerManager.AddPlayerEventByDisplayName(fromPlayer.DisplayName, PlayerEvent.EventType.Moderation, $"Lost ownership of pen '{penColor}'.");
                }
                if( PlayerManager.GetPlayerByNetworkId(toUserId) is Player toPlayer )
                {
                    toPlayerName = toPlayer.DisplayName;
                    PlayerManager.AddPlayerEventByDisplayName(toPlayer.DisplayName, PlayerEvent.EventType.Moderation, $"Took ownership of pen '{penColor}'.");
                }
                if( LogOutput )
                {
                    logger.Info($"Pen '{penColor}' Ownership Change : {fromPlayerName} to {toPlayerName}");
                }
            }

            ExecuteActions();
            return true;
        }
        return false;
    }
}