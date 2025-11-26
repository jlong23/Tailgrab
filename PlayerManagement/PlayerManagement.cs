using NLog;
using Tailgrab.LineHandler;

namespace Tailgrab.PlayerManagement
{

    public class PlayerEvent
    {
        public enum EventType
        {
            Join,
            Leave,
            Sticker,
            AvatarChange,
            Moderation
        }

        public DateTime EventTime { get; set; } = DateTime.Now;
        public EventType Type { get; set; }
        public string EventDescription { get; set; }

        public PlayerEvent(EventType type, string eventDescription)
        {
            Type = type;
            EventDescription = eventDescription;
        }
    }

    public class Player
    {
        public string UserId { get; set; }
        public string DisplayName { get; set; }
        public int NetworkId { get; set; }
        public DateTime InstanceStartTime { get; set; }
        public DateTime? InstanceEndTime { get; set; }
        public List<PlayerEvent> Events { get; set; } = new List<PlayerEvent>();

        public Player(string userId, string displayName)
        {
            UserId = userId;
            DisplayName = displayName;
            InstanceStartTime = DateTime.Now;
        }

        public void AddEvent(PlayerEvent playerEvent)
        {
            Events.Add(playerEvent);
        }
    }
    
    public static class PlayerManager
    {
        private static Dictionary<int, Player> playersByNetworkId = new Dictionary<int, Player>();
        private static Dictionary<string, Player> playersByUserId = new Dictionary<string, Player>();
        private static Dictionary<string, Player> playersByDisplayName = new Dictionary<string, Player>();
        private static Dictionary<string, string> avatarByDisplayName = new Dictionary<string, string>();
        public static readonly string COLOR_PREFIX_YELLOW = $"\u001b[33;1m";
        public static readonly string COLOR_PREFIX_GREEN = $"\u001b[32;1m";
        public static readonly string COLOR_RESET = "\u001b[0m";
        public static Logger logger = LogManager.GetCurrentClassLogger();

        public static void PlayerJoined(string userId, string displayName, AbstractLineHandler handler)
        {
            if (!playersByUserId.ContainsKey(userId))
            {
                Player newPlayer = new Player(userId, displayName);
                playersByUserId[userId] = newPlayer;
                playersByDisplayName[displayName] = newPlayer;
                if( handler.LogOutput )
                {
                    logger.Info($"{COLOR_PREFIX_GREEN}Player Joined: {displayName} (ID: {userId}){COLOR_RESET}");
                }

                if( avatarByDisplayName.TryGetValue(displayName, out string? avatarName))
                {
                    AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.AvatarChange, $"Joined with Avatar: {avatarName}");
                    if( handler.LogOutput )
                    {
                        logger.Info($"{COLOR_PREFIX_GREEN}\tAvatar on Join: {avatarName}{COLOR_RESET}");
                    }
                }
            }
        }

        public static void PlayerLeft(string displayName, AbstractLineHandler handler )
        {
            if (playersByDisplayName.TryGetValue(displayName, out Player? player))
            {
                player.InstanceEndTime = DateTime.Now;
                playersByDisplayName.Remove(displayName);
                playersByNetworkId.Remove(player.NetworkId);
                playersByUserId.Remove(player.UserId);
                if( handler.LogOutput )
                {
                    PrintPlayerInfo(player);
                }
            }
        }

        public static Player? GetPlayerByNetworkId(int networkId)
        {
            playersByNetworkId.TryGetValue(networkId, out Player? player);
            return player;
        }

        public static Player? GetPlayerByUserId(string userId)
        {
            playersByUserId.TryGetValue(userId, out Player? player);
            return player;
        }

        public static Player? GetPlayerByDisplayName(string displayName)
        {
            playersByDisplayName.TryGetValue(displayName, out Player? player);
            return player;
        }

        public static Player? AssignPlayerNetworkId(string displayName, int networkId)
        {
            if( playersByDisplayName.TryGetValue(displayName, out Player? player))
            {
                player.NetworkId = networkId;
                playersByNetworkId[networkId] = player;
            }   

            return player;
        }

        public static IEnumerable<Player> GetAllPlayers()
        {
            return playersByUserId.Values;
        }

        public static void ClearAllPlayers(AbstractLineHandler handler)
        {

            foreach( var player in playersByUserId.Values )
            {
                player.InstanceEndTime = DateTime.Now;
                if( handler.LogOutput )
                {
                    PrintPlayerInfo(player);
                }
            }

            playersByNetworkId.Clear();
            playersByUserId.Clear();
            playersByDisplayName.Clear();
        }

        public static int GetPlayerCount()
        {
            return playersByUserId.Count;
        }   

        public static void LogAllPlayers(AbstractLineHandler handler)
        {
            if( handler.LogOutput )
            {
                foreach( var player in playersByUserId.Values )
                {
                    PrintPlayerInfo(player);
                }                
            }
        }

        public static Player? AddPlayerEventByDisplayName(string displayName, PlayerEvent.EventType eventType, string eventDescription)
        {
            if( playersByDisplayName.TryGetValue(displayName, out Player? player))
            {
                PlayerEvent newEvent = new PlayerEvent(eventType, eventDescription);
                player.AddEvent(newEvent);
                return player;
            } 

            return null;
        }

        public static void SetAvatarForPlayer(string displayName, string avatarId)
        {
            avatarByDisplayName[displayName] = avatarId;
        }

        private static void PrintPlayerInfo(Player player)
        {
            logger.Info($"{COLOR_PREFIX_YELLOW}Player Left: {player.DisplayName} (ID: {player.UserId}, NetworkID: {player.NetworkId}){COLOR_RESET}");
            foreach( var ev in player.Events )
            {
                logger.Info($"{COLOR_PREFIX_YELLOW}\tEvent: {ev.EventTime} - {ev.Type} - {ev.EventDescription}{COLOR_RESET}");
            }
        }
    }
}
