using Newtonsoft.Json;
using NLog;
using OtpNet;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Tailgrab.Clients.Ollama;
using Tailgrab.Common;
using VRChat.API.Client;
using VRChat.API.Model;

namespace Tailgrab.Clients.VRChat
{
    public class VRChatClient
    {
        private const string URI_VRC_BASE_API = "https://api.vrchat.cloud";
        private const string APP_NAME = "Tailgrab";
        private static readonly string API_VERSION = LoadVersion();
        private const string APP_CONTACT = "jlong@rabbitearsvideoproduction.com";
        private static readonly string UserAgent = $"{APP_NAME}/{API_VERSION}";
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static string LoadVersion()
        {
            try
            {
                string versionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BuildVersion.txt");
                if (System.IO.File.Exists(versionFile))
                {
                    return System.IO.File.ReadAllText(versionFile).Trim();
                }
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Warn($"Failed to load version from BuildVersion.txt: {ex.Message}");
            }
            return "1.1.3"; // Fallback version
        }

        private IVRChat? _vrchat;

        #region Authentication
        public async Task Initialize()
        {
            string? username = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_UserName);
            string? password = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_Password);
            string? twoFactorSecret = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_2FactorKey);

            // Persist cookies to disk (cookies.json) for reuse
            try
            {

                if (username is null || password is null || twoFactorSecret is null)
                {
                    System.Windows.MessageBox.Show("VR Chat Web API Credentials are not set yet, use the Config / Secrets tab to update credenials and restart Tailgrab.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                if (LoginVRChat() && _vrchat != null)
                {
                    var response = await _vrchat.Authentication.GetCurrentUserAsync();
                    if (response != null && response is not null)
                    {
                        if (response.RequiresTwoFactorAuth != null && response.RequiresTwoFactorAuth.Contains("emailOtp"))
                        {
                            logger.Warn("An verification code was sent to your email address!");
                            logger.Warn("Prompt user for code: ");

                            string code = Microsoft.VisualBasic.Interaction.InputBox("Please enter EMail OTP code (6 digits)");
                            var otpResponse = await _vrchat.Authentication.Verify2FAEmailCodeAsync(new TwoFactorEmailCode(code));
                        }
                        else if (response.RequiresTwoFactorAuth != null && response.RequiresTwoFactorAuth.Contains("totp"))
                        {
                            var totp = new Totp(Base32Encoding.ToBytes(twoFactorSecret));
                            string code = totp.ComputeTotp();

                            var otpResponse = await _vrchat.Authentication.Verify2FAAsync(new TwoFactorAuthCode(code));
                        }

                        var currentUser = await _vrchat.Authentication.GetCurrentUserAsync();
                        logger.Info($"Logged in as \"{currentUser.DisplayName}\"");

                        var cookies = _vrchat.GetCookies();
                        PersistCookies(cookies);
                    }
                } else
                {
                    logger.Warn("Unable to login to VRChat ");
                    System.Windows.MessageBox.Show("VR Chat Web API failed to log in, check the log file and restart Tailgrab.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to Log Into VRC and to save cookies': {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to Log Into VRChat Web API, check logs for details. Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private bool LoginVRChat()
        {
            // Try to load cookies from disk and use them if they are present and not expired
            List<Cookie>? loadedCookies = LoadCookies();

            VRChatClientBuilder builder = new VRChatClientBuilder()
                .WithApplication(name: APP_NAME, version: API_VERSION, contact: APP_CONTACT);

            if (loadedCookies != null && loadedCookies.Count > 0)
            {
                logger.Info("Loaded valid cookies from disk, attempting to use them for authentication...");

                string authCookieValue = string.Empty;
                string twoFactorCookieValue = string.Empty;
                foreach (var cookie in loadedCookies)
                {
                    if (cookie.Name == "auth")
                    {
                        authCookieValue = cookie.Value;
                    }
                    else if (cookie.Name == "twoFactorAuth")
                    {
                        twoFactorCookieValue = cookie.Value;
                    }
                }
                _vrchat = builder.WithAuthCookie(authCookieValue, twoFactorCookieValue).Build();
                return true;
            }
            else
            {
                string? username = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_UserName);
                string? password = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_Password);
                if (username != null && password != null)
                {
                    logger.Info("No valid cookies found on disk, falling back to username/password authentication.");
                    _vrchat = builder.WithUsername(username).WithPassword(password).Build();
                    return true;
                }
            }

            return false;
        }

        #endregion

        public List<AvatarModeration> GetAvatarModerations()
        {
            List<AvatarModeration> moderations = [];
            try
            {
                if (_vrchat != null)
                {
                    moderations = _vrchat.Authentication.GetGlobalAvatarModerations();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching avatar moderations: {ex.Message}");
            }
            return moderations;
        }

        public async Task<bool> BlockAvatarGlobal(string avatarId)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Info($"Failed Block avatar {avatarId} globally, not logged in.");
                    return false;
                }

                // Create HTTP client with cookies
                using HttpClient httpClient = CreateHttpClientWithCookies();

                AvatarModerationItem rpt = new()
                {
                    TargetAvatarId = avatarId,
                    AvatarModerationType = "block"
                };

                HttpResponseMessage response = await httpClient.PostAsJsonAsync($"{URI_VRC_BASE_API}/api/1/auth/user/avatarmoderations?targetAvatarId={avatarId}&avatarModerationType=block", rpt);
                string responseContent = await response.Content.ReadAsStringAsync();
                logger.Debug($"Response from Block avatar {avatarId} globally: {responseContent}");
                logger.Info($"Submitted Block avatar {avatarId} globally.");
                response.EnsureSuccessStatusCode();

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting avatar moderation status: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> DeleteAvatarGlobal(string avatarId)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Info($"Failed Unblock avatar {avatarId} globally, not logged in.");
                    return false;
                }

                // Create HTTP client with cookies
                using HttpClient httpClient = CreateHttpClientWithCookies();

                HttpResponseMessage response = await httpClient.DeleteAsync($"{URI_VRC_BASE_API}/api/1/auth/user/avatarmoderations?targetAvatarId={avatarId}&avatarModerationType=block");
                string responseContent = await response.Content.ReadAsStringAsync();
                logger.Debug($"Response from Block avatar {avatarId} globally: {responseContent}");
                logger.Info($"Submitted Block avatar {avatarId} globally.");
                response.EnsureSuccessStatusCode();

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting avatar moderation status: {ex.Message}");
            }

            return false;
        }


        #region Avatar Management
        public Avatar? GetAvatarById(string avatarId)
        {
            Avatar? avatar = null;
            try
            {
                if (_vrchat != null)
                {
                    avatar = _vrchat.Avatars.GetAvatar(avatarId);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching avatar: {ex.Message}");
            }

            return avatar;
        }

        public List<Avatar> GetAvatarsByUserId(string userId)
        {
            List<Avatar> avatars = [];
            try
            {
                if (_vrchat != null)
                {
                    avatars = _vrchat.Avatars.SearchAvatars(sort: SortOption.Order, order: OrderOption.Descending, userId: userId, tag: "avatargallery");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching avatar: {ex.Message}");
            }

            return avatars;
        }
        #endregion

        #region Profile Management
        public User GetProfile(string userId)
        {
            User profile = new ();
            try
            {
                if (_vrchat != null)
                {
                    profile = _vrchat.Users.GetUser(userId);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching User Profile: {ex.Message}");
            }

            return profile;
        }

        public List<LimitedUserGroups> GetProfileGroups(string userId)
        {
            List<LimitedUserGroups> groups = [];
            try
            {
                if (_vrchat != null)
                {
                    groups = _vrchat.Users.GetUserGroups(userId);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching User's Groups: {ex.Message}");
            }

            return groups;
        }

        public async Task<VRChatInventoryItem?> GetUserInventoryItem(string userId, string itemId)
        {
            VRChatInventoryItem? item = null;
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return null;
                }

                string url = $"{URI_VRC_BASE_API}/api/1/user/{userId}/inventory/{itemId}";

                // Create HTTP client with cookies
                using HttpClient httpClient = CreateHttpClientWithCookies();

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                item = JsonConvert.DeserializeObject<VRChatInventoryItem>(json);

                if (item != null)
                {
                    logger.Info($"Fetched inventory item: {item.Name} ({item.ItemType}) for user {userId}");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching inventory item {itemId} for user {userId}: {ex.Message}");
            }

            return item;
        }
        #endregion

        #region Image Assets
        public async Task<ImageReference?> GetImageReference(string inventoryId, string userId, List<string> imageUrlList)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return null;
                }

                // Create HTTP client with cookies
                using HttpClient httpClient = CreateHttpClientWithCookies();

                // Download the image
                string md5Hash = string.Empty;
                List<string> imageList = [];
                int imageCount = 0;
                foreach (string imageUrl in imageUrlList)
                {
                    byte[] contentBytes = await httpClient.GetByteArrayAsync(imageUrl);
                    if (imageCount == 0)
                    {
                        md5Hash = Checksum.CreateMD5(contentBytes);
                    }
                    string contentB64 = Convert.ToBase64String(contentBytes);
                    imageList.Add(contentB64);
                }

                ImageReference iref = new ()
                {
                    Base64Data = imageList,
                    Md5Hash = md5Hash,
                    InventoryId = inventoryId,
                    UserId = userId
                };

                return iref;

            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error Downloading image from URI: {imageUrlList}");
                return null;
            }
        }
        #endregion

        #region Print Management
        public Print? GetPrintInfo(string fileURL)
        {
            Print? printInfo = null;
            try
            {
                if (_vrchat != null)
                {
                    printInfo = _vrchat.Prints.GetPrint(fileURL);
                    logger.Info($"Fetched print info: {printInfo?.Id} by {printInfo?.AuthorName}");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching avatar: {ex.Message}");
            }

            return printInfo;
        }
        #endregion

        #region Group Management
        internal Group? GetGroupById(string id)
        {
            Group? group = null;
            try
            {
                if (_vrchat != null)
                {
                    group = _vrchat.Groups.GetGroup(id);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching Group information: {ex.Message}");
            }

            return group;
        }

        public async Task<TGGroupMemberStatus> GetGroupMemberStatus(string groupId, string userId)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return TGGroupMemberStatus.Unknown;
                }

                GroupLimitedMember membership = _vrchat.Groups.GetGroupMember(groupId, userId);
                logger.Info($"Checking group {groupId} member status for user {userId}");

                if( membership != null && membership.MembershipStatus != null)
                {
                    if (membership.MembershipStatus == GroupMemberStatus.Banned)
                    {
                        return TGGroupMemberStatus.Banned;
                    }
                    else if( membership.MembershipStatus == GroupMemberStatus.Member)
                    {
                        return TGGroupMemberStatus.Member;
                    }
                }
                return TGGroupMemberStatus.NotMember;
            }
            catch (Exception ex)
            {
                logger.Error($"Error checking group member status: {ex.Message}");
                return TGGroupMemberStatus.Unknown;
            }
        }

        public async Task<bool> BanUserFromGroup(string groupId, string userId)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return false;
                }

                UserIdPayload request = new UserIdPayload { UserId = userId };
                // Create HTTP client with cookies
                using HttpClient httpClient = CreateHttpClientWithCookies();

                // Submit the moderation report
                HttpResponseMessage response = await httpClient.PostAsJsonAsync($"{URI_VRC_BASE_API}/api/1/groups/{groupId}/bans", request);
                string responseContent = await response.Content.ReadAsStringAsync();
                logger.Debug($"Response from submitting moderation report for content: {responseContent}");
                logger.Info($"Banning user {userId} from group {groupId}");
                response.EnsureSuccessStatusCode();

                return response.IsSuccessStatusCode;

            }
            catch (Exception ex)
            {
                logger.Error($"Error banning user from group: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UnbanUserFromGroup(string groupId, string userId)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return false;
                }

                _vrchat.Groups.UnbanGroupMember(groupId, userId);
                logger.Info($"Unbanning user {userId} from group {groupId}");

                return true;
            }
            catch (Exception ex)
            {
                logger.Error($"Error unbanning user from group: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Moderation Management
        internal async Task<bool> SubmitModerationReportAsync(ModerationReportPayload rpt)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return false;
                }

                // Create HTTP client with cookies
                using HttpClient httpClient = CreateHttpClientWithCookies();

                // Submit the moderation report
                HttpResponseMessage response = await httpClient.PostAsJsonAsync($"{URI_VRC_BASE_API}/api/1/moderationReports", rpt);
                string responseContent = await response.Content.ReadAsStringAsync();
                logger.Debug($"Response from submitting moderation report for content {rpt.ContentId}: {responseContent}");
                logger.Info($"Submitted moderation report for content {rpt.ContentId} with reason: {rpt.Reason}\n{responseContent}");
                response.EnsureSuccessStatusCode();

                return response.IsSuccessStatusCode;

            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error Reporting image from URI: {rpt}");
                return false;
            }
        }
        #endregion

        #region Cookie Persistence
        private static List<Cookie>? LoadCookies()
        {
            string filePath = Path.Combine(CommonConst.APPLICATION_LOCAL_DATA_PATH, "cookies.json");

            if (!System.IO.File.Exists(filePath))
                return null;

            try
            {
                var json = System.IO.File.ReadAllText(filePath);
                var dtoList = JsonConvert.DeserializeObject<List<SerializableCookie>>(json);
                if (dtoList == null || dtoList.Count == 0)
                    return null;

                // If any cookie has an Expires value set and is expired, treat the whole set as invalid
                DateTime now = DateTime.UtcNow;
                foreach (var dto in dtoList)
                {
                    logger.Debug($"Loaded cookie: {dto.Name}, Expires: {dto.Expires}");

                    if (dto.Expires != DateTime.MinValue && dto.Expires.ToUniversalTime() <= now)
                    {
                        return null;
                    }
                }

                var cookies = dtoList.Select(d => d.ToCookie()).ToList();
                return cookies;
            }
            catch
            {
                return null;
            }
        }

        private static void PersistCookies(List<Cookie> cookies)
        {
            string filePath = Path.Combine(CommonConst.APPLICATION_LOCAL_DATA_PATH, "cookies.json");

            var dtoList = cookies.Select(c => SerializableCookie.FromCookie(c)).ToList();
            var json = JsonConvert.SerializeObject(dtoList, Formatting.Indented);
            System.IO.File.WriteAllText(filePath, json);
        }

        private HttpClient CreateHttpClientWithCookies()
        {
            if (_vrchat == null)
            {
                logger.Error("VRChat client not initialized, cannot create HTTP client with cookies.");
                throw new InvalidOperationException("VRChat client not initialized");
            }

            var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer()
            };

            var cookies = _vrchat.GetCookies();
            foreach (var cookie in cookies)
            {
                handler.CookieContainer.Add(new Uri(URI_VRC_BASE_API), cookie);
            }
            HttpClient httpClient = new(handler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

            return httpClient;
        }
        #endregion

        #region Non Public Helper Types
        private class SerializableCookie
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public string Domain { get; set; } = string.Empty;
            public string Path { get; set; } = "/";
            public DateTime Expires { get; set; } = DateTime.MinValue;
            public bool Secure { get; set; }
            public bool HttpOnly { get; set; }

            public Cookie ToCookie()
            {
                var cookie = new Cookie(Name, Value, Path, Domain)
                {
                    Secure = Secure,
                    HttpOnly = HttpOnly
                };

                if (Expires != DateTime.MinValue)
                {
                    cookie.Expires = Expires;
                }

                return cookie;
            }

            public static SerializableCookie FromCookie(Cookie c)
            {
                return new SerializableCookie
                {
                    Name = c.Name,
                    Value = c.Value,
                    Domain = c.Domain ?? string.Empty,
                    Path = c.Path ?? "/",
                    Expires = c.Expires,
                    Secure = c.Secure,
                    HttpOnly = c.HttpOnly
                };
            }
        }
        #endregion

        #region Non Public JSON Serializable Types
        public class AvatarModerationItem
        {
            [JsonProperty("avatarModerationType")]
            public string AvatarModerationType { get; set; } = "block";

            [JsonProperty("targetAvatarId")]
            public string TargetAvatarId { get; set; } = string.Empty;
        }

        public class VRChatInventoryItem
        {
            [JsonProperty("id")]
            public string Id { get; set; } = string.Empty;

            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;

            [JsonProperty("description")]
            public string Description { get; set; } = string.Empty;

            [JsonProperty("itemType")]
            public string ItemType { get; set; } = string.Empty;

            [JsonProperty("itemTypeLabel")]
            public string ItemTypeLabel { get; set; } = string.Empty;

            [JsonProperty("imageUrl")]
            public string ImageUrl { get; set; } = string.Empty;

            [JsonProperty("holderId")]
            public string HolderId { get; set; } = string.Empty;

            [JsonProperty("ancestor")]
            public string Ancestor { get; set; } = string.Empty;

            [JsonProperty("ancestorHolderId")]
            public string AncestorHolderId { get; set; } = string.Empty;

            [JsonProperty("firstAncestor")]
            public string FirstAncestor { get; set; } = string.Empty;

            [JsonProperty("firstAncestorHolderId")]
            public string FirstAncestorHolderId { get; set; } = string.Empty;

            [JsonProperty("collections")]
            public List<string> Collections { get; set; } = [];

            [JsonProperty("created_at")]
            public DateTime CreatedAt { get; set; }

            [JsonProperty("updated_at")]
            public DateTime UpdatedAt { get; set; }

            [JsonProperty("template_created_at")]
            public DateTime TemplateCreatedAt { get; set; }

            [JsonProperty("template_updated_at")]
            public DateTime TemplateUpdatedAt { get; set; }

            [JsonProperty("defaultAttributes")]
            public Dictionary<string, object> DefaultAttributes { get; set; } = [];

            [JsonProperty("userAttributes")]
            public Dictionary<string, object> UserAttributes { get; set; } = [];

            [JsonProperty("equipSlot")]
            public string EquipSlot { get; set; } = string.Empty;

            [JsonProperty("equipSlots")]
            public List<string> EquipSlots { get; set; } = [];

            [JsonProperty("expiryDate")]
            public DateTime? ExpiryDate { get; set; }

            [JsonProperty("flags")]
            public List<string> Flags { get; set; } = [];

            [JsonProperty("isArchived")]
            public bool IsArchived { get; set; }

            [JsonProperty("isSeen")]
            public bool IsSeen { get; set; }

            [JsonProperty("metadata")]
            public InventoryItemMetadata? Metadata { get; set; }

            [JsonProperty("quantifiable")]
            public bool Quantifiable { get; set; }

            [JsonProperty("tags")]
            public List<string> Tags { get; set; } = [];

            [JsonProperty("templateId")]
            public string TemplateId { get; set; } = string.Empty;

            [JsonProperty("validateUserAttributes")]
            public bool ValidateUserAttributes { get; set; }
        }

        public class InventoryItemMetadata
        {
            [JsonProperty("animated")]
            public bool Animated { get; set; }

            [JsonProperty("animationStyle")]
            public string AnimationStyle { get; set; } = string.Empty;

            [JsonProperty("fileId")]
            public string FileId { get; set; } = string.Empty;

            [JsonProperty("imageUrl")]
            public string ImageUrl { get; set; } = string.Empty;

            [JsonProperty("maskTag")]
            public string MaskTag { get; set; } = string.Empty;
        }

        public class ModerationReportPayload
        {
            [JsonProperty("type")]
            public string Type { get; set; } = string.Empty;

            [JsonProperty("category")]
            public string Category { get; set; } = string.Empty;

            [JsonProperty("reason")]
            public string Reason { get; set; } = string.Empty;

            [JsonProperty("contentId")]
            public string ContentId { get; set; } = string.Empty;

            [JsonProperty("description")]
            public string Description { get; set; } = string.Empty;

            [JsonProperty("details")]
            public List<ModerationReportDetails> Details { get; set; } = [];
        }

        public class ModerationReportDetails
        {
            [JsonProperty("instanceType")]
            public string InstanceType { get; set; } = string.Empty;

            [JsonProperty("instanceAgeGated")]
            public bool InstanceAgeGated { get; set; }

            [JsonProperty("userInSameInstance")]
            public bool UserInSameInstance { get; set; } = true;

            [JsonProperty("holderId")]
            public string HolderId { get; set; } = string.Empty;
        }

        public class PrintInfo
        {
            [JsonProperty("authorId")]
            public string AuthorId { get; set; } = string.Empty;
            [JsonProperty("authorName")]
            public string AuthorName { get; set; } = string.Empty;
            [JsonProperty("id")]
            public string Id { get; set; } = string.Empty;
            [JsonProperty("createdAt")]
            public string CreatedAt { get; set; } = string.Empty;
            [JsonProperty("note")]
            public string Note { get; set; } = string.Empty;
            [JsonProperty("ownerId")]
            public string OwnerId { get; set; } = string.Empty;
            [JsonProperty("timestamp")]
            public string Timestamp { get; set; } = string.Empty;
            [JsonProperty("worldId")]
            public string WorldId { get; set; } = string.Empty;
            [JsonProperty("worldName")]
            public string WorldName { get; set; } = string.Empty;
            [JsonProperty("files")]
            public PrintFileInfo FileInfo { get; set; } = new PrintFileInfo();
        }

        public class PrintFileInfo
        {
            [JsonProperty("fileId")]
            public string FileId { get; set; } = string.Empty;
            [JsonProperty("image")]
            public string ImageUrl { get; set; } = string.Empty;
        }

        public class UserIdPayload
        {
            [JsonProperty("userId")]
            public string UserId { get; set; } = string.Empty;
        }   

        public enum TGGroupMemberStatus
        {
            Unknown,
            NotMember,
            Member,
            Banned
        }
        #endregion
    }
}
