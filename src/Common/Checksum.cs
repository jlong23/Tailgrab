using System.Text.RegularExpressions;

namespace Tailgrab.Common
{

    public class Checksum
    {
        public static readonly Regex sWhitespace = new(@"\s+");

        public static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                return Convert.ToHexString(hashBytes);
            }
        }
        public static string CreateMD5(byte[] imageBytes)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(imageBytes);

                return Convert.ToHexString(hashBytes);
            }
        }

        public static string MD5Hash(string hashable)
        {
            if (string.IsNullOrEmpty(hashable))
            {
                return string.Empty;
            }

            // Remove all whitespace for hashing
            return Checksum.CreateMD5(sWhitespace.Replace(hashable, ""));
        }
    }
}
