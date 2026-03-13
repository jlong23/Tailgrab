using ConcurrentPriorityQueue.Core;
using Microsoft.EntityFrameworkCore;
using NLog;
using System.ComponentModel;
using System.Text;
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.VRChat;
using Tailgrab.Common;
using Tailgrab.LineHandler;
using Tailgrab.Models;
using VRChat.API.Model;

namespace Tailgrab.PlayerManagement
{
    public class PlayerEvent(PlayerEvent.EventType type, string eventDescription)
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
        public EventType Type { get; set; } = type;
        public string EventDescription { get; set; } = eventDescription;
    }

    public class PlayerInventory(string inventoryId, string itemName, string itemUrl, string inventoryType, string aIEvaluation)
    {
        public string InventoryId { get; set; } = inventoryId;
        public string ItemName { get; set; } = itemName;
        public string ItemUrl { get; set; } = itemUrl;
        public string InventoryType { get; set; } = inventoryType;
        public string AIEvaluation { get; set; } = aIEvaluation;
        public DateTime SpawnedAt { get; set; } = DateTime.Now;
    }

    public class PlayerPrint(VRChat.API.Model.Print p, string aiEvaluation, string aiClassification)
    {
        public string PrintId { get; set; } = p.Id;
        public string OwnerId { get; set; } = p.OwnerId;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = p.CreatedAt;
        public string PrintUrl { get; set; } = p.Files.Image;
        public string AIEvaluation { get; set; } = aiEvaluation;
        public string AIClass { get; set; } = aiClassification;
        public string AuthorName { get; set; } = p.AuthorName;
    }

    public class AlertMessage(AlertClassEnum alertClass, AlertTypeEnum alertType, string color, string message)
    {
        public AlertClassEnum AlertClass { get; set; } = alertClass;
        public AlertTypeEnum AlertType { get; set; } = alertType;
        public string Color { get; set; } = color;
        public string Message { get; set; } = message;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class PlayerAvatar(string avatarName, string createdBy)
    {
        public string AvatarName { get; set; } = avatarName;
        public string? CreatedBy { get; set; } = createdBy;
    }

    public class Player(string userId, string displayName, SessionInfo session) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string UserId { get; set; } = userId;
        public string DisplayName { get; set; } = displayName;
        public string AvatarName { get; set; } = "";
        public string PenActivity { get; set; } = "";
        public int NetworkId { get; set; }
        public DateTime InstanceStartTime { get; set; } = DateTime.Now;
        public DateTime? InstanceEndTime { get; set; }
        public List<PlayerEvent> Events { get; set; } = [];
        public List<PlayerInventory> Inventory { get; set; } = [];
        public SessionInfo Session { get; set; } = session;
        public string? LastStickerUrl { get; set; } = string.Empty;

        public Dictionary<string, PlayerPrint> PrintData = [];
        public string? UserBio { get; set; }
        public string? AIEval { get; set; }

        public List<AlertMessage> _AlertMessage = [];

        public string AlertMessage
        {
            get
            {
                string message = "";

                _AlertMessage.Sort((p1, p2) =>
                {
                    int result = p2.AlertType.CompareTo(p1.AlertType);
                    if (result == 0)
                    {
                        result = p2.Timestamp.CompareTo(p1.Timestamp);
                    }
                    return result;
                });

                foreach (AlertMessage alert in _AlertMessage)
                {
                    message += $"[{alert.AlertClass}/{alert.AlertType}] {alert.Message}; ";
                }

                return message;
            }
        }

        public string AlertColor { get; private set; } = "None";

        public AlertTypeEnum MaxAlertType { get; private set; } = AlertTypeEnum.None;

        private DateOnly? _dateJoined;
        public DateOnly? DateJoined
        {
            get => _dateJoined;
            set
            {
                if (_dateJoined != value)
                {
                    _dateJoined = value;
                    OnPropertyChanged(nameof(DateJoined));
                    OnPropertyChanged(nameof(ProfileElapsedTime));
                }
            }
        }

        public string ProfileElapsedTime
        {
            get
            {
                try
                {
                    DateTime joinDate = DateTime.Parse(_dateJoined.ToString() ?? new DateTime().ToString());
                    TimeSpan elapsed = DateTime.Now - joinDate;

                    // If >= 1 year, show years
                    if (elapsed.TotalDays >= 365)
                    {
                        double years = elapsed.TotalDays / 365.25; // Account for leap years
                        return $"{years:F1}Y";
                    }
                    else if( elapsed.TotalDays >= 30)
                    {
                        double months = elapsed.TotalDays / 30.44; // Average days per month
                        return $"{months:F1}M";
                    }
                    else if (elapsed.TotalDays >= 7)
                    {
                        double weeks = elapsed.TotalDays / 7;
                        return $"{weeks:F1}W";
                    }
                    else if (elapsed.TotalDays >= 1)
                    {
                        double days = elapsed.TotalDays;
                        return $"{days:F1}D";
                    }
                    else if (elapsed.TotalDays < 1)
                    {
                        double hours = elapsed.Hours;
                        return $"{hours:F1}H";
                    }
                }
                catch
                {
                    return "N/A";
                }

                return "N/A";
            }
        }


        // This goes away with the new alert system, but for now it is used to track if any of the watch types are active for a player
        public bool IsWatched
        {
            get
            {
                if (_AlertMessage.Count > 0)
                {
                    return true;
                }

                return false;
            }
        }


        private bool _isFriend = false;
        public bool IsFriend { 
            get
            {
                return _isFriend;
            }
            set 
            { 
                if( value == true)
                {
                    AlertColor = "Friend";
                }
                _isFriend = value;
            } }

        public void AddAlertMessage(AlertClassEnum alertClass, AlertTypeEnum alertType, string message)
        {
            string alertColor = PlayerManager.GetAlertColor(alertClass, alertType);
            AlertMessage newAlert = new (alertClass, alertType, alertColor, message);
            _AlertMessage.Add(newAlert);

            foreach (AlertMessage alert in _AlertMessage)
            {
                if (alert.AlertType > MaxAlertType)
                {
                    MaxAlertType = alert.AlertType;
                    if( _isFriend == false)
                    {
                        AlertColor = alert.Color;
                    }
                }
            }
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
            StringBuilder sb = new ();
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

    public class SessionInfo(string worldId, string instanceId)
    {
        public string WorldId { get; set; } = worldId;
        public string InstanceId { get; set; } = instanceId;
        public DateTime StartDateTime { get; } = DateTime.Now;
    }

    public class PlayerChangedEventArgs(PlayerChangedEventArgs.ChangeType type, Player player) : EventArgs
    {
        public enum ChangeType
        {
            Added,
            Updated,
            Removed,
            Cleared
        }

        public ChangeType Type { get; } = type;
        public Player Player { get; } = player;
    }

    public class PlayerManager
    {
        private static ServiceRegistry serviceRegistry;
        public PlayerManager(ServiceRegistry registry)
        {
            serviceRegistry = registry;
        }

        private static Dictionary<string, Player> playersByUserId = [];
        private static Dictionary<int, string> userIdByNetworkId = [];
        private static Dictionary<string, string> userIdByDisplayName = [];
        private static Dictionary<string, string> avatarByDisplayName = [];
        private static Dictionary<string, PlayerAvatar> PlayerAvatarByName = [];
        public static SessionInfo CurrentSession = new("", "");

        public static readonly AnsiColor COLOR_PREFIX_LEAVE = AnsiColor.Yellow;
        public static readonly AnsiColor COLOR_PREFIX_JOIN = AnsiColor.Green;
        public static readonly AnsiColor COLOR_RESET = AnsiColor.Reset;
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // Event for UI and other listeners
        public static event EventHandler<PlayerChangedEventArgs>? PlayerChanged;

        private static ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue = new();
        private static Dictionary<String, DateTime> recentlyProcessedAvatars = [];



        public static Player? GetPlayerByDisplayName(string displayName)
        {
            if (userIdByDisplayName.TryGetValue(displayName, out string? userId))
            {
                return GetPlayerByUserId(userId);
            }
            return null;
        }

        public static Player? GetPlayerByNetworkId(int networkId)
        {
            if (userIdByNetworkId.TryGetValue(networkId, out string? userId))
            {
                return GetPlayerByUserId(userId);
            }
            return null;
        }

        public static Player? GetPlayerByUserId(string userId)
        {
            playersByUserId.TryGetValue(userId, out Player? player);
            return player;
        }


        public static void OnPlayerChanged(PlayerChangedEventArgs.ChangeType changeType, Player player)
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

        public static void OnPlayerChanged(PlayerChangedEventArgs.ChangeType changeType, string displayName)
        {
            try
            {
                Player? player = GetPlayerByDisplayName(displayName);
                if (player != null)
                {
                    PlayerChanged?.Invoke(null, new PlayerChangedEventArgs(changeType, player));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error raising PlayerChanged event");
            }
        }

        public static void UpdateCurrentSession(string worldId, string instanceId)
        {
            CurrentSession = new SessionInfo(worldId, instanceId);
        }

        public void PlayerJoined(string userId, string displayName, AbstractLineHandler handler)
        {
            Player? player;
            if (!playersByUserId.TryGetValue(userId, out Player? value))
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
                player = value;
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

                // Update or create UserInfo record with elapsed time
                UserInfo? user = dBContext.UserInfos.Find(player.UserId);
                if (user == null)
                {
                    user = new UserInfo
                    {
                        DisplayName = player.DisplayName,
                        UserId = player.UserId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        ElapsedMinutes = timeDifference.TotalMinutes
                    };
                    dBContext.Add(user);
                    dBContext.SaveChanges();
                }
                else
                {
                    user.DisplayName = player.DisplayName;
                    user.UpdatedAt = DateTime.Now;
                    user.ElapsedMinutes += timeDifference.TotalMinutes;
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

        public static Player? AssignPlayerNetworkId(string displayName, int networkId)
        {
            Player? player = GetPlayerByDisplayName(displayName);
            if (player != null)
            {
                player.NetworkId = networkId;
                userIdByNetworkId[networkId] = player.UserId;
            }

            return player;
        }

        public static IEnumerable<Player> GetAllPlayers()
        {
            return playersByUserId.Values;
        }

        public static void ClearAllPlayers(AbstractLineHandler handler)
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
            PlayerAvatarByName.Clear();

            // Also a global cleared notification (consumers may want to reset)
            OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Cleared, new Player("", "", CurrentSession) { InstanceStartTime = DateTime.MinValue });
        }

        public static int GetPlayerCount()
        {
            return playersByUserId.Count;
        }

        public static void LogAllPlayers(AbstractLineHandler handler)
        {
            if (handler.LogOutput)
            {
                foreach (var player in playersByUserId.Values)
                {
                    PrintPlayerInfo(player);
                }
            }
        }

        public static Player? AddPlayerEventByDisplayName(string displayName, PlayerEvent.EventType eventType, string eventDescription)
        {

            if (userIdByDisplayName.TryGetValue(displayName, out string? userId))
            {
                return AddPlayerEventByUserId(userId, eventType, eventDescription);
            }

            return null;
        }

        public static Player? AddPlayerEventByUserId(string userId, PlayerEvent.EventType eventType, string eventDescription)
        {
            if (playersByUserId.TryGetValue(userId, out Player? player))
            {
                PlayerEvent newEvent = new(eventType, eventDescription);
                player.AddEvent(newEvent);
                return player;
            }

            return null;
        }

        public void SetAvatarForPlayer(string displayName, string avatarName)
        {
            avatarByDisplayName[displayName] = avatarName;

            Player? player = AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.AvatarWatch, $"User switched to Avatar : {avatarName}"); ;
            if (player != null)
            {
                AvatarInfo? watchedAvatar = PlayerManager.CheckAvatarByName(avatarName);
                if (watchedAvatar != null)
                {
                    logger.Info($"{COLOR_PREFIX_LEAVE.GetAnsiEscape()}Watched Avatar Detected for Player {displayName}: {avatarName} with AlertType {watchedAvatar.AlertType}{COLOR_RESET.GetAnsiEscape()}");
                    if (watchedAvatar.AlertType > AlertTypeEnum.None)
                    {
                        player = AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.AvatarWatch, $"User has used a watched Avatar : {avatarName} alertType: {watchedAvatar.AlertType}");
                        player?.AddAlertMessage(AlertClassEnum.Avatar, watchedAvatar.AlertType, $"{avatarName}");
                    }
                }
                if (player != null)
                {
                    player.AvatarName = avatarName;
                    OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
                }
            }
        }

        public static PlayerAvatar UpdatePlayerAvatar(string avatarName, string uploadedBy)
        {

            if (PlayerAvatarByName.TryGetValue(avatarName, out PlayerAvatar? playerAvatar))
            {
                return playerAvatar;
            }
            else
            {
                playerAvatar = new PlayerAvatar(avatarName, uploadedBy);
                PlayerAvatarByName[avatarName] = playerAvatar;

            }
            return playerAvatar;
        }

        private static void PrintPlayerInfo(Player player)
        {
            logger.Info($"{COLOR_PREFIX_LEAVE.GetAnsiEscape()}Player Left: \n{player}{COLOR_RESET.GetAnsiEscape()}");
        }

        internal static void AddPenEventByDisplayName(string displayName, string eventText)
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
                string aiClassification = "OK";
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
                        ImageEvaluation? evaluated = await ollamaClient.ClassifyImageList(userId, inventoryId, [itemUrl, itemContent]);
                        if (evaluated != null)
                        {
                            string evaluatedText = System.Text.Encoding.UTF8.GetString(evaluated.Evaluation);
                            aiClassification = EvaluateImageClass(evaluatedText) ?? "OK";
                            logger.Info($"Ollama classification for inventory item {inventoryId}: {aiClassification}: {evaluatedText}");
                            if (!aiClassification.Equals("OK") && !evaluated.IsIgnored)
                            {
                                AddPlayerEventByUserId(userId, PlayerEvent.EventType.Emoji, $"AI Evaluation: Spawned Item {itemName} ({inventoryId}) was classified {aiClassification}");
                                player.AddAlertMessage(AlertClassEnum.EmojiSticker, AlertTypeEnum.Nuisance, $"{aiClassification}");
                            }
                        }
                    }

                    PlayerInventory inventory = new(inventoryId, itemName, itemContent, inventoryType, aiClassification);
                    player.Inventory.Add(inventory);

                    AddPlayerEventByUserId(userId, PlayerEvent.EventType.Emoji, $"Spawned Item: {itemName} ({inventoryId})");
                    OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
                }
            }
        }

        private static string? EvaluateImageClass(string? imageEvaluation)
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
            string[] lines = input.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                return false;
            }

            bool firstLineContains = lines[0].Contains(knownString);

            return firstLineContains;
        }

        internal static void AddStickerEvent(string displayName, string fileURL)
        {
            Player? player = GetPlayerByDisplayName(displayName);
            if (player != null)
            {
                player.LastStickerUrl = fileURL;
                AddPlayerEventByDisplayName(displayName, PlayerEvent.EventType.Sticker, $"Spawned sticker: {fileURL}");
                OnPlayerChanged(PlayerChangedEventArgs.ChangeType.Updated, player);
            }
        }

        internal async void AddPrintData(string printId)
        {
            if (serviceRegistry.GetVRChatAPIClient() != null)
            {
                Print? printInfo = serviceRegistry.GetVRChatAPIClient().GetPrintInfo(printId);
                if (printInfo != null)
                {
                    Player? player = AddPlayerEventByUserId(printInfo.OwnerId, PlayerEvent.EventType.Print, $"Dropped Print {printId}");
                    if (player != null)
                    {
                        logger.Info($"Fetched print info for print {printId} owned by {player.DisplayName} (ID: {printInfo.OwnerId})");
                        string evaluatedText = "Not Evaluated";
                        string aiClassification = "OK";
                        var ollamaClient = serviceRegistry.GetOllamaAPIClient();
                        if (ollamaClient != null)
                        {
                            List<string> imageUrls = [];
                            imageUrls.Add(printInfo.Files.Image);
                            ImageEvaluation? evaluated = await ollamaClient.ClassifyImageList(printInfo.OwnerId, printInfo.Id, imageUrls);
                            if (evaluated != null)
                            {
                                evaluatedText = System.Text.Encoding.UTF8.GetString(evaluated.Evaluation);
                                aiClassification = EvaluateImageClass(System.Text.Encoding.UTF8.GetString( evaluated.Evaluation)) ?? "OK";
                                logger.Info($"Ollama classification for inventory item {printInfo.Id}: {aiClassification}: {evaluatedText}");
                                if (!aiClassification.Equals("OK") && !evaluated.IsIgnored)
                                {
                                    player = AddPlayerEventByUserId(printInfo.OwnerId, PlayerEvent.EventType.Print, $"AI Evaluation: Print {printId} was classified {aiClassification}");
                                    player?.AddAlertMessage(AlertClassEnum.Print, AlertTypeEnum.Nuisance, $"{aiClassification}");
                                }
                            }
                        }

                        player?.PrintData[printId] = new PlayerPrint(printInfo, evaluatedText, aiClassification);
                    }
                }
            }
        }


        public Player? UpdatePlayerUserFromVRCProfile(User profile, string profileHash)
        {
            logger.Warn($"Updating UserInfo for user {profile.DisplayName} (ID: {profile.Id}) with DateJoined: {profile.DateJoined} and ProfileHash: {profileHash}");
            if (profile != null && profile.Id != null)
            {
                TailgrabDBContext dbContext = serviceRegistry.GetDBContext();
                Player? player = GetPlayerByUserId(profile.Id);
                if (player != null)
                {
                    player.DateJoined = profile.DateJoined;
                    logger.Info($"Updated UserInfo for user {profile.DisplayName} (ID: {profile.Id}) with DateJoined: {profile.DateJoined} and ProfileHash: {profileHash}; {player.ProfileElapsedTime}");
                }

                // Update or create UserInfo record with elapsed time
                UserInfo? user = dbContext.UserInfos.Find(profile.Id);
                if (user != null)
                {
                    user.DateJoined = profile.DateJoined;
                    user.UpdatedAt = DateTime.UtcNow;
                    user.LastProfileChecksum = profileHash;
                    dbContext.UserInfos.Update(user);
                }
                else
                {
                    user = new UserInfo
                    {
                        DisplayName = profile.DisplayName,
                        UserId = profile.Id,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        DateJoined = profile.DateJoined,
                        LastProfileChecksum = profileHash
                    };
                    dbContext.Add(user);
                }
                dbContext.SaveChanges();


                return player;
            }
            else
            {
                logger.Warn($"Attempted to update player user info from VRC profile, but profile was null");
                return null;
            }
        }

        public GroupInfo? AddUpdateGroupFromVRC(string? groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return null;

            try
            {
                VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();
                VRChat.API.Model.Group? group = vrcClient.GetGroupById(groupId);
                if (group != null)
                {
                    TailgrabDBContext dbContext = serviceRegistry.GetDBContext();
                    GroupInfo? existing = dbContext.GroupInfos.Find(group.Id);
                    if (existing == null)
                    {
                        GroupInfo newEntity = new()
                        {
                            GroupId = group.Id,
                            GroupName = group.Name ?? string.Empty,
                            CreatedAt = group.CreatedAt,
                            UpdatedAt = DateTime.UtcNow
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

        public void SyncAvatarModerations()
        {
            try
            {
                TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
                VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();
                if (dBContext != null && vrcClient != null)
                {
                    int lineNumber = 0;
                    List<VRChat.API.Model.AvatarModeration> moderations = vrcClient.GetAvatarModerations();
                    foreach (VRChat.API.Model.AvatarModeration mod in moderations)
                    {
                        logger.Debug($"Processing Avatar Moderation for Avatar ID {mod.TargetAvatarId} with Status {mod.AvatarModerationType} and CreatedAt {mod.Created}");
                        if (mod != null && mod.AvatarModerationType.Equals(AvatarModerationType.Block))
                        {

                            lineNumber++;
                            AvatarInfo? existingAvatar = dBContext.AvatarInfos.Find(mod.TargetAvatarId);
                            if (existingAvatar == null || existingAvatar.AlertType < AlertTypeEnum.Nuisance )
                            {
                                QueuedModeratedAvatarWatch watchItem = new(2, mod.TargetAvatarId, AlertTypeEnum.Nuisance, lineNumber);
                                EnqueueModeratedAvatarForCheck(watchItem);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to clear the database");
            }
        }

        #region Alert Color Management
        public static string GetAlertColor(AlertClassEnum alertClass, AlertTypeEnum alertType)
        {
            string alertKey = alertClass switch
            {
                AlertClassEnum.Avatar => CommonConst.Avatar_Alert_Key,
                AlertClassEnum.Group => CommonConst.Group_Alert_Key,
                AlertClassEnum.Profile => CommonConst.Profile_Alert_Key,
                AlertClassEnum.Print => CommonConst.Profile_Alert_Key,
                AlertClassEnum.EmojiSticker => CommonConst.Profile_Alert_Key,
                _ => CommonConst.Profile_Alert_Key

            };

            string key = CommonConst.ConfigRegistryPath + "\\" + alertKey + "\\" + alertType.ToString();
            return ConfigStore.GetStoredKeyString(key, CommonConst.Color_Alert_Key) ?? "None";
        }

        #endregion


        #region Avatar Management
        public static int GetQueueCount()
        {
            return priorityQueue.Count;
        }

        public void AddAvatar(AvatarInfo avatar)
        {
            try
            {
                serviceRegistry.GetDBContext().AvatarInfos.Add(avatar);
                serviceRegistry.GetDBContext().SaveChanges();
            }
            catch (Exception ex)
            {
                logger.Error($"Error creating avatar: {ex.Message}");
            }
        }

        public static AvatarInfo? GetAvatarById(string avatarId)
        {
            return serviceRegistry.GetDBContext().AvatarInfos.Find(avatarId);
        }

        public void UpdateAvatar(AvatarInfo avatar)
        {
            try
            {
                avatar.UpdatedAt = DateTime.UtcNow;
                serviceRegistry.GetDBContext().AvatarInfos.Update(avatar);
                serviceRegistry.GetDBContext().SaveChanges();
            }
            catch (Exception ex)
            {
                logger.Error($"Error updating avatar: {ex.Message}");
            }
        }

        public void DeleteAvatar(string avatarId)
        {
            var avatar = serviceRegistry.GetDBContext().AvatarInfos.Find(avatarId);
            if (avatar != null)
            {
                serviceRegistry.GetDBContext().AvatarInfos.Remove(avatar);
                serviceRegistry.GetDBContext().SaveChanges();
            }
        }

        public static void CacheAvatars(List<string> avatarIdInCache)
        {
            foreach (var avatarId in avatarIdInCache)
            {
                EnqueueAvatarForCheck(avatarId);
            }
        }

        private static void EnqueueAvatarForCheck(string avatarId)
        {
            if (recentlyProcessedAvatars.TryGetValue(avatarId, out DateTime dateTime))
            {
                if ((DateTime.UtcNow - dateTime).TotalMinutes < 60)
                {
                    return;
                }
            }
            recentlyProcessedAvatars.Add(avatarId, DateTime.UtcNow);

            var queuedItem = new QueuedAvatarProcess(5, avatarId);

            priorityQueue.Enqueue(queuedItem);
        }

        public static void EnqueueWatchAvatarForCheck(QueuedAvatarWatch watch)
        {
            priorityQueue.Enqueue(watch);
        }

        public void EnqueueModeratedAvatarForCheck(QueuedModeratedAvatarWatch watch)
        {
            priorityQueue.Enqueue(watch);
        }


        public void GetAvatarsFromUser(string userId, string avatarName)
        {

            logger.Debug($"Fetching avatars for user {userId} to find avatar named {avatarName}");

            try
            {
                // Avatar already exists in the database and was updated within the last 12 hours
                System.Threading.Thread.Sleep(500);
                List<Avatar> avatarData = serviceRegistry.GetVRChatAPIClient().GetAvatarsByUserId(userId);
                foreach (var avatar in avatarData)
                {
                    logger.Debug(avatar.ToString());
                    if (avatar.Name.Equals(avatarName, StringComparison.OrdinalIgnoreCase))
                    {
                        AvatarInfo? dbAvatarInfo = GetAvatarById(avatar.Id);

                        if (dbAvatarInfo == null)
                        {
                            var avatarInfo = new AvatarInfo
                            {
                                AvatarId = avatar.Id,
                                UserId = avatar.AuthorId,
                                AvatarName = avatar.Name,
                                ImageUrl = avatar.ImageUrl,
                                CreatedAt = avatar.CreatedAt,
                                UpdatedAt = DateTime.UtcNow,
                                AlertType = AlertTypeEnum.None,
                                UserName = avatar.AuthorName
                            };

                            AddAvatar(avatarInfo);
                        }
                        else
                        {
                            dbAvatarInfo.UserId = avatar.AuthorId;
                            dbAvatarInfo.UserName = avatar.AuthorName;
                            dbAvatarInfo.AvatarName = avatar.Name;
                            dbAvatarInfo.ImageUrl = avatar.ImageUrl;
                            dbAvatarInfo.CreatedAt = avatar.CreatedAt;
                            UpdateAvatar(dbAvatarInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching avatar: {ex.Message}");
            }
        }

        public void CompactDatabase()
        {
            serviceRegistry.GetDBContext().Database.ExecuteSqlRaw("VACUUM;");
        }

        public static AvatarInfo? CheckAvatarByName(string avatarName)
        {
            var bannedAvatars = serviceRegistry.GetDBContext().AvatarInfos
                                         .Where(b => b.AvatarName != null && b.AvatarName.Equals(avatarName) && b.AlertType > 0)
                                         .OrderByDescending(b => b.AlertType)
                                         .ToList();

            if (bannedAvatars.Count > 0)
            {
                // Play alert sound based on the highest alert type found for the avatar
                AlertTypeEnum maxAlertType = bannedAvatars[0].AlertType;
                SoundManager.PlayAlertSound(CommonConst.Avatar_Alert_Key, maxAlertType);

                return bannedAvatars[0];
            }

            return null;
        }

        public static async Task AvatarCheckTask(ConcurrentPriorityQueue<IHavePriority<int>, int> priorityQueue, ServiceRegistry serviceRegistry)
        {
            OllamaClient.logger.Info($"Amplitude Avatar Cache Queue Running");
            TailgrabDBContext dBContext = serviceRegistry.GetDBContext();
            while (true)
            {
                // Process items from the priority queue
                while (true)
                {
                    var result = priorityQueue.Dequeue();
                    if (result.IsSuccess)
                    {
                        if (result.Value is QueuedAvatarProcess item && item.AvatarId != null)
                        {
                            await UpdateAmpAvatarRecord(serviceRegistry, dBContext, item.AvatarId);
                        }
                        else if (result.Value is QueuedAvatarWatch item2)
                        {
                            await UpdateWatchedAvatarRecord(serviceRegistry, dBContext, item2);
                        }
                        else if (result.Value is QueuedModeratedAvatarWatch item3)
                        {
                            await UpdateModeratedAvatarRecord(serviceRegistry, dBContext, item3);
                        }
                    }
                    else
                    {
                        // No more items to process
                        break;
                    }
                }
                // Wait for a short period before checking the queue again
                await Task.Delay(5000);
            }
        }

        private static async Task UpdateAmpAvatarRecord(ServiceRegistry serviceRegistry, TailgrabDBContext dBContext, string avatarId)
        {
            try
            {
                AvatarInfo? dbAvatarInfo = dBContext.AvatarInfos.Find(avatarId);
                bool updateNeeded = false;
                if (dbAvatarInfo == null)
                {
                    updateNeeded = true;
                }
                else if (dbAvatarInfo.AlertType == AlertTypeEnum.None &&
                    (!dbAvatarInfo.UpdatedAt.HasValue || dbAvatarInfo.UpdatedAt.Value >= DateTime.UtcNow.AddHours(-2)))
                {
                    updateNeeded = true;
                }

                if (updateNeeded)
                {
                    // Adds and Updates avatar info in the database, if it doesn't exist or was last updated more than 2 hours ago
                    Avatar? avatarData = FetchUpdateAvatarData(serviceRegistry, dBContext, avatarId, dbAvatarInfo);

                    if (avatarData == null && dbAvatarInfo == null)
                    {
                        // Private Avatar
                        CreateAvatarInfoForPrivate(dBContext, avatarId);
                    }

                    // Wait for a short period before checking the queue again
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error fetching user profile for userId: {avatarId}");
            }
        }


        private static async Task UpdateModeratedAvatarRecord(ServiceRegistry _serviceRegistry, TailgrabDBContext dbContext, QueuedModeratedAvatarWatch watch)
        {
            try
            {
                // Fetch the AvatarInfo record
                AvatarInfo? avatarInfo = await dbContext.AvatarInfos.FindAsync(watch.AvatarId);
                PlayerManager.FetchUpdateAvatarData(_serviceRegistry, dbContext, watch.AvatarId, avatarInfo);
                avatarInfo = await dbContext.AvatarInfos.FindAsync(watch.AvatarId);

                if (avatarInfo == null)
                {
                    logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' not found in database/vrc, skipping.");
                }
                else if (avatarInfo.AlertType == AlertTypeEnum.None)
                {
                    avatarInfo.AlertType = AlertTypeEnum.Nuisance;
                    avatarInfo.UpdatedAt = DateTime.UtcNow;
                    dbContext.AvatarInfos.Update(avatarInfo);
                    dbContext.SaveChanges();
                }
                else
                {
                    logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' already has Has an Alert, skipping.");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Line {watch.LineNumber}: Error processing avatar ID '{watch.AvatarId}'");
            }

            // Throttle processing to avoid overwhelming the API
            await Task.Delay(1000);
        }

        private static async Task UpdateWatchedAvatarRecord(ServiceRegistry _serviceRegistry, TailgrabDBContext dbContext, QueuedAvatarWatch watch)
        {
            try
            {
                // Fetch the AvatarInfo record
                AvatarInfo? avatarInfo = await dbContext.AvatarInfos.FindAsync(watch.AvatarId);
                PlayerManager.FetchUpdateAvatarData(_serviceRegistry, dbContext, watch.AvatarId, avatarInfo);
                avatarInfo = await dbContext.AvatarInfos.FindAsync(watch.AvatarId);

                if (avatarInfo == null)
                {
                    logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' not found in database/vrc, skipping.");
                }
                else if (avatarInfo.AlertType == AlertTypeEnum.None)
                {

                    avatarInfo.AlertType = watch.AlertType;
                    avatarInfo.UpdatedAt = DateTime.UtcNow;
                    dbContext.AvatarInfos.Update(avatarInfo);
                    dbContext.SaveChanges();

                    if (avatarInfo.AlertType >= AlertTypeEnum.Nuisance)
                    {
                        await _serviceRegistry.GetVRChatAPIClient().BlockAvatarGlobal(avatarInfo.AvatarId);
                    }
                    else
                    {
                        await _serviceRegistry.GetVRChatAPIClient().DeleteAvatarGlobal(avatarInfo.AvatarId);
                    }

                    logger.Debug($"Line {watch.LineNumber}: Set Watch State for Avatar ID '{watch.AvatarId}'");

                }
                else
                {
                    logger.Debug($"Line {watch.LineNumber}: Avatar ID '{watch.AvatarId}' already has Has an Alert, skipping.");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Line {watch.LineNumber}: Error processing avatar ID '{watch.AvatarId}'");
            }

            // Throttle processing to avoid overwhelming the API
            await Task.Delay(3000);
        }


        private static void CreateAvatarInfoForPrivate(TailgrabDBContext dBContext, string AvatarId)
        {
            var avatarInfo = new AvatarInfo
            {
                AvatarId = AvatarId,
                UserId = "",
                AvatarName = $"Unknown Avatar {AvatarId}",
                ImageUrl = "",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                dBContext.Add(avatarInfo);
                dBContext.SaveChanges();
                logger.Debug($"Adding fallback avatar record for {avatarInfo}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error adding fallback avatar record for {AvatarId}: {ex.Message}");
            }
        }

        public static Avatar? FetchUpdateAvatarData(ServiceRegistry serviceRegistry, TailgrabDBContext dBContext, string AvatarId, AvatarInfo? dbAvatarInfo)
        {
            Avatar? avatarData = null;
            try
            {
                // Avatar already exists in the database and was updated within the last 12 hours
                System.Threading.Thread.Sleep(500);
                avatarData = serviceRegistry.GetVRChatAPIClient().GetAvatarById(AvatarId);
                if (avatarData != null)
                {
                    if (dbAvatarInfo == null)
                    {
                        var avatarInfo = new AvatarInfo
                        {
                            AvatarId = avatarData.Id,
                            UserId = avatarData.AuthorId,
                            UserName = avatarData.AuthorName,
                            AvatarName = avatarData.Name,
                            ImageUrl = avatarData.ImageUrl,
                            CreatedAt = avatarData.CreatedAt,
                            UpdatedAt = DateTime.UtcNow
                        };

                        try
                        {
                            dBContext.Add(avatarInfo);
                            dBContext.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Error adding avatar record for {AvatarId}: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Ensure entity is attached to the dbContext before updating to avoid Detached state errors
                        var entry = dBContext.Entry(dbAvatarInfo);
                        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                        {
                            dBContext.Attach(dbAvatarInfo);
                            entry = dBContext.Entry(dbAvatarInfo);
                        }

                        dbAvatarInfo.UserId = avatarData.AuthorId;
                        dbAvatarInfo.UserName = avatarData.AuthorName;
                        dbAvatarInfo.AvatarName = avatarData.Name;
                        dbAvatarInfo.ImageUrl = avatarData.ImageUrl;
                        dbAvatarInfo.CreatedAt = avatarData.CreatedAt;
                        dbAvatarInfo.UpdatedAt = DateTime.UtcNow;

                        try
                        {
                            entry.State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            dBContext.SaveChanges();
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Error updating avatar record for {AvatarId}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching avatar: {ex.Message}");
            }

            return avatarData;
        }
    }
    #endregion

    #region Avatar Queue Classes
    internal class QueuedAvatarProcess(int priority, string avatarId) : IHavePriority<int>
    {
        public int Priority { get; set; } = priority;

        public string AvatarId { get; set; } = avatarId;
    }


    public class QueuedAvatarWatch(int priority, string avatarId, AlertTypeEnum alertType, int lineNumber) : IHavePriority<int>
    {
        public int Priority { get; set; } = priority;

        public string AvatarId { get; set; } = avatarId;

        public AlertTypeEnum AlertType { get; set; } = alertType;

        public int LineNumber { get; set; } = lineNumber;
    }

    public class QueuedModeratedAvatarWatch(int priority, string avatarId, AlertTypeEnum alertType, int lineNumber) : IHavePriority<int>
    {
        public int Priority { get; set; } = priority;

        public string AvatarId { get; set; } = avatarId;

        public AlertTypeEnum AlertType { get; set; } = alertType;

        public int LineNumber { get; set; } = lineNumber;
    }
    #endregion
}
