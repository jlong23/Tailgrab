using ConcurrentPriorityQueue.Core;
using Tailgrab.Common;

namespace Tailgrab.Clients.Ollama
{
    // Simplest implementation of IHavePriority<T>
    public class QueuedProcess : IHavePriority<int>
    {
        public int Priority { get; set; }
        public int retries { get; set; } = 0;
        public required string UserId { get; set; }
        public string? UserBio { get; set; }
        public bool IsFriend { get; set; }
        public string? ProfileUrl { get; set; }
        public TrustClassEnum UserTrustClass { get; set; } = TrustClassEnum.VISITOR;
        public AgeVerificationEnum AgeVerification { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;

        public string MD5Hash
        {
            get
            {
                if (string.IsNullOrEmpty(UserBio))
                {
                    return string.Empty;
                }

                // Remove all whitespace for hashing
                return Checksum.CreateMD5(UserBio);
            }
        }
    }
}
