using ConcurrentPriorityQueue.Core;
using VRChat.API.Model;

namespace Tailgrab.Clients.Ollama
{
    public class ImageReference : IHavePriority<int>
    {
        public int Priority { get; set; }
        public List<string> Base64Data { get; set; } = [];
        public string Md5Hash { get; set; } = string.Empty;
        public string InventoryId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int retries { get; set; } = 0;
        public string ItemName { get; set; } = string.Empty;
        public string ItemContentUrl { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public Print? PrintInfo { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"ImageReference(Priority={Priority}, Md5Hash={Md5Hash}, InventoryId={InventoryId}, UserId={UserId}, retries={retries}, ItemName={ItemName}, ItemContentUrl={ItemContentUrl}, ItemType={ItemType}, PrintInfo={PrintInfo}, Prompt={Prompt}, Model={Model})";
        }
    }
}
