using Newtonsoft.Json;
using NLog;
using OtpNet;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            return "1.1.6"; // Fallback version
        }

        private IVRChat? _vrchat;

        #region Authentication
        [STAThread]
        public async Task<bool> Initialize()
        {
            string? username = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_UserName);
            string? password = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_Password);
            string? twoFactorSecret = ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_2FactorKey);

            // Persist cookies to disk (cookies.json) for reuse
            try
            {
                if (username is null || password is null )
                {
                    System.Windows.MessageBox.Show("VR Chat Web API Credentials are not set yet, use the Config / Secrets tab to update credenials and restart Tailgrab.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return false;
                }

                if (LoginVRChat() && _vrchat != null)
                {
                    var response = await _vrchat.Authentication.GetCurrentUserAsync();
                    if (response != null && response is not null)
                    {
                        if( response.RequiresTwoFactorAuth != null && response.RequiresTwoFactorAuth.Count > 0)
                        {
                            logger.Warn($"2FA is required for this account. Style : {string.Join(", ", response.RequiresTwoFactorAuth)}");
                        }

                        if (response.RequiresTwoFactorAuth != null && response.RequiresTwoFactorAuth.Contains("emailOtp"))
                        {
                            logger.Warn("An verification code was sent to your email address!");
                            logger.Warn("Prompt user for code: ");

                            string? code = NativeOtpDialog.PromptForOtpCode("Please enter Email OTP code (6 digits)");

                            if (!string.IsNullOrEmpty(code))
                            {
                                var otpResponse = await _vrchat.Authentication.Verify2FAEmailCodeAsync(new TwoFactorEmailCode(code));
                            }
                        }
                        else if (response.RequiresTwoFactorAuth != null && response.RequiresTwoFactorAuth.Contains("totp"))
                        {
                            string code = string.Empty;
                            if (string.IsNullOrEmpty(twoFactorSecret))
                            {
                                logger.Error("2FA secret is not set, Prompting user for code.");
                                code = NativeOtpDialog.PromptForOtpCode("Please enter Authenticator OTP code (6 digits)") ?? string.Empty;
                            } 
                            else
                            {
                                var totp = new Totp(Base32Encoding.ToBytes(twoFactorSecret));
                                code = totp.ComputeTotp();
                            }

                            if (!string.IsNullOrEmpty(code))
                            {
                                var otpResponse = await _vrchat.Authentication.Verify2FAAsync(new TwoFactorAuthCode(code));
                            }
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
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to Log Into VRC and to save cookies': {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to Log Into VRChat Web API, check logs for details. Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }

            return true;
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

        #region World Management
        public World? GetWorldById(string worldId)
        {
            World? world = null;
            try
            {
                if (_vrchat != null)
                {
                    world = _vrchat.Worlds.GetWorld(worldId);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in GetWorldById for world '{worldId}': {ex.Message}");
            }

            return world;
        }

        public async Task<World?> GetWorldInfo(string worldId)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return null;
                }

                World world = _vrchat.Worlds.GetWorld(worldId);
                if (world == null)
                {
                    logger.Warn($"World with ID {worldId} not found.");
                    return null;
                }


                return world;
            }
            catch (Exception ex)
            {
                logger.Error($"Error getting world info for {worldId}: {ex.Message}");
                return null;
            }
        }
        #endregion

        #region Avatar Management
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

                CreateAvatarModerationRequest request = new CreateAvatarModerationRequest
                {
                    TargetAvatarId = avatarId,
                    AvatarModerationType = AvatarModerationType.Block
                };

                AvatarModerationCreated created = await _vrchat.Authentication.CreateGlobalAvatarModerationAsync(request);

                logger.Info($"Submitted Block avatar {avatarId} globally.");
                return created != null;
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

                OkStatus2 status = _vrchat.Authentication.DeleteGlobalAvatarModeration(avatarId, AvatarModerationType.Block);

                return status != null && status.OK is string;
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting avatar moderation status: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> ChangeIntoAvatar(string avatarId)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Info($"Failed to switch into avatar {avatarId}, not logged in.");
                    return false;
                }

                CurrentUser currentUser = _vrchat.Avatars.SelectAvatar(avatarId);

                return currentUser != null;
            }
            catch (Exception ex)
            {
                logger.Error($"Error switching into avatar {avatarId}: {ex.Message}");
            }
            return false;
        }
        public Result<Avatar?> GetAvatarById(string avatarId)
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
                logger.Error($"Error in GetAvatarById for avatar '{avatarId}': {ex.Message}");
                return new Result<Avatar?> { Exception = ex };
            }

            return new Result<Avatar?> { Value = avatar };
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
                logger.Error($"Error in GetAvatarsByUserId for user '{userId}': {ex.Message}");
            }

            return avatars;
        }
        #endregion

        #region Profile Management

        internal Dictionary<string, VRChatUserProfileEntry> _userProfileCache = new();

        internal int CacheUserTimeoutSeconds { get; set; } = 900; // Default cache timeout of 15 minutes

        internal VRChatUserProfileEntry? GetCachedUserProfile(string userId)
        {
            bool valueInCache = _userProfileCache.TryGetValue(userId, out var cachedProfile);
            if (valueInCache && cachedProfile != null)
            {
                // Refresh the cache if the entry is older than the timeout
                if ((DateTime.UtcNow - cachedProfile.LastFetched).TotalSeconds > CacheUserTimeoutSeconds)
                {
                    logger.Info($"Cached profile for user {userId} has expired, removing from cache.");
                    _userProfileCache.Remove(userId);
                }
                else
                {
                    logger.Info($"Returning cached profile for user {userId}.");
                    return cachedProfile;
                }
            }

            if (_vrchat == null)
            {
                logger.Error("VRChat client not initialized, cannot fetch user profile.");
                return null;
            }

            User user = _vrchat.Users.GetUser(userId);
            if (user != null)
            {
                PublicProfile profile = _vrchat.Users.GetPublicProfile(userId);
                List<LimitedUserGroups> userGroups = _vrchat.Users.GetUserGroups(userId);

                var newProfileEntry = new VRChatUserProfileEntry
                {
                    UserId = userId,
                    DisplayName = user.DisplayName,
                    Bio = profile.Bio,
                    StatusDescription = string.IsNullOrEmpty(user.StatusDescription) ? string.Empty : user.StatusDescription,
                    Pronouns = profile.Pronouns,
                    ProfileIconUrl = profile.IconUrl,
                    ProfileBannerUrl = profile.BannerUrl,
                    JoinDate = user.DateJoined,
                    IsFriend = user.IsFriend,
                    IsFriendRequesting = user.FriendRequestStatus != null,
                    AgeVerified = profile.AgeVerified,
                    AgeVerificationStatus = profile.AgeVerificationStatus,
                    Tags = user.Tags,
                    GroupMemberships = userGroups,
                    LastFetched = DateTime.UtcNow
                };

                logger.Info($"Caching and Returning profile for user {userId}.");
                _userProfileCache[userId] = newProfileEntry;
                return newProfileEntry;
            }

            return null;
        }

        internal void ClearUserProfileCache()
        {
            foreach ( VRChatUserProfileEntry entry in _userProfileCache.Values)
            {
                if ((DateTime.UtcNow - entry.LastFetched).TotalSeconds > CacheUserTimeoutSeconds)
                {
                    logger.Info($"Cached profile for user {entry.UserId} has expired, removing from cache.");
                    _userProfileCache.Remove(entry.UserId);
                }
            }
        }

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

        public PublicProfile GetProfilePublic(string userId)
        {
            PublicProfile profile = new();
            try
            {
                if (_vrchat != null)
                {
                    profile = _vrchat.Users.GetPublicProfile(userId);
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
        #endregion

        #region Inventory Management
        public async Task<InventoryItem?> GetUserInventoryItem(string userId, string itemId)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return null;
                }

                InventoryItem item = _vrchat.Inventory.GetUserInventoryItem(userId, itemId);
                return item;
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching inventory item {itemId} for user {userId}: {ex.Message}");
            }

            return null;
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

                // Download and normalize the image
                string md5Hash = string.Empty;
                List<string> imageList = [];
                foreach (string imageUrl in imageUrlList)
                {
                    byte[] contentBytes = await httpClient.GetByteArrayAsync(imageUrl);
                    //byte[] scaledContentBytes = ScaleImageToMaxSize(contentBytes, 512, 512);
                    if (contentBytes.Length > 0)
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

        public async Task<List<string>> DownloadContentUrls(List<string> imageUrlList)
        {
            List<string> imageList = new List<string>();
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return imageList;
                }

                // Create HTTP client with cookies
                using HttpClient httpClient = CreateHttpClientWithCookies();

                // Download and normalize the image
                foreach (string imageUrl in imageUrlList)
                {
                    byte[] contentBytes = await httpClient.GetByteArrayAsync(imageUrl);
                    byte[] scaledContentBytes = ScaleImageToMaxSize(contentBytes, 512, 512);
                    string contentB64 = Convert.ToBase64String(scaledContentBytes);
                    imageList.Add(contentB64);
                }

                return imageList;

            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error Downloading file from URI: {imageUrlList}");
                return imageList;
            }
        }

        private static byte[] ScaleImageToMaxSize(byte[] imageBytes, int maxWidth, int maxHeight)
        {
            if (imageBytes.Length == 0)
            {
                return imageBytes;
            }

            using MemoryStream inputStream = new(imageBytes);
            BitmapDecoder decoder = BitmapDecoder.Create(inputStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource source = decoder.Frames[0];

            if (source.PixelWidth <= maxWidth && source.PixelHeight <= maxHeight)
            {
                return imageBytes;
            }

            double scaleX = (double)maxWidth / source.PixelWidth;
            double scaleY = (double)maxHeight / source.PixelHeight;
            double scale = Math.Min(scaleX, scaleY);

            TransformedBitmap scaledBitmap = new(source, new ScaleTransform(scale, scale));
            BitmapFrame frame = BitmapFrame.Create(scaledBitmap);

            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(frame);

            using MemoryStream outputStream = new();
            encoder.Save(outputStream);
            return outputStream.ToArray();
        }
        #endregion

        #region Print Management
        public Print? GetPrintInfo(string fileURL)
        {
            try
            {
                if (_vrchat != null)
                {
                    Print printInfo = _vrchat.Prints.GetPrint(fileURL);
                    logger.Info($"Fetched print info: {printInfo?.Id} by {printInfo?.AuthorName}");
                    return printInfo;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching avatar: {ex.Message}");
            }

            return null;
        }
        #endregion

        #region Group Management
        internal Result<Group?> GetGroupById(string id)
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
                logger.Error($"Error fetching Group information for group '{id}': {ex.Message}");
                return new Result<Group?> { Exception = ex };
            }

            return new Result<Group?> { Value = group };
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

                // Older version of the VRChat Library gave GroupLimitedMember
                // GroupLimitedMember membership = _vrchat.Groups.GetGroupMember(groupId, userId);
                GroupMember membership = _vrchat.Groups.GetGroupMember(groupId, userId);
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

                BanGroupMemberRequest request = new BanGroupMemberRequest
                {
                    UserId = userId
                };

                GroupMember member = _vrchat.Groups.BanGroupMember(groupId, request);
                logger.Info($"Banning user {userId} from group {groupId}");

                return member != null && member.MembershipStatus == GroupMemberStatus.Banned;
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
        internal async Task<bool> DeleteModerationReportAsync(string rptId)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return false;
                }

                if( string.IsNullOrEmpty(rptId))
                {
                    logger.Error("Report ID is null or empty, cannot delete moderation report.");
                    return false;
                }   

                SuccessFlag success = _vrchat.Authentication.DeleteModerationReport(rptId);

                return success != null && success.Success;

            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error Deleting moderation report with ID: {rptId}");
                return false;
            }
        }

        internal async Task<ModerationReport?> SubmitModerationReportAsync(SubmitModerationReportRequest report)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return null;
                }

                ModerationReport reported = await _vrchat.Authentication.SubmitModerationReportAsync(report);

                return reported;

            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error Reporting image from URI: {report}");
                return null;
            }
        }

        internal async Task<PaginatedModerationReportList?> ListModerationReportAsync(int offset, bool isClosed)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return null;
                }

                PaginatedModerationReportList reportList = 
                    await _vrchat.Authentication.GetModerationReportsAsync( offset: offset, status: isClosed ? "closed" : "open");

                return reportList;

            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error Listing moderation reports with offset: {offset}");
                return null;
            }
        }

        internal async Task<bool> PersonalModeration(string userId, PlayerModerationType type )
        {
            bool result = false;
            if (_vrchat == null)
            {
                logger.Error("VRChat client not initialized");
                return result;
            }

            try
            {
                ModerateUserRequest request = new ModerateUserRequest
                {
                    Moderated = userId,
                    Type = type
                };

                PlayerModeration moderation = _vrchat.Moderations.ModerateUser( request );
                result = moderation != null && moderation.SourceUserId == userId;

            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error Personal Moderation for user: {userId}");
            }

            return result;
        }
        #endregion

        #region User Management
        public async Task<string?> SearchUserByDisplayName(string displayName)
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return null;
                }

                List<LimitedUserSearch> results = _vrchat.Users.SearchUsers(displayName);

                foreach (LimitedUserSearch search in results)
                {
                    if(search.DisplayName.Equals(displayName, StringComparison.Ordinal))
                    {
                        logger.Info($"Found user with display name: {displayName}");
                        return search.Id;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Error($"Error checking if user {displayName} is in the same instance: {ex.Message}");
                return null;
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

        public void DeleteCookies()
        {
            string filePath = Path.Combine(CommonConst.APPLICATION_LOCAL_DATA_PATH, "cookies.json");

            if (!System.IO.File.Exists(filePath))
                return;


            try
            {
                System.IO.File.Delete(filePath);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to delete cookies: {ex.Message}");
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

        public enum TGGroupMemberStatus
        {
            Unknown,
            NotMember,
            Member,
            Banned
        }

        public class Result<T>
        {
            public T? Value { get; set; }
            public Exception? Exception { get; set; }
            public bool HasException => Exception != null;

        }
        #endregion

        #region Cached Data Structures
        public class VRChatUserProfileEntry
        {
            public string UserId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Bio { get; set; } = string.Empty;
            public string StatusDescription { get; set; } = string.Empty;
            public string Pronouns { get; set; } = string.Empty;
            public string ProfileIconUrl { get; set; } = string.Empty;
            public string ProfileBannerUrl { get; set; } = string.Empty;
            public DateOnly JoinDate { get; set; }
            public bool IsFriend { get; set; } = false;
            public bool IsFriendRequesting { get; set; } = false;
            public bool AgeVerified { get; set; } = false;
            public AgeVerificationStatus? AgeVerificationStatus { get; set; }
            public List<LimitedUserGroups> GroupMemberships { get; set; } = new List<LimitedUserGroups>();
            public List<string> Tags { get; set; } = new List<string>();
            public DateTime LastFetched { get; set; } = DateTime.UtcNow;

            public string TrustClassString
            {
                get
                {
                    return TrustClassEnumMapper.MapTagsToString(Tags, AgeVerified, AgeVerificationStatus?.ToString() ?? string.Empty);
                }
            }

            public string ProfileTextFormated
            {
                get
                {
                    return $"DisplayName: {DisplayName}\n" +
                           $"StatusDesc: {StatusDescription}\n" +
                           $"Pronouns: {Pronouns}\n" +
                           $"UserTrust : {TrustClassEnumMapper.MapTagsToString(Tags, AgeVerified, AgeVerificationStatus?.ToString() ?? string.Empty)}\n" +
                           $"UserAgeVerified: {AgeVerified}\n" +
                           $"ProfileBio: {Bio}\n";
                }
            }
        }
        #endregion
    }
}
