using Newtonsoft.Json;
using NLog;
using OtpNet;
using System.IO;
using System.Net;
using Tailgrab.Config;
using VRChat.API.Model;
using VRChat.API.Client;


namespace Tailgrab.Clients.VRChat
{
    public class VRChatClient
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();

        private IVRChat? _vrchat;

        public async void Initialize()
        {
            string? username = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_VRChat_Web_UserName);
            string? password = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_VRChat_Web_Password);
            string? twoFactorSecret = ConfigStore.LoadSecret(Tailgrab.Common.Common.Registry_VRChat_Web_2FactorKey);

            if (username is null || password is null || twoFactorSecret is null)
            {
                System.Windows.MessageBox.Show("VR Chat Web API Credentials are not set yet, use the Config / Secrets tab to update credenials and restart Tailgrab.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            string cookiePath = Path.Combine(Directory.GetCurrentDirectory(), "cookies.json");

            // Try to load cookies from disk and use them if they are present and not expired
            List<Cookie>? loadedCookies = LoadValidCookiesFromFile(cookiePath);

            VRChatClientBuilder builder = new VRChatClientBuilder()
                .WithApplication(name: "Jarvis", version: "1.0.0", contact: "jlong@rabbitearsvideoproduction.com");

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
            Console.WriteLine($"Logged in as \"{currentUser.DisplayName}\"");

            var cookies = _vrchat.GetCookies();

            // Persist cookies to disk (cookies.json) for reuse
            try
            {
                SaveCookiesToFile(cookiePath, cookies);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save cookies to '{cookiePath}': {ex.Message}");
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
    }
}
