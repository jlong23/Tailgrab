namespace Tailgrab.Common
{
    public static class Common
    {
        public const string ApplicationName = "Tailgrab";
        public const string CompanyName = "DeviousFox";
        public const string ConfigRegistryPath = "Software\\DeviousFox\\Tailgrab\\Config";
        public const string Registry_VRChat_Web_UserName = "VRCHAT_USERNAME";
        public const string Registry_VRChat_Web_Password = "VRCHAT_PASSWORD";
        public const string Registry_VRChat_Web_2FactorKey = "VRCHAT_2FA";
        public const string Registry_Ollama_API_Key = "OLLAMA_API_KEY";
        public const string Registry_Ollama_API_Endpoint = "OLLAMA_API_ENDPOINT";
        public const string Registry_Ollama_API_Prompt = "OLLAMA_API_PROMPT";
        public const string Registry_Ollama_API_Model = "OLLAMA_API_Model";
        public const string Default_Ollama_API_Prompt = "From the following block of text, classify the contents into a single class;\n'OK', 'Explicit Sexual', 'Harrassment & Bullying', 'Self Harm' or 'Other'.\nWhen replying, give a single line for the Classification and then a new line for the resoning: \n";
        public const string Default_Ollama_API_Endpoint = "https://ollama.com";
        public const string Default_Ollama_API_Model = "gemma3:27b";

    }
}
