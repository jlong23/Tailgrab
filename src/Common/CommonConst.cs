using System.IO;
using System.Text.RegularExpressions;

namespace Tailgrab.Common
{
    public static class CommonConst
    {

        public static string APPLICATION_LOCAL_DATA_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tailgrab");
        public const string APPLICATION_LOCAL_DATABASE = "tailgrab.db";
        public const string ApplicationName = "Tailgrab";
        public const string CompanyName = "DeviousFox";
        public const string ConfigRegistryPath = "Software\\DeviousFox\\Tailgrab\\Config";

        // Registry schema version tracking
        public const string Registry_Schema_Version = "REGISTRY_SCHEMA_VERSION";

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
        public const string Default_Ollama_API_Prompt = "```markdown\r\nAnalyze the following text block and classify it into exactly one of these categories:\r\n1. 'OK' - Content is appropriate for general audiences (PG-13 equivalent)\r\n2. 'Explicit Sexual' - Contains descriptions of sexual acts or intent\r\n   - Flagged terms: 'Bussy', 'Faggot', 'Dih'\r\n   - Note: Age requirements (18+, 21+) alone do not indicate sexual content\r\n3. 'Harassment & Bullying' - Contains trolling, bullying, or discrimination based on religion, sexual orientation, or race\r\n   - Includes racial slurs (e.g., variations of 'nigg*') and their intentional misspellings\r\n4. 'Self Harm' - Describes explicit self-destructive behaviors\r\n\r\nClassification Guidelines:\r\n- Use 'OK' if content is ambiguous or lacks sufficient information\r\n- Translate any non-English text to English before classification\r\n- Consider context when evaluating flagged terms\r\n\r\nOutput the results in the following format, excluding brackets:\r\n[Classification]\r\n[Reasoning]\r\n```\r\n";
        public const string Default_Ollama_API_Image_Prompt = "```markdown\r\nAnalyze the attached image and classify its content using exactly one of these categories:\r\n1. OK\r\n2. Explicit Sexual\r\n3. Self Harm\r\n4. Harassment & Bullying\r\n   - Important: Any slurs - including partial spellings, intentional misspellings, or coded variations - should be classified under 'Harassment & Bullying'.\r\n\r\nClassification Guidelines:\r\n- Use 'OK' if content is ambiguous or lacks sufficient information\r\n- Translate any non-English text to English before classification\r\n\r\n- Consider context when evaluating flagged terms\r\n\r\nOutput the results in the following format, excluding brackets:\r\n[Classification]\r\n[Reasoning]\r\n```\r\n";
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

        public const string Avatar_Alert_Key = "Avatar";
        public const string Group_Alert_Key = "Group";
        public const string Profile_Alert_Key = "Profile";
        public const string Sound_Alert_Key = "Sound";
        public const string Color_Alert_Key = "Color";

        // Highlight class color registry keys
        public const string Registry_HighlightClass_Normal_Background = "HIGHLIGHT_NORMAL_BG";
        public const string Registry_HighlightClass_Normal_Foreground = "HIGHLIGHT_NORMAL_FG";
        public const string Registry_HighlightClass_Friend_Background = "HIGHLIGHT_FRIEND_BG";
        public const string Registry_HighlightClass_Friend_Foreground = "HIGHLIGHT_FRIEND_FG";
        public const string Registry_HighlightClass_Class01_Background = "HIGHLIGHT_CLASS01_BG";
        public const string Registry_HighlightClass_Class01_Foreground = "HIGHLIGHT_CLASS01_FG";
        public const string Registry_HighlightClass_Class02_Background = "HIGHLIGHT_CLASS02_BG";
        public const string Registry_HighlightClass_Class02_Foreground = "HIGHLIGHT_CLASS02_FG";
        public const string Registry_HighlightClass_Class03_Background = "HIGHLIGHT_CLASS03_BG";
        public const string Registry_HighlightClass_Class03_Foreground = "HIGHLIGHT_CLASS03_FG";
        public const string Registry_HighlightClass_Class04_Background = "HIGHLIGHT_CLASS04_BG";
        public const string Registry_HighlightClass_Class04_Foreground = "HIGHLIGHT_CLASS04_FG";
        public const string Registry_HighlightClass_Selected_Background = "HIGHLIGHT_SELECTED_BG";
        public const string Registry_HighlightClass_Selected_Foreground = "HIGHLIGHT_SELECTED_FG";
        public const string Registry_HighlightClass_MouseOver_Background = "HIGHLIGHT_MOUSEOVER_BG";
        public const string Registry_HighlightClass_MouseOver_Foreground = "HIGHLIGHT_MOUSEOVER_FG";

        // Default highlight class colors
        public const string Default_HighlightClass_Normal_Background = "#FF1E1E1E";
        public const string Default_HighlightClass_Normal_Foreground = "#FFE6E6E6";
        public const string Default_HighlightClass_Friend_Background = "#FF1E1E1E";
        public const string Default_HighlightClass_Friend_Foreground = "LightGreen";
        public const string Default_HighlightClass_Class01_Background = "Yellow";
        public const string Default_HighlightClass_Class01_Foreground = "Black";
        public const string Default_HighlightClass_Class02_Background = "Red";
        public const string Default_HighlightClass_Class02_Foreground = "Yellow";
        public const string Default_HighlightClass_Class03_Background = "Purple";
        public const string Default_HighlightClass_Class03_Foreground = "Yellow";
        public const string Default_HighlightClass_Class04_Background = "Black";
        public const string Default_HighlightClass_Class04_Foreground = "Yellow";
        public const string Default_HighlightClass_Selected_Background = "#FF1d1db3";
        public const string Default_HighlightClass_Selected_Foreground = "#FFFFFF00";
        public const string Default_HighlightClass_MouseOver_Background = "#FF1d1db3";
        public const string Default_HighlightClass_MouseOver_Foreground = "#FFFFFF00";

        public const string Registry_Discovered_Avatar_Caching = "DISCOVERED_AVATAR_CACHING";
        public const string Registry_Moderated_Avatar_Caching = "MODERATED_AVATAR_CACHING";
        public const string Registry_Discovered_Group_Caching = "DISCOVERED_GROUP_CACHING";


        public const string AI_EVALUATION_SEXUAL = "Explicit Sexual";
        public const string AI_EVALUATION_HATE = "Harassment & Bullying";
        public const string AI_EVALUATION_SELFHARM = "Self Harm";

        public const string Registry_XSOverlay_Level = "XS_OVERLAY_LEVEL";
        public const string XSOverlay_Level_None = "None";

        public static AlertTypeEnum AlertTypeEnumFromString(string alertType)
        {
            return alertType switch
            {
                "Watch" => AlertTypeEnum.Watch,
                "Nuisance" => AlertTypeEnum.Nuisance,
                "Crasher" => AlertTypeEnum.Crasher,
                _ => AlertTypeEnum.None
            };
        }
    }
}
