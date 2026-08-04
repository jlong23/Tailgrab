using NLog;
using System.IO;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace tailgrab.Clients.VRCDB
{
    public class VRCDBClient
    {
        private const string URI_VRC_BASE_API = "https://api.avtrdb.com/v3";
        private const string URI_VRC_AVATAR_SEARCH = "/avatar/search/vrcx";
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

        public async Task<List<AvatarItem>> GetAvatarsByAuthorAsync(string authorId)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
                httpClient.DefaultRequestHeaders.Add("Contact", APP_CONTACT);
                string requestUri = $"{URI_VRC_BASE_API}{URI_VRC_AVATAR_SEARCH}?authorId={authorId}";
                try
                {
                    var response = await httpClient.GetAsync(requestUri);
                    response.EnsureSuccessStatusCode();
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var avatars = System.Text.Json.JsonSerializer.Deserialize<List<AvatarItem>>(jsonResponse);
                    return avatars ?? new List<AvatarItem>();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error fetching avatars for authorId: {authorId}");
                    throw;
                }
            }
        }
    }

    public class AvatarPerformance
    {
        [JsonPropertyName("pc_rating")]
        public string? PcRating { get; set; }

        [JsonPropertyName("android_rating")]
        public string? AndroidRating { get; set; }

        [JsonPropertyName("ios_rating")]
        public string? IosRating { get; set; } // Nullable because it can be null in JSON

        [JsonPropertyName("has_impostor")]
        public bool HasImpostor { get; set; }
    }

    public class AvatarItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("authorName")]
        public string? AuthorName { get; set; }

        [JsonPropertyName("authorId")]
        public string? AuthorId { get; set; }

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("performance")]
        public AvatarPerformance? Performance { get; set; }
    }

}
