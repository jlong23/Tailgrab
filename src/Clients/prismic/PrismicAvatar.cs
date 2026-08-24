using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Tailgrab.Clients.Prismic
{
    public class AvatarEntry
    {
        public string AvatarId { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public bool Quest { get; set; }
        public bool Ios { get; set; }
        public int[] Flags { get; set; } // [Platform, Impostor, PC Rating, Quest Rating, IOS Rating, Content Warnings, Style Filter, Marketplace]
    }

    public class AvatarData
    {
        public int AvatarCount { get; set; }
        public int AuthorCount { get; set; }
        public string LastUpdate { get; set; }
        public List<AvatarEntry> Entries { get; set; } = new List<AvatarEntry>();
        public Dictionary<string, AvatarEntry> IdMap { get; set; } = new Dictionary<string, AvatarEntry>();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"AvatarCount: {AvatarCount}, AuthorCount: {AuthorCount}, LastUpdate: {LastUpdate}");
            foreach (var entry in Entries)
            {
                sb.AppendLine($"AvatarId: {entry.AvatarId}, Name: {entry.Name}, Author: {entry.Author}, Description: {entry.Description}, Quest: {entry.Quest}, Ios: {entry.Ios}, Flags: [{string.Join(", ", entry.Flags)}]");
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
                // JavaScript bitwise operations on typed arrays often imply Big Endian network order 
                // or depend on how the bytes were written. Assuming Big Endian (byte0 << 24).
                result[i] = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
            }
            return result;
        }

        public AvatarData Parse()
        {
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
            var flags = ReadIntArray(fileAvatars);
            var authorIds = ReadIntArray(fileAvatars);

            // Read Strings
            var remainingBytes = ReadBytes(Remaining);
            string allStrings = Encoding.UTF8.GetString(remainingBytes);
            var stringParts = allStrings.Split('\n');

            if (stringParts.Length < 2) throw new Exception("Malformed string block");

            var authorNames = stringParts[0].Split('\r');
            var avatarNames = stringParts[1].Split('\r');

            for (int i = 0; i < fileAvatars; i++)
            {
                int f = flags[i];

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
                avatarData.IdMap[avatarId] = entry;
            }

            return avatarData;
        }

        private string DecodeAvatarId(byte[] idBytes, byte[] dynamicKey)
        {
            // The JS code calls decodeAvatarId, but the function body isn't provided in the snippet.
            // Typically, this involves XORing the ID bytes with the dynamic key.
            // Assuming a simple XOR loop similar to the dynamicBytes generation or a direct XOR.
            // Based on common patterns in such binary formats:

            byte[] decoded = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                decoded[i] = (byte)(idBytes[i] ^ dynamicKey[i % dynamicKey.Length]);
            }

            // Return as Hex String (common for IDs) or GUID string
            return BitConverter.ToString(decoded).Replace("-", "").ToLowerInvariant();
        }

        private string ReverseString(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            char[] chars = input.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        public static async Task<AvatarData> GetPrismicObjAsync(string filePath)
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
            var reader = new PrismicBinaryReader(fileBytes);
            return reader.Parse();
        }
    }

}
