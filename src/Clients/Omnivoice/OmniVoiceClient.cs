using NLog;
using System.Net.Http;
using System.Text.Json;
using System.IO;
using Tailgrab.Common;
using System.Net.Http.Json;

namespace Tailgrab.Clients.Omnivoice
{
    internal class OmniVoiceClient
    {
        #region Fields & Initialization

        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly HttpClient _http;
        private List<VoiceJsonData> _voicePersonalities = new();

        public OmniVoiceClient()
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromMinutes(1); // TTS can be slow

            // Load the Voice Personalities
            LoadVoicePersonalities();
        }

        #endregion

        #region Internal Records
        /// <summary>
        /// OmniVoice-TTSServe has a structure to render cloned voices.
        /// </summary>
        internal record SythesisRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("text")]
            public string Text { get; init; }

            [System.Text.Json.Serialization.JsonPropertyName("audio_base64")]
            public string AudioBase64 { get; init; }

            [System.Text.Json.Serialization.JsonPropertyName("reference_text")]
            public string RefrenceText { get; init; }

            [System.Text.Json.Serialization.JsonPropertyName("language")]
            public string Language { get; init; }

            [System.Text.Json.Serialization.JsonPropertyName("num_steps")]
            public Int32 NumberSteps { get; set; } = 16;

            [System.Text.Json.Serialization.JsonPropertyName("guidance_scale")]
            public double GuidanceScale { get; set; } = 1.5;

            [System.Text.Json.Serialization.JsonPropertyName("denoise")]
            public bool Denoise{ get; set; } = true;

            public override string ToString()
            {
                return $"SythesisRequest(Text={Text}, AudioBase64Length={AudioBase64?.Length}, RefrenceText={RefrenceText}, Language={Language}, NumberSteps={NumberSteps}, GuidanceScale={GuidanceScale}, Denoise={Denoise})";
            }
        }

        /// <summary>
        /// Basid Voice Clone Data from the Filesystem
        /// </summary>
        internal record VoiceJsonData
        {
            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string Name { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("description")]
            public string Description { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("prompt")]
            public string Prompt { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("language")]
            public string Language { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("referenceText")]
            public string ReferenceText { get; set; }

            // Not serialized from JSON
            public string AudioReferenceUrl { get; set; }
            public byte[] AudioReferenceBytes { get; set; }
        }

        #endregion

        #region Configuration

        public bool EnableTTSService
        {
            get => ConfigStore.GetStoredKeyBool(CommonConst.Registry_OmniVoice_Enabled, true);
            set => ConfigStore.PutStoredKeyBool(CommonConst.Registry_OmniVoice_Enabled, value);
        }

        public string OmnivoiceEndpoint
        {
            get => ConfigStore.GetStoredKeyString(CommonConst.Registry_OmniVoice_Endpoint) ?? @"http://10.4.1.115:7500/synthesize";
            set => ConfigStore.PutStoredKeyString(CommonConst.Registry_OmniVoice_Endpoint, value);
        }

        public string VoiceCloneProfile
        {
            get => ConfigStore.GetStoredKeyString(CommonConst.Registry_OmniVoice_VoiceCloneProfile) ?? "Jarvis";
            set => ConfigStore.PutStoredKeyString(CommonConst.Registry_OmniVoice_VoiceCloneProfile, value);
        }

        public int AudioSampleRate
        {
            get => int.Parse(ConfigStore.GetStoredKeyString(CommonConst.Registry_OmniVoice_AudioSampleRate) ?? "24000");
            set => ConfigStore.PutStoredKeyString(CommonConst.Registry_OmniVoice_AudioSampleRate, value.ToString());
        }

        #endregion

        #region Voice Personality Management

        /// <summary>
        /// Load the Base Voice Personalities into Memory
        /// </summary>
        public void LoadVoicePersonalities()
        {
            string voicesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Tailgrab",
                "voices");

            if (!Directory.Exists(voicesPath))
            {
                Logger.Warn($"Voices directory not found: {voicesPath}");
                return;
            }

            _voicePersonalities.Clear();

            try
            {
                var jsonFiles = Directory.GetFiles(voicesPath, "*.json", SearchOption.TopDirectoryOnly);

                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        string jsonContent = System.IO.File.ReadAllText(jsonFile);
                        var voiceData = JsonSerializer.Deserialize<VoiceJsonData>(jsonContent);

                        if (voiceData != null && !string.IsNullOrEmpty(voiceData.Name))
                        {
                            string wavFileName = $"{Path.GetFileNameWithoutExtension(jsonFile)}.wav";
                            string wavFilePath = Path.Combine(voicesPath, wavFileName);

                            voiceData.AudioReferenceUrl = wavFilePath;
                            voiceData.Description ??= string.Empty;

                            _voicePersonalities.Add(voiceData);
                            Logger.Info($"Loaded voice personality: {voiceData.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error loading voice file {jsonFile}: {ex.Message}");
                    }
                }

                Logger.Info($"Loaded {_voicePersonalities.Count} voice personalities from {voicesPath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading voice personalities: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds the Voice Personality record and loads the Reference Audio into the structure.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>VoiceJsonData or null if not found</returns>
        public async Task<VoiceJsonData?> GetVoicePersonalityWithAudioAsync(string name)
        {
            var vp = _voicePersonalities.FirstOrDefault(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (vp == null)
            {
                Logger.Warn($"Voice personality '{name}' not found.");
                return null;
            }

            try
            {
                if (!System.IO.File.Exists(vp.AudioReferenceUrl))
                {
                    Logger.Error($"Audio file not found: {vp.AudioReferenceUrl}");
                    return null;
                }

                byte[] audioBytes = await System.IO.File.ReadAllBytesAsync(vp.AudioReferenceUrl).ConfigureAwait(false);
                vp.AudioReferenceBytes = audioBytes;

                return vp;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reading audio file for '{name}': {ex.Message}");
                return null;
            }
        }

        #endregion

        #region TTS Service Operations
        /// <summary>
        /// Test the TTS Service Endpoint
        /// </summary>
        /// <param name="ttsEndpoint"></param>
        /// <returns></returns>
        public async Task<bool> TestTTSServiceAsync(string ttsEndpoint)
        {
            try
            {
                var resp = await _http.GetAsync(ttsEndpoint);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error testing TTS service: {ex.Message}");
                return false;
            }
        }


        /// <param name="ttsEndpoint">e.g. "http://localhost:7500/synthesize"</param>
        /// <param name="sampleWavBytes">Reference voice sample (raw WAV file bytes)</param>
        /// <param name="sampleWavTranscription">Transcript of the reference sample</param>
        /// <param name="sampleRate">Sample rate of the reference WAV (Hz)</param>
        /// <param name="textToRender">Text to synthesize in the cloned voice</param>
        public async Task<byte[]> GetClonedVoiceWavAsync(
            string ttsEndpoint,
            byte[] sampleWavBytes,
            string sampleWavTranscription,
            int sampleRate,
            string textToRender)
        {
            // Encode reference WAV as base64 data URL
            string refAudioBase64 = Convert.ToBase64String(sampleWavBytes);

            SythesisRequest payload = new SythesisRequest
            {
                Text = textToRender,
                AudioBase64 = refAudioBase64,
                RefrenceText = sampleWavTranscription,
                Language = "en"
            };

            Logger.Info($"Sending TTS request to {ttsEndpoint} with payload: {payload}");
            HttpResponseMessage resp = await _http.PostAsJsonAsync(ttsEndpoint, payload);

            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);

            // Response: { "audio_base64": "...", "sample_rate": 24000, ... }
            string audioBase64 = doc.RootElement
                .GetProperty("audio_base64").GetString()!;

            return Convert.FromBase64String(audioBase64);
        }

        /// <summary>
        /// Renders the given text using the configured voice clone profile and saves the output to a WAV file.
        /// </summary>
        /// <param name="textToRender">The text to render as audio.</param>
        /// <returns>The generated audio as a byte array.</returns>
        public async Task<byte[]> GetAudioForTextAsync(string textToRender)
        {
            if (!EnableTTSService)
            {
                Logger.Warn("TTS service is disabled.");
                return null;
            }

            try
            {
                // Get the voice personality
                VoiceJsonData? vp = await GetVoicePersonalityWithAudioAsync(VoiceCloneProfile);
                if (vp == null)
                {
                    Logger.Error($"Voice personality '{VoiceCloneProfile}' not found.");
                    return null;
                }

                // Call the TTS service
                byte[] generatedWav = await GetClonedVoiceWavAsync(
                    OmnivoiceEndpoint,
                    vp.AudioReferenceBytes,
                    vp.ReferenceText,
                    AudioSampleRate,
                    textToRender);

                return generatedWav;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating audio: {ex.Message}");
                return null;
            }
        }
        #endregion
    }
}
