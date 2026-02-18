namespace Tailgrab.Common
{
    public static class CommonConst
    {
        public const string ApplicationName = "Tailgrab";
        public const string CompanyName = "DeviousFox";
        public const string ConfigRegistryPath = "Software\\DeviousFox\\Tailgrab\\Config";

        // VRChat Web API registry keys
        public const string Registry_VRChat_Web_UserName = "VRCHAT_USERNAME";
        public const string Registry_VRChat_Web_Password = "VRCHAT_PASSWORD";
        public const string Registry_VRChat_Web_2FactorKey = "VRCHAT_2FA";

        // Ollama API registry keys and defaults
        public const string Registry_Ollama_API_Key = "OLLAMA_API_KEY";
        public const string Registry_Ollama_API_Endpoint = "OLLAMA_API_ENDPOINT";
        public const string Registry_Ollama_API_Prompt = "OLLAMA_API_PROMPT";
        public const string Registry_Ollama_API_Image_Prompt = "OLLAMA_API_PROMPT_IMAGE";
        public const string Registry_Ollama_API_Model = "OLLAMA_API_Model";
        public const string Default_Ollama_API_Prompt = "From the following block of text, classify the contents into a single class from the following classes;\r\n'OK' - Where as all text content can be considered PG13;\r\n'Explicit Sexual' - Where as any of the text contained describes sexual acts or intent. Flagged words Bussy, Fagot, Dih;\r\n'Harassment & Bullying' - Where the text is describing acts of trolling or bullying users on Religion, Sexual Orientation or Race. Flagged words of base nigg* and variations of that spelling to hide racism.\r\n'Self Harm' - Any part of the text where it explicitly describes destructive behaviours.\r\nIf there is not enough information to determine the class, use a default of OK. When replying, return a single line for the Classification and a carriage return, then place the reasoning on subsequent lines, translate any foreign language to English: \n";
        public const string Default_Ollama_API_Image_Prompt = "From the attached image, reply with a single classification of, 'OK', 'Sexual Content', 'Gore' or 'Racism'. In the following line, give the reasoning for the classifcation.";
        public const string Default_Ollama_API_Endpoint = "https://ollama.com";
        public const string Default_Ollama_API_Model = "gemma3:27b";

        // Alert sound registry keys
        public const string Registry_Alert_Avatar = "ALERT_AVATAR_SOUND";
        public const string Registry_Alert_Group = "ALERT_GROUP_SOUND";
        public const string Registry_Alert_Profile = "ALERT_PROFILE_SOUND";

        // Gist related registry keys
        public const string Registry_Group_Checksum = "GIST_GROUP_LIST_CHECKSUM";
        public const string Registry_Group_Gist = "GIST_GROUP_LIST_URL";

        // Avatar Gist related registry keys
        public const string Registry_Avatar_Checksum = "GIST_AVATAR_LIST_CHECKSUM";
        public const string Registry_Avatar_Gist = "GIST_AVATAR_LIST_URL";
    }
}
