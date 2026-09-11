using NLog;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using Tailgrab.Common;
using VRChat.API.Model;

namespace Tailgrab.Clients.Prismic
{
    public class AvatarEntry    
    {
        [JsonPropertyName("avatarId")]
        public required string AvatarId { get; set; }
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        [JsonPropertyName("author")]
        public required string Author { get; set; }
        [JsonPropertyName("description")]
        public required string Description { get; set; }

        [JsonIgnore]
        public bool Quest { get; set; }
        
        [JsonIgnore]
        public bool Ios { get; set; }

        [JsonPropertyName("flags")]
        public required int[] Flags { get; set; } // [Platform, Impostor, PC Rating, Quest Rating, IOS Rating, Content Warnings, Style Filter, Marketplace]

        [JsonIgnore]
        // Platform: 1 - PC, 2 - Quest, 4 - IOS
        public string Platform => Flags.Length > 0 ? GetPlatformString(Flags[0]) : "Unknown";

        [JsonIgnore]
        // Impostor: 1 - PC, 2 - Quest, 4 - IOS
        public string Impostor => Flags.Length > 1 ? GetImpostorString(Flags[1]) : "Unknown";

        [JsonIgnore]
        // PC Rating: 0 - Unknown, 1 - Excellent, 2 - Good, 3 - Medium, 4 - Poor, 5 - Very Poor
        public string PCRating => Flags.Length > 2 ? GetRatingString(Flags[2]) : "Unknown";
        
        [JsonIgnore]
        // Quest Rating: 0 - Unknown, 1 - Excellent, 2 - Good, 3 - Medium, 4 - Poor, 5 - Very Poor
        public string QuestRating => Flags.Length > 3 ? GetRatingString(Flags[3]) : "Unknown";
        
        [JsonIgnore]
        // IOS Rating: 0 - Unknown, 1 - Excellent, 2 - Good, 3 - Medium, 4 - Poor, 5 - Very Poor
        public string IOSRating => Flags.Length > 4 ? GetRatingString(Flags[4]) : "Unknown";
        
        [JsonIgnore]
        // Content Warnings: 1 - Sexually suggestive, 2 - Adult Language, 4 - Graphic Violence, 8 - Excessive Gore, 16 - Extreme Horror
        public string ContentWarnings => Flags.Length > 5 ? GetContentWarningsString(Flags[5]) : "None";
        
        [JsonIgnore]
        // Style Filter: 1 - Pop Culture, 2 - Furry, 4 - Sci-Fi, 8 - Anime, 16 - Cartoon, 32 - Objects, 64 - Human, 128 - Realistic, 256 - Animal, 512 - Fantasy, 1024 - Fashion
        public string StyleFilter => Flags.Length > 6 ? GetStyleFilterString(Flags[6]) : "None";

        [JsonIgnore]
        // Marketplace: 0 - Not in Marketplace, 1 - In Marketplace
        public string Marketplace => Flags.Length > 7 ? (Flags[7] == 1 ? "In Marketplace" : "Not in Marketplace") : "Unknown";

        private static string GetPlatformString(int value)
        {
            var platforms = new List<string>();
            if ((value & 1) != 0) platforms.Add("PC");
            if ((value & 2) != 0) platforms.Add("Quest");
            if ((value & 4) != 0) platforms.Add("IOS");
            return platforms.Count > 0 ? string.Join(", ", platforms) : "Unknown";
        }

        private static string GetImpostorString(int value)
        {
            var impostors = new List<string>();
            if ((value & 1) != 0) impostors.Add("PC");
            if ((value & 2) != 0) impostors.Add("Quest");
            if ((value & 4) != 0) impostors.Add("IOS");
            return impostors.Count > 0 ? string.Join(", ", impostors) : "Unknown";
        }

        private static string GetRatingString(int value)
        {
            return value switch
            {
                0 => "Unknown",
                1 => "Excellent",
                2 => "Good",
                3 => "Medium",
                4 => "Poor",
                5 => "Very Poor",
                _ => "Unknown"
            };
        }

        private static string GetContentWarningsString(int value)
        {
            var warnings = new List<string>();
            if ((value & 1) != 0) warnings.Add("Sexually suggestive");
            if ((value & 2) != 0) warnings.Add("Adult Language");
            if ((value & 4) != 0) warnings.Add("Graphic Violence");
            if ((value & 8) != 0) warnings.Add("Excessive Gore");
            if ((value & 16) != 0) warnings.Add("Extreme Horror");
            return warnings.Count > 0 ? string.Join(", ", warnings) : "None";
        }

        private static string GetStyleFilterString(int value)
        {
            var styles = new List<string>();
            if ((value & 1) != 0) styles.Add("Pop Culture");
            if ((value & 2) != 0) styles.Add("Furry");
            if ((value & 4) != 0) styles.Add("Sci-Fi");
            if ((value & 8) != 0) styles.Add("Anime");
            if ((value & 16) != 0) styles.Add("Cartoon");
            if ((value & 32) != 0) styles.Add("Objects");
            if ((value & 64) != 0) styles.Add("Human");
            if ((value & 128) != 0) styles.Add("Realistic");
            if ((value & 256) != 0) styles.Add("Animal");
            if ((value & 512) != 0) styles.Add("Fantasy");
            if ((value & 1024) != 0) styles.Add("Fashion");
            return styles.Count > 0 ? string.Join(", ", styles) : "None";
        }

        public override string ToString()
        {
            string avKey = "avtr:" + Checksum.CreateMD5(Name + ":" + Author);

            return $"AvatarId: {AvatarId}, Name: {Name}, Author: {Author}, Description: {Description}, Quest: {Quest}, IOS: {Ios}, Platform: {Platform}, Impostor: {Impostor}, PC Rating: {PCRating}, Quest Rating: {QuestRating}, IOS Rating: {IOSRating}, Content Warnings: {ContentWarnings}, Style Filter: {StyleFilter}, Marketplace: {Marketplace}, avKey: {avKey}";
        }
    }

    public class AvatarData
    {
        public int AvatarCount { get; set; }
        public int AuthorCount { get; set; }
        public string? LastUpdate { get; set; }
        public List<AvatarEntry> Entries { get; set; } = new List<AvatarEntry>();
        public Dictionary<string, AvatarEntry> IdMap { get; set; } = new Dictionary<string, AvatarEntry>();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"AvatarCount: {AvatarCount}, AuthorCount: {AuthorCount}, LastUpdate: {LastUpdate}");
            foreach (var entry in Entries)
            {
                sb.AppendLine(entry.ToString());
            }
            return sb.ToString();
        }
    }

    public class PrismicBinaryReader
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly byte[] _data;
        private int _position;

        // Static bytes assumed from context (JavaScript referenced 'staticBytes' which wasn't defined in the snippet).
        // You must populate this with the correct 16-byte key used by the encoder.
        private static readonly byte[] StaticBytes = new byte[16] {
            208, 29, 107, 36, 251, 69, 122, 14,
            67, 204, 171, 246, 106, 38, 183, 224
        };

        public PrismicBinaryReader(byte[] data)
        {
            _data = data;
            _position = 0;
        }

        public int Remaining => _data.Length - _position;

        public byte[] ReadBytes(int count)
        {
            if (_position + count > _data.Length) throw new InvalidOperationException("Not enough data");
            var result = new byte[count];
            Array.Copy(_data, _position, result, 0, count);
            _position += count;
            return result;
        }

        public byte ReadByte()
        {
            if (_position >= _data.Length) throw new InvalidOperationException("Not enough data");
            return _data[_position++];
        }

        // Reads 3 bytes as a 24-bit integer (Big Endian based on JS shift logic: byte0 << 16)
        public int ReadInt24()
        {
            var bytes = ReadBytes(3);
            return (bytes[0] << 16) | (bytes[1] << 8) | bytes[2];
        }

        // Reads an array of 4-byte integers (Big Endian based on typical network protocols unless specified)
        // The JS code implies standard integer reading. Assuming Big Endian to match the Int24 style.
        public int[] ReadIntArray(int count)
        {
            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                var bytes = ReadBytes(4);
                // Little Endian: least significant byte first (byte0 is LSB).
                result[i] = bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24);
            }
            return result;
        }

        public async Task RemoveHashesByPatternAsync(IConnectionMultiplexer connection, string pattern, int databaseId = 0)
        {
            var script = LuaScript.Prepare(
                "for _,k in ipairs(redis.call('keys', @pattern)) do redis.call('del', k) end");

            await connection.GetDatabase(databaseId).ScriptEvaluateAsync(script, new { pattern = pattern });

            logger.Info($"Deleted keys matching pattern '{pattern}'");
        }

        public async void Parse()
        {
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("warren01:6379");
            IDatabase db = redis.GetDatabase();
            await RemoveHashesByPatternAsync(redis, "avtr_idx:*");

            if (_data.Length == 0) throw new Exception("Data has length zero");

            // Check Header "PAS"
            var headerBytes = ReadBytes(3);
            string header = Encoding.UTF8.GetString(headerBytes);
            if (header != "PAS") throw new Exception("PAS Header not found");

            // Meta Bytes
            var metaBytes = ReadBytes(2);
            int version = (metaBytes[0] >> 5) & 31;
            int platform = metaBytes[0] & 7;

            var avatarData = new AvatarData
            {
                AvatarCount = ReadInt24(),
                AuthorCount = ReadInt24()
            };

            // Date Calculation
            var dateArr = ReadBytes(2);
            int dateNum = ((dateArr[0] << 8) + dateArr[1]) >> 3;
            int year = ((dateNum >> 9) + 16);
            int month = (dateNum >> 5) & 15;
            int day = dateNum & 31;
            avatarData.LastUpdate = $"20{year:D2}-{month:D2}-{day:D2}";

            int fileAvatars = ReadInt24();
            int fileAuthors = ReadInt24(); // Unused per JS comment

            byte flagSize = ReadByte();
            var randomBytes = ReadBytes(16);

            // Dynamic Bytes Calculation
            byte[] dynamicBytes = new byte[16];
            if (version == 0)
            {
                for (int i = 0; i < 16; i++)
                    dynamicBytes[i] = (byte)(randomBytes[i] ^ StaticBytes[i]);
            }
            else
            {
                for (int i = 0; i < 16; i++)
                    dynamicBytes[i] = (byte)(randomBytes[i] ^ ((StaticBytes[(i + version) % 16] + flagSize) & 0xFF));
            }

            // Read Avatar IDs (16 bytes each)
            int dataSize = fileAvatars * 16;
            var avatarIdBytes = ReadBytes(dataSize);
            // Read Flags and Author IDs
            // Note: JS says "shit will break" if flagSize != 4. Assuming 4-byte integers.
            Int32[] flags = ReadIntArray(fileAvatars);
            Int32[] authorIds = ReadIntArray(fileAvatars);

            // Read Strings
            var remainingBytes = ReadBytes(Remaining);
            string allStrings = Encoding.UTF8.GetString(remainingBytes);
            var stringParts = allStrings.Split('\n');

            if (stringParts.Length < 2) throw new Exception("Malformed string block");

            var authorNames = stringParts[0].Split('\r');
            var avatarNames = stringParts[1].Split('\r');

            var startTime = Stopwatch.GetTimestamp();
            for (int i = 0; i < fileAvatars; i++)
            {
                int f = flags[i];

                /*
                Platform:
                    1 - PC
                    2 - Quest
                    4 - IOS
                Impostor:
                    1 - PC
                    2 - Quest
                    4 - IOS
                PC Rating:
                    0 - Unknown
                    1 - Excellent
                    2 - Good
                    3 - Medium
                    4 - Poor
                    5 - Very Poor
                Quest Rating:
                    0 - Unknown
                    1 - Excellent
                    2 - Good
                    3 - Medium
                    4 - Poor
                    5 - Very Poor
                IOS Rating:
                    0 - Unknown
                    1 - Excellent
                    2 - Good
                    3 - Medium
                    4 - Poor
                    5 - Very Poor
                Content Warnings:
                    1 - Sexually suggestive
                    2 - Adult Language
                    4 - Graphic Violence
                    8 - Excessive Gore
                    16 - Extreme Horror
                Style Filter:
                    1 - Pop Culture
                    2 - Furry
                    4 - Sci-Fi
                    8 - Anime
                    16 - Cartoon
                    32 - Objects
                    64 - Human
                    128 - Realistic
                    256 - Animal
                    512 - Fantasy
                    1024 - Fashion
                Marketplace:
                    0 - Not in Marketplace
                    1 - In Marketplace
                */

                // Extract Flags
                int[] avatarFlags = new int[]
                {
                (f >> 29) & 7,       // Platform
                (f >> 26) & 7,       // Impostor
                (f >> 17) & 7,       // PC Rating
                (f >> 20) & 7,       // Quest Rating
                (f >> 23) & 7,       // IOS Rating
                (f >> 12) & 31,      // Content Warnings
                (f >> 1) & 2047,     // Style Filter
                (f) & 1              // Marketplace
                };

                // Decode Avatar ID
                int offset = i * 16;
                var idSlice = new byte[16];
                Array.Copy(avatarIdBytes, offset, idSlice, 0, 16);
                string avatarId = DecodeAvatarId(idSlice, dynamicBytes);

                // Parse Name and Description
                string rawNameDesc = avatarNames[i];
                var nameDescParts = rawNameDesc.Split('\t');

                // Reverse strings as per JS logic
                string name = ReverseString(nameDescParts[0]);
                string description = (nameDescParts.Length > 1) ? ReverseString(nameDescParts[1]) : "";

                // Author Name
                int authorIndex = authorIds[i] & 524287;
                string author = (authorIndex < authorNames.Length) ? ReverseString(authorNames[authorIndex]) : "Unknown";

                var entry = new AvatarEntry
                {
                    AvatarId = avatarId,
                    Name = name,
                    Author = author,
                    Description = description,
                    Quest = false,
                    Ios = false,
                    Flags = avatarFlags
                };

                avatarData.Entries.Add(entry);
                avatarData.IdMap.Add(avatarId, entry);

                string avKeyFull = "avtr:" + Checksum.CreateMD5(author) + ":" + Checksum.CreateMD5(name);
                string avKeyAuthor = "avtr_idx:" + Checksum.CreateMD5(author);

                bool keyExists = await db.KeyExistsAsync(avKeyFull);
                if (!keyExists)
                {
                    logger.Info($"Adding new avatar entry: {entry.ToString()}");
                }

                IBatch batch = db.CreateBatch();
                Task set1 = batch.StringSetAsync(avKeyFull, JsonSerializer.Serialize(entry));
                Task set2 = batch.SetAddAsync(avKeyAuthor, Checksum.CreateMD5(name));
                batch.Execute();
                await Task.WhenAll(set1, set2);

                if (i % 10000 == 0)
                {
                    var elapsed = Stopwatch.GetElapsedTime(startTime);
                    logger.Info($"Processed Prismic Avatars #{i} of {fileAvatars} in {elapsed.TotalMilliseconds}");
                    startTime = Stopwatch.GetTimestamp();
                }
            }

            logger.Info($"{avatarData.AvatarCount} avatars, {avatarData.AuthorCount} authors, last update: {avatarData.LastUpdate}");
            logger.Info($"Total entries parsed: {avatarData.Entries.Count}");
            logger.Info($"Total unique IDs in IdMap: {avatarData.IdMap.Count}");

            foreach (AvatarEntry entry in avatarData.Entries.AsEnumerable().Reverse().Take(50))
            {
                logger.Info(entry.ToString());
            }

            redis.Close();
            redis.Dispose();
        }

        private string DecodeAvatarId(byte[] crypt, byte[] iv)
        {
            for (int i = crypt.Length - 1; i >= 0; i--)
            {
                int k = crypt[i] ^ crypt[(i + crypt.Length - 1) % crypt.Length] ^ iv[i];
                crypt[i] = (byte)k;
            }

            var hexString = string.Concat(crypt.Select(x => x.ToString("x2")));
            var decrypt = hexString.ToCharArray().Reverse().ToList();

            decrypt.Insert(8, '-');
            decrypt.Insert(13, '-');
            decrypt.Insert(18, '-');
            decrypt.Insert(23, '-');

            return "avtr_" + string.Concat(decrypt);
        }

        private string ReverseString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            char[] chars = input.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        public static async Task<List<string>> GetFileUris(string apiUrl)
        {
            List<string> rawUrls = new List<string>();
            using var client = new HttpClient();

            // GitHub API requires a User-Agent header
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GistRawUrlFetcher", "1.0"));

            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                logger.Info(jsonResponse);
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);

                var filesElement = doc.RootElement.GetProperty("files");

                // Iterate over all files in the gist
                foreach (var fileProperty in filesElement.EnumerateObject())
                {
                    var fileObject = fileProperty.Value;
                    if (fileObject.TryGetProperty("raw_url", out JsonElement rawUrlElement))
                    {
                        string? rawUrl = rawUrlElement.GetString();
                        if (!string.IsNullOrEmpty(rawUrl))
                        {
                            rawUrls.Add(rawUrl);
                            logger.Debug($"File: {fileProperty.Name} -> Raw URL: {rawUrl}");
                        }
                    }
                }

                logger.Debug($"\nTotal files found: {rawUrls.Count}");
            }
            catch (HttpRequestException ex)
            {
                logger.Error($"HTTP error: {ex.Message}");
                if (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    logger.Error("Hint: Did you set a User-Agent header? GitHub requires it.");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error: {ex.Message}");
            }

            return rawUrls;
        }

        public static async Task<string> GetURLContentString(string uri)
        {
            using var client = new HttpClient();
            try
            {
                HttpResponseMessage response = await client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                string content = await response.Content.ReadAsStringAsync();
                return content;

            }
            catch (HttpRequestException ex)
            {
                logger.Error($"HTTP error while fetching {uri}: {ex.Message}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error($"Error while fetching {uri}: {ex.Message}");
                return string.Empty;

            }
        }

        public static async Task<byte[]> GetURLContentBytes(string uri)
        {
            using var client = new HttpClient();
            try
            {
                HttpResponseMessage response = await client.GetAsync(uri);
                response.EnsureSuccessStatusCode();
                byte[] content = await response.Content.ReadAsByteArrayAsync();
                return content;

            }
            catch (HttpRequestException ex)
            {
                logger.Error($"HTTP error while fetching {uri}: {ex.Message}");
                return Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                logger.Error($"Error while fetching {uri}: {ex.Message}");
                return Array.Empty<byte>();

            }
        }

        public static async void GetPrismicDataAsync(string gistHash)
        {
            List<string> uris = await GetFileUris($"https://api.github.com/gists/{gistHash}");

            foreach (string apiUrl in uris)
            {
                if (apiUrl.Contains("pasavtrdb.txt"))
                {
                    byte[] byteResponse = await GetURLContentBytes(apiUrl);
                    logger.Info($"Successfully downloaded avatar data from {apiUrl}");

                    var reader = new PrismicBinaryReader(byteResponse);
                    reader.Parse();
                }
            }
        }

        public static async void GetPrismicObjAsync(string filePath)
        {
            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var reader = new PrismicBinaryReader(fileBytes);
            reader.Parse();
        }
    }
}
