using Microsoft.EntityFrameworkCore;
using NLog;
using System.Text;
using Tailgrab.Common;
using Tailgrab.LineHandler;
using Tailgrab.Models;
using VRChat.API.Model;

namespace Tailgrab.PlayerManagement
{
    public class PlayerEvent
    {
        public enum EventType
        {
            Join,
            Leave,
            Sticker,
            Print,
            PenActivity,
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
        public string AvatarName { get; set; }
        public string PenActivity { get; set; }
        public int NetworkId { get; set; }
        public DateTime InstanceStartTime { get; set; }
        public DateTime? InstanceEndTime { get; set; }
        public List<PlayerEvent> Events { get; set; } = new List<PlayerEvent>();
        public SessionInfo Session { get; set; }
        public string? LastStickerUrl { get; set; } = string.Empty;

        public Dictionary<string, Print> PrintData = new Dictionary<string, Print>();
        public string? UserBio { get; set; }
        public string? AIEval { get; set; }
        public bool IsWatched { get
            { 
                if( IsAvatarWatch || IsGroupWatch || IsProfileWatch)
                {
                    return true;
                }

                return false;
            }
        }

        public string WatchCode
        {
            get 
            { 
                string code = "";

                if( IsAvatarWatch )
                {
                    code += "A";
                }   
                if( IsGroupWatch )
                {
                    code += "G";
                }
                if( IsProfileWatch )
                {
                    code += "P";
                }

                return code;
            }
        }

        public bool IsAvatarWatch { get; set; } = false;
        public bool IsGroupWatch { get; set; } = false;
        public bool IsProfileWatch { get; set; } = false;


        public Player(string userId, string displayName, SessionInfo session )
        {
            UserId = userId;
            DisplayName = displayName;
            AvatarName = "";
            PenActivity = "";
            InstanceStartTime = DateTime.Now;
            Session = session;
        }

        public void AddEvent(PlayerEvent playerEvent)
        {
            Events.Add(playerEvent);
        }

        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool full)
        {
            StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"DisplayName: {DisplayName}");
            sb.AppendLine($"UserId: {UserId}");
            sb.AppendLine($"Current Avatar Name: {(string.IsNullOrEmpty(AvatarName) ? string.Empty : AvatarName)}");
            if (!string.IsNullOrEmpty(LastStickerUrl))
            {
                sb.AppendLine($"Last Sticker: {(string.IsNullOrEmpty(LastStickerUrl) ? string.Empty : LastStickerUrl)}");
            }
            if (!string.IsNullOrEmpty(PenActivity))
            {
                sb.AppendLine($"Last Pen Activity: {(string.IsNullOrEmpty(PenActivity) ? string.Empty : PenActivity)}");
            }
            sb.AppendLine($"InstanceStart: {InstanceStartTime:u}");
            sb.AppendLine($"InstanceEnd: {(InstanceEndTime.HasValue ? InstanceEndTime.Value.ToString("u") : string.Empty)}");
            sb.AppendLine($"WorldId: {Session.WorldId}");
            sb.AppendLine($"InstanceId: {Session.InstanceId}");

            if (PrintData != null && PrintData.Count > 0)
            {
                sb.AppendLine("Events:");
                foreach (var ev in PrintData.Values)
                {
                    sb.AppendLine($"  - {ev.Timestamp:u} {ev.Id} {ev.AuthorName} {ev.OwnerId}");
                }
            }

            if (Events != null && Events.Count > 0)
            {
                sb.AppendLine("Events:");
                foreach (var ev in Events)
                {
                    sb.AppendLine($"  - {ev.EventTime:u} {ev.Type} {ev.EventDescription}");
                }
            }

            if ( full && UserBio != null && UserBio.Length > 0) 
            {
                sb.AppendLine(new string('-', 50));

                sb.AppendLine("Player Profile At Join:");
                sb.AppendLine(UserBio);
            }

            return sb.ToString();
        }
    }

    public class SessionInfo
    {
        public string WorldId { get; set; }
        public string InstanceId { get; set; }
        public SessionInfo(string worldId, string instanceId)
        {
            WorldId = worldId;
            InstanceId = instanceId;
        }
    }

    public class PlayerChangedEventArgs : EventArgs
    {
        public enum ChangeType
        {
            Added,
            Updated,
            Removed,
            Cleared
        }

        public ChangeType Type { get; }
        public Player Player { get; }

        public PlayerChangedEventArgs(ChangeType type, Player player)
        {
            Type = type;
            Player = player;
        }
    }

    public class PlayerManager
    {
        private ServiceRegistry serviceRegistry;

        private static Dictionary<int, Player> playersByNetworkId = new Dictionary<int, Player>();
        private static Dictionary<string, Player> playersByUserId = new Dictionary<string, Player>();
        private static Dictionary<string, Player> playersByDisplayName = new Dictionary<string, Player>();
        private static Dictionary<string, string> avatarByDisplayName = new Dictionary<string, string>();
        private static List<string> avatarsInSession = new List<string>();

        public static readonly AnsiColor COLOR_PREFIX_LEAVE = AnsiColor.Yellow;
        public static readonly AnsiColor COLOR_PREFIX_JOIN = AnsiColor.Green;
        public static readonly AnsiColor COLOR_RESET = AnsiColor.Reset;
        public static Logger logger = LogManager.GetCurrentClassLogger();
        public static SessionInfo CurrentSession = new SessionInfo("", "");

        // Event for UI and other listeners
        public static event EventHandler<PlayerChangedEventArgs>? PlayerChanged;

        public PlayerManager(ServiceRegistry registry)
        {
            serviceRegistry = registry;
        }

        public void OnPlayerChanged(PlayerChangedEventArgs.ChangeType changeType, Player player)
        {
            try
            {
                PlayerChanged?.Invoke(null, new PlayerChangedEventArgs(changeType, player));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error raising PlayerChanged event");
            }
        }

        public void UpdateCurrentSession(string worldId, string instanceId)
        {
            CurrentSession = new SessionInfo(worldId, instanceId);
        }

        public void PlayerJoined(string userId, string displayName, AbstractLineHandler handler)
        {
            Player? player = null;
            PlayerChangedEventArgs.ChangeType changeType = PlayerChangedEventArgs.ChangeType.Added;
            if (!playersByUserId.ContainsKey(userId))
            {
                player = new Player(userId, displayName, CurrentSession);
                if (handler.LogOutput)
                {
                    logger.Info($"{COLOR_PREFIX_JOIN.GetAnsiEscape()}Player Joined: {displayName} (ID: {userId}){COLOR_RESET.GetAnsiEscape()}");
                }

                changeType = PlayerChangedEventArgs.ChangeType.Added;
            }
            else
            {
                // If existing, treat as update (display name may have changed etc.)
                player = playersByUserId[userId];
                if (player.DisplayName != displayName)
                {
                    // remove old display-name mapping if present
                    if (!string.IsNullOrEmpty(player.DisplayName))
                    {
                        playersByDisplayName.Remove(player.DisplayName);
                    }
                    player.DisplayName = displayName;
                }

                changeType = PlayerChangedEventArgs.ChangeType.Added;
            }

            if ( player == null )
            {
                logger.Error("PlayerJoined: Failed to create or retrieve player instance.");
                return;
            }

            if (avatarByDisplayName.TryGetValue(displayName, out string? avatarName))
            {
                if (avatarName != null)
                {
                    player.AvatarName = avatarName;
                    AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.AvatarChange, $"Joined with Avatar: {avatarName}");
                    if (handler.LogOutput)
                    {
                        logger.Info($"{COLOR_PREFIX_JOIN.GetAnsiEscape()}\tAvatar on Join: {avatarName}{COLOR_RESET.GetAnsiEscape()}");
                    }
                }
            }

            serviceRegistry.GetOllamaAPIClient().CheckUserProfile(userId);
            playersByUserId[userId] = player;
            playersByDisplayName[displayName] = player;

            OnPlayerChanged(changeType, player);
        }

        public void PlayerLeft(string displayName, AbstractLineHandler handler )
        {
            if (playersByDisplayName.TryGetValue(displayName, out Player? player))
            {
                player.InstanceEndTime = DateTime.Now;
                TimeSpan timeDifference = (TimeSpan)(player.InstanceEndTime - player.InstanceStartTime);
                logger.Debug($"{displayName} session time: {timeDifference.TotalMinutes} minutes");
                TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
                UserInfo? user = dBContext.UserInfos.Find(player.UserId);
                if (user == null)
                {
                    user = new UserInfo();
                    user.DisplayName = player.DisplayName;
                    user.UserId = player.UserId;
                    user.IsBos = 0;
                    user.CreatedAt = DateTime.Now;
                    user.UpdatedAt = DateTime.Now;
                    user.ElapsedHours = timeDifference.TotalMinutes;
                    dBContext.Add(user);
                    dBContext.SaveChanges();
                }
                else
                {
                    user.DisplayName = player.DisplayName;
                    user.UpdatedAt = DateTime.Now;
                    user.ElapsedHours = user.ElapsedHours + timeDifference.TotalMinutes;
                    dBContext.Update(user);
                    dBContext.SaveChanges();
                }

                // Raise event with updated player before removing from internal dictionaries
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Removed, player);

                playersByDisplayName.Remove(displayName);
                playersByNetworkId.Remove(player.NetworkId);
                playersByUserId.Remove(player.UserId);
                if( handler.LogOutput )
                {
                    PrintPlayerInfo(player);
                }
            }
        }

        public Player? GetPlayerByNetworkId(int networkId)
        {
            playersByNetworkId.TryGetValue(networkId, out Player? player);
            return player;
        }

        public Player? GetPlayerByUserId(string userId)
        {
            playersByUserId.TryGetValue(userId, out Player? player);
            return player;
        }

        public Player? GetPlayerByDisplayName(string displayName)
        {
            playersByDisplayName.TryGetValue(displayName, out Player? player);
            return player;
        }

        public Player? AssignPlayerNetworkId(string displayName, int networkId)
        {
            if( playersByDisplayName.TryGetValue(displayName, out Player? player))
            {
                player.NetworkId = networkId;
                playersByNetworkId[networkId] = player;
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
            }   

            return player;
        }

        public IEnumerable<Player> GetAllPlayers()
        {
            return playersByUserId.Values;
        }

        public void ClearAllPlayers(AbstractLineHandler handler)
        {
            foreach (string avatarName in avatarsInSession)
            {
                serviceRegistry.GetAvatarManager().AddAvatarsInSession(avatarName);
            }

            foreach ( var player in playersByUserId.Values )
            {
                player.InstanceEndTime = DateTime.Now;
                if( handler.LogOutput )
                {
                    PrintPlayerInfo(player);
                }
                // Notify removed for each player
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Removed, player);
            }

            playersByNetworkId.Clear();
            playersByUserId.Clear();
            playersByDisplayName.Clear();
            avatarsInSession.Clear();

            // Also a global cleared notification (consumers may want to reset)
            OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Cleared, new Player("","", CurrentSession) { InstanceStartTime = DateTime.MinValue });
        }

        public int GetPlayerCount()
        {
            return playersByUserId.Count;
        }   

        public void LogAllPlayers(AbstractLineHandler handler)
        {
            if( handler.LogOutput )
            {
                foreach( var player in playersByUserId.Values )
                {
                    PrintPlayerInfo(player);
                }                
            }
        }

        public Player? AddPlayerEventByDisplayName(string displayName, PlayerEvent.EventType eventType, string eventDescription)
        {
            if( playersByDisplayName.TryGetValue(displayName, out Player? player))
            {
                PlayerEvent newEvent = new PlayerEvent(eventType, eventDescription);
                player.AddEvent(newEvent);
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
                return player;
            } 

            return null;
        }

        public Player? AddPlayerEventByUserId(string displayName, PlayerEvent.EventType eventType, string eventDescription)
        {
            if (playersByUserId.TryGetValue(displayName, out Player? player))
            {
                PlayerEvent newEvent = new PlayerEvent(eventType, eventDescription);
                player.AddEvent(newEvent);
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
                return player;
            }

            return null;
        }

        public void SetAvatarForPlayer(string displayName, string avatarName)
        {
            bool watchedAvatar = serviceRegistry.GetAvatarManager().CheckAvatarByName(avatarName);
            if( watchedAvatar )
            {
                logger.Info($"{COLOR_PREFIX_LEAVE.GetAnsiEscape()}Watched Avatar Detected for Player {displayName}: {avatarName}{COLOR_RESET.GetAnsiEscape()}");
            }

            avatarByDisplayName[displayName] = avatarName;
            if (playersByDisplayName.TryGetValue(displayName, out var p))
            {
                p.IsAvatarWatch = watchedAvatar;
                p.AvatarName = avatarName;
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, p);
            }

            if( !avatarsInSession.Contains(avatarName))
            {
                avatarsInSession.Add(avatarName);
            }

        }

        private void PrintPlayerInfo(Player player)
        {
            logger.Info($"{COLOR_PREFIX_LEAVE.GetAnsiEscape()}Player Left: \n{player.ToString()}{COLOR_RESET.GetAnsiEscape()}");
        }

        internal void AddPenEventByDisplayName(string displayName, string eventText)
        {
            if (playersByDisplayName.TryGetValue(displayName, out Player? player))
            {
                player.PenActivity = eventText;
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
            }
        }

        internal void AddInventorySpawn(string inventoryId)
        {

        }

        internal void AddStickerEvent(string displayName, string userId, string fileURL)
        {
            if (playersByDisplayName.TryGetValue(displayName, out Player? player))
            {
                player.LastStickerUrl = fileURL;
                AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.Sticker, $"Spawned sticker: {fileURL}");
            }
        }

        internal void CompactDatabase()
        {
            serviceRegistry.GetAvatarManager().CompactDatabase();
        }

        internal void AddPrintData(string printId)
        {
            if (serviceRegistry.GetVRChatAPIClient() != null)
            {
                Print? printInfo = serviceRegistry.GetVRChatAPIClient().GetPrintInfo(printId);
                if (printInfo != null)
                {
                    if( playersByUserId.TryGetValue(printInfo.OwnerId, out Player? player))
                    {
                        player.PrintData[printId] = printInfo;
                        logger.Info($"Added Print {printId} for Player {player.DisplayName} (ID: {printInfo.OwnerId})" );
                    }
                    AddPlayerEventByUserId(printInfo.OwnerId, PlayerEvent.EventType.Print, $"Dropped Print {printId}");
                }
            }
        }
    }
}
