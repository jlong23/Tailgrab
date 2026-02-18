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
        public static string UserAgent = "Tailgrab/1.1.0";
        public static Logger logger = LogManager.GetCurrentClassLogger();

        private IVRChat? _vrchat;

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

            string cookiePath = Path.Combine(Directory.GetCurrentDirectory(), "cookies.json");

            // Try to load cookies from disk and use them if they are present and not expired
            List<Cookie>? loadedCookies = LoadValidCookiesFromFile(cookiePath);

            VRChatClientBuilder builder = new VRChatClientBuilder()
                .WithApplication(name: "Tailgrab", version: "1.1.0", contact: "jlong@rabbitearsvideoproduction.com");

            if (loadedCookies != null && loadedCookies.Count > 0)
            {
                Console.WriteLine("Loaded valid cookies from disk, attempting to use them for authentication...");
                // Try to call WithCookies via reflection (some SDKs expose it)
                var withCookiesMethod = builder.GetType()
                    .GetMethods()
                    .FirstOrDefault(m => m.Name == "WithCookies" && m.GetParameters().Length == 1);

                if (withCookiesMethod != null)
                {
                    var result = withCookiesMethod.Invoke(builder, new object[] { loadedCookies });
                    if (result is VRChatClientBuilder cb)
                    {
                        builder = cb;
                    }
                    else
                    {
                        // fallback to username/password if return type not expected
                        builder = builder.WithUsername(username).WithPassword(password);
                    }
                }
                else
                {
                    // no WithCookies method; fall back to username/password
                    builder = builder.WithUsername(username).WithPassword(password);
                }
            }
            else
            {
                Console.WriteLine("No valid cookies found on disk, falling back to username/password authentication.");
                builder = builder.WithUsername(username).WithPassword(password);
            }

            _vrchat = builder.Build();

            var response = await _vrchat.Authentication.GetCurrentUserAsync();
            if (response.RequiresTwoFactorAuth.Contains("emailOtp"))
            {
                Console.WriteLine("An verification code was sent to your email address!");
                Console.Write("Enter code: ");
                //string code = Console.ReadLine();
                string code = "1234";
                var otpResponse = await _vrchat.Authentication.Verify2FAEmailCodeAsync(new TwoFactorEmailCode(code));
            }
            else if (response.RequiresTwoFactorAuth.Contains("totp"))
            {
                var totp = new Totp(Base32Encoding.ToBytes(twoFactorSecret));
                string code = totp.ComputeTotp();

                var otpResponse = await _vrchat.Authentication.Verify2FAAsync(new TwoFactorAuthCode(code));
            }

            var currentUser = await _vrchat.Authentication.GetCurrentUserAsync();
            logger.Info($"Logged in as \"{currentUser.DisplayName}\"");

            var cookies = _vrchat.GetCookies();

                SaveCookiesToFile(cookiePath, cookies);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to Log Into VRC and to save cookies': {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to Log Into VRChat Web API, check logs for details. Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private static List<Cookie>? LoadValidCookiesFromFile(string filePath)
        {
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
            List<Avatar> avatars = new List<Avatar>();
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


        public User GetProfile(string userId)
        {
            User profile = new User();
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
            List<LimitedUserGroups> groups = new List<LimitedUserGroups>();
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
                var handler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer()
                };

                var cookies = _vrchat.GetCookies();
                foreach (var cookie in cookies)
                {
                    handler.CookieContainer.Add(new Uri(URI_VRC_BASE_API), cookie);
                }

                using var httpClient = new HttpClient(handler);
                httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

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

        public async Task<ImageReference?> GetImageReference(string inventoryId, string userId, List<string> imageUrlList )
        {
            try
            {
                if (_vrchat == null)
                {
                    logger.Error("VRChat client not initialized");
                    return null;
                }

                // Create HTTP client with cookies
                var handler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer()
                };

                var cookies = _vrchat.GetCookies();
                foreach (var cookie in cookies)
                {
                    handler.CookieContainer.Add(new Uri(URI_VRC_BASE_API), cookie);
                }

                using HttpClient httpClient = new HttpClient(handler);
                httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

                // Download the image
                string md5Hash = string.Empty;
                List<string> imageList = new List<string>();
                int imageCount = 0;
                foreach ( string imageUrl in imageUrlList)
                {
                    byte[] contentBytes = await httpClient.GetByteArrayAsync(imageUrl);
                    if( imageCount == 0)
                    {
                        md5Hash = Checksum.CreateMD5(contentBytes);
                    }
                    string contentB64 = Convert.ToBase64String(contentBytes);
                    imageList.Add(contentB64);
                }

                ImageReference iref = new ImageReference
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

        public Inventory? GetInventoryInfo(string fileURL)
        {
            Inventory? printInfo = null;
            try
            {
                if (_vrchat != null)
                {
                    printInfo = _vrchat.Inventory.GetInventory();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error fetching avatar: {ex.Message}");
            }

            return printInfo;               
        }

        public List<AvatarModeration> GetAvatarModerations()
        {
            List<AvatarModeration> moderations = new List<AvatarModeration>();
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
                var handler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer()
                };

                var cookies = _vrchat.GetCookies();
                foreach (var cookie in cookies)
                {
                    handler.CookieContainer.Add(new Uri(URI_VRC_BASE_API), cookie);
                }

                using HttpClient httpClient = new HttpClient(handler);
                httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

                AvatarModerationItem rpt = new AvatarModerationItem
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
                var handler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer()
                };

                var cookies = _vrchat.GetCookies();
                foreach (var cookie in cookies)
                {
                    handler.CookieContainer.Add(new Uri(URI_VRC_BASE_API), cookie);
                }

                using HttpClient httpClient = new HttpClient(handler);
                httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

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


        private static void SaveCookiesToFile(string filePath, List<Cookie> cookies)
        {
            var dtoList = cookies.Select(c => SerializableCookie.FromCookie(c)).ToList();
            var json = JsonConvert.SerializeObject(dtoList, Formatting.Indented);
            System.IO.File.WriteAllText(filePath, json);
        }

        internal Group? getGroupById(string id)
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
                var handler = new HttpClientHandler
                {
                    CookieContainer = new CookieContainer()
                };

                var cookies = _vrchat.GetCookies();
                foreach (var cookie in cookies)
                {
                    handler.CookieContainer.Add(new Uri(URI_VRC_BASE_API), cookie);
                }

                using HttpClient httpClient = new HttpClient(handler);
                httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

                // Download the image
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
            public List<string> Collections { get; set; } = new List<string>();

            [JsonProperty("created_at")]
            public DateTime CreatedAt { get; set; }

            [JsonProperty("updated_at")]
            public DateTime UpdatedAt { get; set; }

            [JsonProperty("template_created_at")]
            public DateTime TemplateCreatedAt { get; set; }

            [JsonProperty("template_updated_at")]
            public DateTime TemplateUpdatedAt { get; set; }

            [JsonProperty("defaultAttributes")]
            public Dictionary<string, object> DefaultAttributes { get; set; } = new Dictionary<string, object>();

            [JsonProperty("userAttributes")]
            public Dictionary<string, object> UserAttributes { get; set; } = new Dictionary<string, object>();

            [JsonProperty("equipSlot")]
            public string EquipSlot { get; set; } = string.Empty;

            [JsonProperty("equipSlots")]
            public List<string> EquipSlots { get; set; } = new List<string>();

            [JsonProperty("expiryDate")]
            public DateTime? ExpiryDate { get; set; }

            [JsonProperty("flags")]
            public List<string> Flags { get; set; } = new List<string>();

            [JsonProperty("isArchived")]
            public bool IsArchived { get; set; }

            [JsonProperty("isSeen")]
            public bool IsSeen { get; set; }

            [JsonProperty("metadata")]
            public InventoryItemMetadata? Metadata { get; set; }

            [JsonProperty("quantifiable")]
            public bool Quantifiable { get; set; }

            [JsonProperty("tags")]
            public List<string> Tags { get; set; } = new List<string>();

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
            public List<ModerationReportDetails> Details { get; set; } = new List<ModerationReportDetails>();
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
            public string WorldId { get; set; } =  string.Empty;
            [JsonProperty("worldName")]
            public string WorldName { get; set; }   = string.Empty;
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
        #endregion
    }
}
