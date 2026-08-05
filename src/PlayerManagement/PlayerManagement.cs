using ConcurrentPriorityQueue.Core;
using Microsoft.EntityFrameworkCore;
using NLog;
using System.ComponentModel;
using System.Text;
using System.Windows;
using Tailgrab.Clients.Ollama;
using Tailgrab.Clients.XSOverlay;
using Tailgrab.Common;
using Tailgrab.LineHandler;
using Tailgrab.Models;
using VRChat.API.Model;
using static Tailgrab.Clients.VRChat.VRChatClient;

namespace Tailgrab.PlayerManagement
{
    public class PlayerManager
    {

        private static ServiceRegistry serviceRegistry;
        public PlayerManager(ServiceRegistry registry)
        {   
            if(registry == null)
            {
                throw new ArgumentNullException(nameof(registry), "ServiceRegistry parameter cannot be null.");
            }
            serviceRegistry = registry;
        }

        private static Dictionary<string, Player> playersByUserId = [];
        private static Dictionary<int, string> userIdByNetworkId = [];
        private static Dictionary<string, string> userIdByDisplayName = [];
        private static Dictionary<string, string> avatarByDisplayName = [];
        private static Dictionary<string, PlayerAvatar> playerAvatarByName = [];
        public static SessionInfo CurrentSession = new("", "");

        public static readonly AnsiColor COLOR_PREFIX_LEAVE = AnsiColor.Yellow;
        public static readonly AnsiColor COLOR_PREFIX_JOIN = AnsiColor.Green;
        public static readonly AnsiColor COLOR_RESET = AnsiColor.Reset;
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // Event for UI and other listeners
        public static event EventHandler<PlayerChangedEventArgs>? PlayerChanged;

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

        public static PlayerAvatar? GetPlayerAvatarByName(string avatarName)
        {
            if (playerAvatarByName.TryGetValue(avatarName, out PlayerAvatar? playerAvatar))
            {
                return playerAvatar;
            }
            return null;
        }

        public static void SetPlayerAvatarByName(string avatarName, PlayerAvatar playerAvatar)
        {
            playerAvatarByName[avatarName] = playerAvatar;
        }

        public static void SetAvatarByDisplayName(string playerName, string avatarName)
        {
            avatarByDisplayName[playerName] = avatarName;
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
            OverlayManager overlay = serviceRegistry.GetXSOverlay();
            overlay.Initialize();
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

            serviceRegistry.GetGroupManager().CheckUserGroups(userId);
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
            playerAvatarByName.Clear();

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


        public Player? UpdatePlayerUserFromVRCProfile(User profile, string profileHash)
        {
            if (profile != null && profile.Id != null)
            {
                TailgrabDBContext dbContext = serviceRegistry.GetDBContext();
                Player? player = GetPlayerByUserId(profile.Id);
                if (player != null)
                {
                    player.DateJoined = profile.DateJoined;
                    logger.Debug($"Updated UserInfo for user {profile.DisplayName} (ID: {profile.Id}) with DateJoined: {profile.DateJoined} and ProfileHash: {profileHash}; {player.ProfileElapsedTime}");
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

        #region Moderation Report Management
        public async Task GetModerationReports()
        {
            try
            {
                int offset = 0;
                while (true)
                {
                    ModerationReportListResponse? reports = await serviceRegistry.GetVRChatAPIClient().ListModerationReportAsync(offset);
                    if (reports == null)
                        break;

                    foreach (var report in reports.Results)
                    {
                        logger.Info($"Report ID: {report.Id}, Type: {report.Type}, ContentId: {report.ContentId}, ContentName: {report.ContentName}");
                        await SaveModerationReport(report);
                    }
                    offset += 60;
                    if (reports.HasNext == false)
                    {
                        logger.Info("No more moderation reports to process.");
                        break;
                    }
                    await Task.Delay(1000); // Delay for 1 second before the next request
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to fetch moderation reports");
            }
        }

        public async Task SaveModerationReport(ModerationReportPayload rpt, ModerationReportResponse response, string UserId)
        {
            try
            {
                TailgrabDBContext dBContext = serviceRegistry.GetDBContext();


                ModerationInfo info = new()
                {
                    // We should get the ModerationID from the response, but for now we will generate a new GUID
                    Id = response.Id ?? Guid.NewGuid().ToString(),
                    EventDateTime = DateTime.Now,
                    ContentId = response.ContentId,
                    ContentName = response.ContentName ?? string.Empty,
                    ContentType = response.Type ?? string.Empty,
                    Thumbnail = response.ContentThumbnailImageUrl ?? string.Empty,
                    Report = System.Text.Encoding.UTF8.GetBytes(response.Description ?? string.Empty),
                    UserId = UserId
                };

                // Save the moderation info to the database
                dBContext.ModerationInfos.Add(info);
                await dBContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save moderation report");
                System.Windows.MessageBox.Show($"Failed to save moderation report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task SaveModerationReport(ModerationReportResponse response)
        {
            try
            {
                TailgrabDBContext dbContext = serviceRegistry.GetDBContext();
                ModerationInfo? info = dbContext.ModerationInfos.Find(response.Id);
                if (info != null)
                {
                    logger.Info($"Moderation report with ID {response.Id} already exists in the database. Skipping save.");
                    return;
                }
                else
                {
                    info = new ModerationInfo
                    {
                        // We should get the ModerationID from the response, but for now we will generate a new GUID
                        Id = response.Id ?? Guid.NewGuid().ToString(),
                        EventDateTime = DateTime.Now,
                        ContentId = response.ContentId,
                        ContentName = response.ContentName ?? string.Empty,
                        ContentType = response.Type ?? string.Empty,
                        Thumbnail = response.ContentThumbnailImageUrl ?? string.Empty,
                        Report = System.Text.Encoding.UTF8.GetBytes(response.Description ?? string.Empty),
                        UserId = convertModerationsReportTypeToUserId(response)
                    };

                    // Save the moderation info to the database
                    dbContext.ModerationInfos.Add(info);
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save moderation report");
                System.Windows.MessageBox.Show($"Failed to save moderation report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal string convertModerationsReportTypeToUserId(ModerationReportResponse response)
        {
            string userId = string.Empty;
            Tailgrab.Clients.VRChat.VRChatClient vrcClient = serviceRegistry.GetVRChatAPIClient();
            switch (response.Type)
            {
                case "avatar":
                    Result<Avatar?> result = vrcClient.GetAvatarById(response.ContentId);
                    if (result.Value != null)
                    {
                        userId = result.Value.AuthorId;
                    }
                    break;

                case "world":
                    World? world = vrcClient.GetWorldById(response.ContentId);
                    if (world != null)
                    {
                        userId = world.AuthorId;
                    }
                    break;

                case "group":
                    Result<Group?> groupResult = vrcClient.GetGroupById(response.ContentId);
                    Group? group = groupResult.Value;
                    if (group != null)
                    {
                        userId = group.OwnerId;

                    }
                    break;

                case "user":
                    userId = response.ContentId;
                    break;

                case "sticker":
                    break;

                case "emoji":
                    break;

                case "print":
                    break;

                default:
                    userId = response.ContentId;
                    break;
            }

            return userId;
        }

        public static Task<List<ModerationInfo>> GetModerationReportsByUserId(string userId)
        {
            TailgrabDBContext dbContext = serviceRegistry.GetDBContext();
            return dbContext.ModerationInfos.Where(m => m.UserId == userId).ToListAsync();
        }
        #endregion

    }



    #region Avatar Queue Classes
    #endregion

    #region Player and Event Classes
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
            Emoji,
            ModerationReport,
        }

        public DateTime EventTime { get; set; } = DateTime.Now;
        public EventType Type { get; set; } = type;
        public string EventDescription { get; set; } = eventDescription;
    }

    public class PlayerInventory(string inventoryId, string itemName, string itemUrl, string inventoryType, string aIEvaluation, string evaluatedText)
    {
        public string InventoryId { get; set; } = inventoryId;
        public string ItemName { get; set; } = itemName;
        public string ItemUrl { get; set; } = itemUrl;
        public string InventoryType { get; set; } = inventoryType;
        public string AIEvaluation { get; set; } = aIEvaluation;
        public string EvaluatedText { get; set; } = evaluatedText;

        public AlertDisplayItem AlertInfo { get; set; } = AIEvalutionEnumMapper.MapEnumToAlertDisplayItem(AIEvalutionEnumMapper.MapEvaluationToEnum(evaluatedText));
        public DateTime SpawnedAt { get; set; } = DateTime.Now;
    }

    public class PlayerPrint(string printId, string ownerId, DateTime createdAt, string printUrl, string authorName, string aiEvaluation, string aiClassification)
    {
        public string PrintId { get; set; } = printId;
        public string OwnerId { get; set; } = ownerId;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = createdAt;
        public string PrintUrl { get; set; } = printUrl;
        public string AIEvaluation { get; set; } = aiEvaluation;
        public string AIClass { get; set; } = aiClassification;
        public AlertDisplayItem AlertInfo { get; set; } = AIEvalutionEnumMapper.MapEnumToAlertDisplayItem(AIEvalutionEnumMapper.MapEvaluationToEnum(aiEvaluation));
        public string AuthorName { get; set; } = authorName;
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
        public string ProfileImage { get; set; } = string.Empty;
        public TrustClassEnum UserTrustClass { get; set; }

        public AgeVerificationEnum AgeVerified { get; set; }

        public string AlertMessage
        {
            get
            {
                string message = "";

                _AlertMessage.Sort((p1, p2) =>
                {
                    int result = p1.AlertClass.CompareTo(p2.AlertClass);  // Ascending
                    if (result == 0)
                    {
                        result = p2.AlertType.CompareTo(p1.AlertType);    // Descending
                        if (result == 0)
                        {
                            result = p1.Timestamp.CompareTo(p2.Timestamp); // Ascending
                        }
                    }
                    return result;
                });

                // @TODO: Change the AlertClass and AlertTypes to Icons
                var groupedAlerts = _AlertMessage.GroupBy(a => a.AlertClass);
                foreach (var group in groupedAlerts)
                {
                    message += $"[{group.Key}] ";

                    foreach (AlertMessage alert in group)
                    {
                        message += $"{alert.AlertType}: {alert.Message}; ";
                    }
                }

                message = message.TrimEnd(' ', ';');

                return message;
            }
        }

        /// <summary>
        /// Returns alert messages as display items with icons for use in UI
        /// </summary>
        public List<AlertDisplayItem> AlertMessages
        {
            get
            {
                List<AlertDisplayItem> displayItems = [];

                _AlertMessage.Sort((p1, p2) =>
                {
                    int result = p1.AlertClass.CompareTo(p2.AlertClass);
                    if (result == 0)
                    {
                        result = p1.AlertType.CompareTo(p2.AlertType);
                        if (result == 0)
                        {
                            result = p1.Timestamp.CompareTo(p2.Timestamp);
                        }
                    }
                    return result;
                });

                var groupedAlerts = _AlertMessage.GroupBy(a => a.AlertType);
                foreach (var group in groupedAlerts)
                {
                    // Add AlertClass icon
                    displayItems.Add(new AlertDisplayItem
                    {
                        IconGeometry = AlertTypeEnumMapper.MapEnumToIcon(group.Key),
                        IconBrush = AlertTypeEnumMapper.MapEnumToIconBrush(group.Key),
                        IconClass = group.Key.ToString(),
                        Description = String.Empty,
                        AlertColor = group.First().Color
                    });

                    foreach (AlertMessage alert in group)
                    {
                        // Add AlertType icon with message
                        displayItems.Add(new AlertDisplayItem
                        {
                            IconGeometry = AlertClassEnumMapper.GetAlertClassIcon(alert.AlertClass),
                            IconBrush = AlertClassEnumMapper.GetAlertClassIconBrush(alert.AlertClass),
                            IconClass = alert.AlertClass.ToString(),
                            Description = $"{alert.Message}",
                            AlertColor = alert.Color
                        });
                    }
                }

                return displayItems;
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
                    else if (elapsed.TotalDays >= 30)
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
        public bool IsFriend
        {
            get
            {
                return _isFriend;
            }
            set
            {
                if (value == true)
                {
                    AlertColor = "Friend";
                }
                _isFriend = value;
            }
        }

        public void AddAlertMessage(AlertClassEnum alertClass, AlertTypeEnum alertType, string message)
        {
            string alertColor = PlayerManager.GetAlertColor(alertClass, alertType);
            AlertMessage newAlert = new(alertClass, alertType, alertColor, message);
            _AlertMessage.Add(newAlert);

            foreach (AlertMessage alert in _AlertMessage)
            {
                if (alert.AlertType > MaxAlertType)
                {
                    MaxAlertType = alert.AlertType;
                    if (_isFriend == false)
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
            StringBuilder sb = new();
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
    #endregion
}
