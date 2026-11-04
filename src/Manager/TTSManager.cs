using ConcurrentPriorityQueue.Core;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NLog;
using System.Diagnostics.CodeAnalysis;
using System.Speech.Synthesis;
using Tailgrab.Clients.Omnivoice;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Tailgrab.Manager
{
    public class TTSManager
    {
        protected static readonly Logger _Logger = LogManager.GetCurrentClassLogger();
        private static ServiceRegistry? serviceRegistry;
        private ConcurrentPriorityQueue<IHavePriority<int>, int> ttsQueue = new();

        // Create an instance of the synthesizer
        SpeechSynthesizer synthesizer = new SpeechSynthesizer();


        [SetsRequiredMembers]
        public TTSManager(ServiceRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry), "ServiceRegistry parameter cannot be null.");
            }
            serviceRegistry = registry;
            _ = Task.Run(() => SpeechTaskProcessor(ttsQueue, serviceRegistry));
        }

        public void EnqueueSpeech(string text)
        {
            SpeechItem item = new SpeechItem
            {
                Text = text,
                Priority = 10, // Default priority
            };

            ttsQueue.Enqueue(item);
            _Logger.Debug($"TTSManager: Queuing TTS: '{item.Text}' with Volume: {item.Volume} and Rate: {item.Speed}.");
        }

        private async Task SpeechTaskProcessor(ConcurrentPriorityQueue<IHavePriority<int>, int> queue, ServiceRegistry registry)
        {
            try
            {
                _Logger.Info($"Speech Queue Running");
                while (true)
                {
                    // Process items from the priority queue
                    while (true)
                    {
                        var result = ttsQueue.Dequeue();
                        if (result.IsSuccess)
                        {
                            if (result.Value is SpeechItem item && item.Text != null)
                            {
                                synthesizer.SelectVoice(item.Voice);
                                synthesizer.Volume = item.Volume; // Set volume (0-100)
                                synthesizer.Rate = item.Speed; // Set speed (-10 to 10)
                                                               // Speak text synchronously
                                synthesizer.Speak(item.Text);
                                continue;
                            }
                        }
                        else
                        {
                            // No more items to process
                            break;
                        }

                        // Wait for a short period before getting next record
                        await Task.Delay(1000);
                    }

                    // Wait for a short period before checking the queue again
                    await Task.Delay(10000);
                }
            }
            catch (Exception ex)
            {
                _Logger.Error(ex, "Error in SpeechTaskProcessor");
            }
        }
    }

    public class SpeechItem : IHavePriority<int>
    {
        public string? Text { get; set; }

        public int Volume { get; set; } = 100; // Default volume

        public int Speed { get; set; } = 0; // Default speed: Neutral 0

        public string Voice { get; set; } = "Microsoft Zira Desktop"; // Default voice

        public int Priority { get; set; }
    }
}
