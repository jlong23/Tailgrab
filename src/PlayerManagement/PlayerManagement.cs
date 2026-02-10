using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.ApplicationServices;
using NLog;
using System.Text;
using System.Windows;
using Tailgrab.Clients.VRChat;
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
            Moderation,
            GroupWatch,
            ProfileWatch,
            AvatarWatch,
            Emoji
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

    public class PlayerInventory
    {
        public string InventoryId { get; set; }
        public string ItemName { get; set; }
        public string ItemUrl { get; set; }
        public string InventoryType { get; set; }
        public string AIEvaluation { get; set; }
        public DateTime SpawnedAt { get; set; }
        public PlayerInventory(string inventoryId, string itemName, string itemUrl, string inventoryType, string aIEvaluation)
        {
            InventoryId = inventoryId;
            ItemName = itemName;
            SpawnedAt = DateTime.Now;
            ItemUrl = itemUrl;
            InventoryType = inventoryType;
            AIEvaluation = aIEvaluation;
        }
    }

    public class PlayerPrint
    {
        public string PrintId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string PrintUrl { get; set; }
        public string AIEvaluation { get; set; }
        public string AuthorName { get; set; }

        public PlayerPrint(VRChat.API.Model.Print p, string aiEvaluation)
        {
            PrintId = p.Id;
            CreatedAt = p.CreatedAt;
            PrintUrl = p.Files.Image;
            AuthorName = p.AuthorName;
            AIEvaluation = aiEvaluation;
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
        public List<PlayerInventory> Inventory { get; set; } = new List<PlayerInventory>();
        public SessionInfo Session { get; set; }
        public string? LastStickerUrl { get; set; } = string.Empty;

        public Dictionary<string, PlayerPrint> PrintData = new Dictionary<string, PlayerPrint>();
        public string? UserBio { get; set; }
        public string? AIEval { get; set; }
        public bool IsWatched
        {
            get
            {
                if (IsAvatarWatch || IsGroupWatch || IsProfileWatch || IsEmojiWatch || IsPrintWatch)
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

                if (IsAvatarWatch)
                {
                    code += "A";
                }
                if (IsGroupWatch)
                {
                    code += "G";
                }
                if (IsProfileWatch)
                {
                    code += "B";
                }
                if (IsEmojiWatch)
                {
                    code += "E";
                }
                if (IsPrintWatch)
                {
                    code += "B";
                }

                return code;
            }
        }

        public bool IsAvatarWatch { get; set; } = false;
        public bool IsGroupWatch { get; set; } = false;
        public bool IsProfileWatch { get; set; } = false;
        public bool IsEmojiWatch { get; set; } = false;
        public bool IsPrintWatch { get; set; } = false;


        public Player(string userId, string displayName, SessionInfo session)
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
                    sb.AppendLine($"  - {ev.CreatedAt:u} {ev.PrintId} {ev.AuthorName} {ev.AIEvaluation}");
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

            if (full && UserBio != null && UserBio.Length > 0)
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
        public DateTime startDateTime { get; } = DateTime.Now;
        public SessionInfo(string worldId, string instanceId)
        {
            WorldId = worldId;
            InstanceId = instanceId;
            startDateTime = DateTime.Now;
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

        private static Dictionary<string, Player> playersByUserId = new Dictionary<string, Player>();
        private static Dictionary<int, string> userIdByNetworkId = new Dictionary<int, string>();
        private static Dictionary<string, string> userIdByDisplayName = new Dictionary<string, string>();
        private static Dictionary<string, string> avatarByDisplayName = new Dictionary<string, string>();

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

        public Player? GetPlayerByDisplayName(string displayName)
        {
            if (userIdByDisplayName.TryGetValue(displayName, out string? userId))
            {
                return GetPlayerByUserId(userId);
            }
            return null;
        }

        public Player? GetPlayerByNetworkId(int networkId)
        {
            if (userIdByNetworkId.TryGetValue(networkId, out string? userId))
            {
                return GetPlayerByUserId(userId);
            }
            return null;
        }

        public Player? GetPlayerByUserId(string userId)
        {
            playersByUserId.TryGetValue(userId, out Player? player);
            return player;
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

        public void OnPlayerChanged(PlayerChangedEventArgs.ChangeType changeType, string displayName)
        {
            try
            {
                Player? player = GetPlayerByDisplayName(displayName);
                if (player != null )
                {
                    PlayerChanged?.Invoke(null, new PlayerChangedEventArgs(changeType, player));
                }
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
            if (!playersByUserId.ContainsKey(userId))
            {
                player = new Player(userId, displayName, CurrentSession);
                if (handler.LogOutput)
                {
                    logger.Info($"{COLOR_PREFIX_JOIN.GetAnsiEscape()}Player Joined: {displayName} (ID: {userId}){COLOR_RESET.GetAnsiEscape()}");
                }
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
                        userIdByDisplayName.Remove(player.DisplayName);
                    }
                    player.DisplayName = displayName;
                }
            }

            if (player == null)
            {
                logger.Error("PlayerJoined: Failed to create or retrieve player instance.");
                return;
            }

            // Check for existing avatar mapping
            if (avatarByDisplayName.TryGetValue(displayName, out string? avatarName))
            {
                if (avatarName != null)
                {
                    player.AvatarName = avatarName;
                    player.Events.Add(new PlayerEvent(PlayerEvent.EventType.AvatarChange, $"Joined with Avatar: {avatarName}"));
                    if (handler.LogOutput)
                    {
                        logger.Info($"{COLOR_PREFIX_JOIN.GetAnsiEscape()}\tAvatar on Join: {avatarName}{COLOR_RESET.GetAnsiEscape()}");
                    }
                }
            }

            serviceRegistry.GetOllamaAPIClient().CheckUserProfile(userId);
            playersByUserId[userId] = player;
            userIdByDisplayName[displayName] = player.UserId;

            OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Added, player);
        }

        public void PlayerLeft(string displayName, AbstractLineHandler handler)
        {
            Player? player = GetPlayerByDisplayName(displayName);
            if (player != null)
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
                    user.ElapsedMinutes = timeDifference.TotalMinutes;
                    dBContext.Add(user);
                    dBContext.SaveChanges();
                }
                else
                {
                    user.DisplayName = player.DisplayName;
                    user.UpdatedAt = DateTime.Now;
                    user.ElapsedMinutes = user.ElapsedMinutes + timeDifference.TotalMinutes;
                    dBContext.Update(user);
                    dBContext.SaveChanges();
                }

                // Raise event with updated player before removing from internal dictionaries
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Removed, player);

                userIdByDisplayName.Remove(displayName);
                avatarByDisplayName.Remove(displayName);
                userIdByNetworkId.Remove(player.NetworkId);
                playersByUserId.Remove(player.UserId);
                if (handler.LogOutput)
                {
                    PrintPlayerInfo(player);
                }
            }
        }

        public Player? AssignPlayerNetworkId(string displayName, int networkId)
        {
            Player? player = GetPlayerByDisplayName(displayName);
            if (player != null)
            {
                player.NetworkId = networkId;
                userIdByNetworkId[networkId] = player.UserId;
            }

            return player;
        }

        public IEnumerable<Player> GetAllPlayers()
        {
            return playersByUserId.Values;
        }

        public void ClearAllPlayers(AbstractLineHandler handler)
        {
            foreach (var player in playersByUserId.Values)
            {
                player.InstanceEndTime = DateTime.Now;
                if (handler.LogOutput)
                {
                    PrintPlayerInfo(player);
                }
                // Notify removed for each player
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Removed, player);
            }

            userIdByNetworkId.Clear();
            playersByUserId.Clear();
            userIdByDisplayName.Clear();

            // Also a global cleared notification (consumers may want to reset)
            OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Cleared, new Player("", "", CurrentSession) { InstanceStartTime = DateTime.MinValue });
        }

        public int GetPlayerCount()
        {
            return playersByUserId.Count;
        }

        public void LogAllPlayers(AbstractLineHandler handler)
        {
            if (handler.LogOutput)
            {
                foreach (var player in playersByUserId.Values)
                {
                    PrintPlayerInfo(player);
                }
            }
        }

        public Player? AddPlayerEventByDisplayName(string displayName, PlayerEvent.EventType eventType, string eventDescription)
        {

            if(userIdByDisplayName.TryGetValue(displayName, out string? userId))
            {
                return AddPlayerEventByUserId(userId, eventType, eventDescription);
            }

            return null;
        }

        public Player? AddPlayerEventByUserId(string userId, PlayerEvent.EventType eventType, string eventDescription)
        {
            if (playersByUserId.TryGetValue(userId, out Player? player))
            {
                PlayerEvent newEvent = new PlayerEvent(eventType, eventDescription);
                player.AddEvent(newEvent);
                return player;
            }

            return null;
        }

        public void SetAvatarForPlayer(string displayName, string avatarName)
        {
            avatarByDisplayName[displayName] = avatarName;

            bool watchedAvatar = serviceRegistry.GetAvatarManager().CheckAvatarByName(avatarName);
            if (watchedAvatar)
            {
                logger.Info($"{COLOR_PREFIX_LEAVE.GetAnsiEscape()}Watched Avatar Detected for Player {displayName}: {avatarName}{COLOR_RESET.GetAnsiEscape()}");
            }

            Player? player = GetPlayerByDisplayName(displayName);
            if (player != null)
            {
                player.IsAvatarWatch = watchedAvatar;
                player.AvatarName = avatarName;
                AddPlayerEventByDisplayName(displayName ?? string.Empty, PlayerEvent.EventType.AvatarWatch, $"User switched to Avatar : {avatarName}");

                if (watchedAvatar)
                {
                    player.PenActivity = $"AV: {avatarName}";
                    AddPlayerEventByDisplayName(displayName ?? string.Empty, PlayerEvent.EventType.AvatarWatch, $"User has used a watched Avatar : {avatarName}");

                }

                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
            }
        }

        private void PrintPlayerInfo(Player player)
        {
            logger.Info($"{COLOR_PREFIX_LEAVE.GetAnsiEscape()}Player Left: \n{player.ToString()}{COLOR_RESET.GetAnsiEscape()}");
        }

        internal void AddPenEventByDisplayName(string displayName, string eventText)
        {
            Player? player = GetPlayerByDisplayName(displayName);
            if (player != null)
            {
                player.PenActivity = eventText;
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
            }
        }

        internal async void AddInventorySpawn(string userId, string inventoryId)
        {
            Player? player = GetPlayerByUserId(userId);
            if (player != null)
            {
                string itemName = "Unknown Item";
                string itemUrl = "";
                string itemContent = "";
                string inventoryType = "Unknown Type";
                string aiEvaluation = "OK";

                try
                {
                    var inventoryItem = await serviceRegistry.GetVRChatAPIClient()?.GetUserInventoryItem(userId, inventoryId)!;
                    if (inventoryItem != null)
                    {
                        itemName = inventoryItem.Name ?? inventoryItem.ItemType ?? "Unknown Item";
                        itemUrl = inventoryItem.ImageUrl ?? "";
                        itemContent = inventoryItem.Metadata?.ImageUrl ?? itemUrl;
                        inventoryType = inventoryItem.ItemTypeLabel ?? "Unknown Type";

                        logger.Info($"Fetched inventory item: {itemName} / ({inventoryItem.ItemTypeLabel}) for user {userId}");
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn($"Failed to fetch inventory item {inventoryId} / {inventoryType} for user {userId}: {ex.Message}");
                }

                if (inventoryType.Contains("Emoji") || inventoryType.Contains("Sticker"))
                {
                    var ollamaClient = serviceRegistry.GetOllamaAPIClient();
                    if (ollamaClient != null)
                    {
                        string? evaluated = await ollamaClient.ClassifyImageList(userId, inventoryId, new List<string>{ itemUrl, itemContent });
                        if (evaluated != null)
                        {
                            aiEvaluation = EvaluatImage(evaluated) ?? "OK";
                            logger.Info($"Ollama classification for inventory item {inventoryId}: {aiEvaluation}: {evaluated}");
                            if (!aiEvaluation.Equals("OK"))
                            {
                                AddPlayerEventByUserId(userId, PlayerEvent.EventType.Emoji, $"AI Evaluation: Spawned Item {itemName} ({inventoryId}) was classified {evaluated}");
                                player.PenActivity = $"EM: {aiEvaluation}";
                                player.IsEmojiWatch = true;
                            }
                        }
                    }

                    PlayerInventory inventory = new PlayerInventory(inventoryId, itemName, itemUrl, inventoryType, aiEvaluation);
                    player.Inventory.Add(inventory);

                    AddPlayerEventByUserId(userId, PlayerEvent.EventType.Emoji, $"Spawned Item: {itemName} ({inventoryId})");
                    OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
                }
            }
        }

        private static string? EvaluatImage(string? imageEvaluation)
        {
            if (string.IsNullOrEmpty(imageEvaluation))
            {
                return null;
            }

            if (CheckLines(imageEvaluation, "Sexual Content"))
            {
                return "Sexual Content";
            }
            else if (CheckLines(imageEvaluation, "Racism"))
            {
                return "Racism";
            }
            else if (CheckLines(imageEvaluation, "Gore"))
            {
                return "Gore";
            }

            return null;
        }
        private static bool CheckLines(string input, string knownString)
        {
            string[] lines = input.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                return false;
            }

            bool firstLineContains = lines[0].Contains(knownString);

            return firstLineContains;
        }

        internal void AddStickerEvent(string displayName, string userId, string fileURL)
        {
            Player? player = GetPlayerByDisplayName(displayName);
            if (player != null)
            {
                player.LastStickerUrl = fileURL;
                AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.Sticker, $"Spawned sticker: {fileURL}");
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
            }
        }

        internal void CompactDatabase()
        {
            serviceRegistry.GetAvatarManager().CompactDatabase();
        }

        internal async void AddPrintData(string printId)
        {
            if (serviceRegistry.GetVRChatAPIClient() != null)
            {
                Print? printInfo = serviceRegistry.GetVRChatAPIClient().GetPrintInfo(printId);
                if (printInfo != null)
                {
                    if (playersByUserId.TryGetValue(printInfo.OwnerId, out Player? player))
                    {
                        string? evaluated = string.Empty;
                        var ollamaClient = serviceRegistry.GetOllamaAPIClient();
                        if (ollamaClient != null)
                        {
                            string aiEvaluation = "OK";
                            List<string> imageUrls = new List<string>();
                            imageUrls.Add(printInfo.Files.Image);
                            evaluated = await ollamaClient.ClassifyImageList(printInfo.OwnerId, printInfo.Id, imageUrls);
                            if (evaluated != null)
                            {
                                aiEvaluation = EvaluatImage(evaluated) ?? "OK";
                                logger.Info($"Ollama classification for inventory item {printInfo.Id}: {aiEvaluation}: {evaluated}");
                                if( !aiEvaluation.Equals("OK"))
                                {
                                    AddPlayerEventByUserId(printInfo.OwnerId, PlayerEvent.EventType.Print, $"AI Evaluation: Print {printId} was classified {evaluated}");
                                    player.PenActivity = "PR: " + aiEvaluation;
                                    player.IsPrintWatch = true;
                                }                               
                            }
                        }

                        player.PrintData[printId] = new PlayerPrint( printInfo, evaluated ?? "Not Evaluated" );
                        logger.Info($"Added Print {printId} for Player {player.DisplayName} (ID: {printInfo.OwnerId})");
                    }
                    AddPlayerEventByUserId(printInfo.OwnerId, PlayerEvent.EventType.Print, $"Dropped Print {printId}");
                }
            }
        }

        public GroupInfo? AddUpdateGroupFromVRC(string? groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return null;

            try
            {
                VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();
                VRChat.API.Model.Group? group = vrcClient.getGroupById(groupId);
                if (group != null)
                {
                    TailgrabDBContext dbContext = serviceRegistry.GetDBContext();
                    GroupInfo? existing = dbContext.GroupInfos.Find(group.Id);
                    if (existing == null)
                    {
                        GroupInfo newEntity = new GroupInfo
                        {
                            GroupId = group.Id,
                            GroupName = group.Name ?? string.Empty,
                            CreatedAt = group.CreatedAt,
                            UpdatedAt = DateTime.UtcNow,
                            IsBos = false
                        };

                        dbContext.GroupInfos.Add(newEntity);
                        dbContext.SaveChanges();
                        return newEntity;
                    }
                    else
                    {
                        existing.GroupId = group.Id;
                        existing.GroupName = group.Name ?? string.Empty;
                        existing.CreatedAt = group.CreatedAt;
                        existing.UpdatedAt = DateTime.UtcNow;
                        dbContext.GroupInfos.Update(existing);
                        dbContext.SaveChanges();
                        return existing;
                    }

                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to fetch Group: {ex.Message}");
            }

            return null;
        }
    }
}
