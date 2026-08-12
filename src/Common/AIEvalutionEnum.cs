using System.Windows.Media;
using Tailgrab.PlayerManagement;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Tailgrab.Common
{
    public enum AIEvalutionEnum
    {
        NOT_AVAILABLE = 0,
        INVALID_RESPONSE = 1,
        OK = 2,
        EXPLICIT_SEXUAL = 3,
        HARASSMENT_AND_BULLYING = 4,
        SELF_HARM = 5,
    }

    public class AIEvalutionEnumMapper
    {
        public static AIEvalutionEnum MapEvaluationToEnum(string? imageEvaluation)
        {
            if (string.IsNullOrEmpty(imageEvaluation))
                return AIEvalutionEnum.NOT_AVAILABLE;

            if (CheckLines(imageEvaluation, CommonConst.AI_EVALUATION_OK))
                return AIEvalutionEnum.OK;

            if (CheckLines(imageEvaluation, CommonConst.AI_EVALUATION_SEXUAL))
                return AIEvalutionEnum.EXPLICIT_SEXUAL;

            if (CheckLines(imageEvaluation, CommonConst.AI_EVALUATION_HATE))
                return AIEvalutionEnum.HARASSMENT_AND_BULLYING;

            if (CheckLines(imageEvaluation, CommonConst.AI_EVALUATION_SELFHARM))
                return AIEvalutionEnum.SELF_HARM;

            return AIEvalutionEnum.INVALID_RESPONSE;
        }

        public static string MapEnumToDescription(AIEvalutionEnum aIEvalutionEnum)
        {
            return aIEvalutionEnum switch
            {
                AIEvalutionEnum.NOT_AVAILABLE => CommonConst.AI_EVALUATION_NOT_AVAILABLE,
                AIEvalutionEnum.INVALID_RESPONSE => CommonConst.AI_EVALUATION_INVALID_RESPONSE,
                AIEvalutionEnum.OK => CommonConst.AI_EVALUATION_OK,
                AIEvalutionEnum.EXPLICIT_SEXUAL => CommonConst.AI_EVALUATION_SEXUAL,
                AIEvalutionEnum.HARASSMENT_AND_BULLYING => CommonConst.AI_EVALUATION_HATE,
                AIEvalutionEnum.SELF_HARM => CommonConst.AI_EVALUATION_SELFHARM,
                _ => "Unknown evaluation",
            };
        }

        public static AlertDisplayItem MapEnumToAlertDisplayItem(string status)
        {
            AIEvalutionEnum item = MapEvaluationToEnum(status);
            return MapEnumToAlertDisplayItem(item);
        }

        public static AlertDisplayItem MapEnumToAlertDisplayItem(AIEvalutionEnum aIEvalutionEnum)
        {
            return new AlertDisplayItem
            {
                IconGeometry = MapEnumToIcon(aIEvalutionEnum),
                IconBrush = MapEnumToBrush(aIEvalutionEnum),
                IconClass = "User Trust",
                Description = MapEnumToDescription(aIEvalutionEnum),
                AlertColor = MapEnumToBrush(aIEvalutionEnum).ToString()
            };
        }

        public static Geometry MapEnumToIcon(AIEvalutionEnum aIEvalutionEnum)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = aIEvalutionEnum switch
            {
                AIEvalutionEnum.NOT_AVAILABLE => "Icon.AIEvalutionEnum.NOT_AVAILABLE",
                AIEvalutionEnum.INVALID_RESPONSE => "Icon.AIEvalutionEnum.INVALID_RESPONSE",
                AIEvalutionEnum.OK => "Icon.AIEvalutionEnum.OK",
                AIEvalutionEnum.EXPLICIT_SEXUAL => "Icon.AIEvalutionEnum.EXPLICIT_SEXUAL",
                AIEvalutionEnum.HARASSMENT_AND_BULLYING => "Icon.AIEvalutionEnum.HARASSMENT_AND_BULLYING",
                AIEvalutionEnum.SELF_HARM => "Icon.AIEvalutionEnum.SELF_HARM",
                _ => "Icon.AIEvalutionEnum.Default",
            };
            return (Geometry)resources[key];
        }

        public static WpfBrush MapEnumToBrush(AIEvalutionEnum aIEvalutionEnum)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = aIEvalutionEnum switch
            {
                AIEvalutionEnum.NOT_AVAILABLE => "Brush.AIEvalutionEnum.NOT_AVAILABLE",
                AIEvalutionEnum.INVALID_RESPONSE => "Brush.AIEvalutionEnum.INVALID_RESPONSE",
                AIEvalutionEnum.OK => "Brush.AIEvalutionEnum.OK",
                AIEvalutionEnum.EXPLICIT_SEXUAL => "Brush.AIEvalutionEnum.EXPLICIT_SEXUAL",
                AIEvalutionEnum.HARASSMENT_AND_BULLYING => "Brush.AIEvalutionEnum.HARASSMENT_AND_BULLYING",
                AIEvalutionEnum.SELF_HARM => "Brush.AIEvalutionEnum.SELF_HARM",
                _ => "Brush.AIEvalutionEnum.Default",
            };
            return (WpfBrush)resources[key];
        }


        private static bool CheckLines(string input, string knownString)
        {
            string[] lines = input.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                return false;
            }

            bool firstLineContains = lines[0].Contains(knownString);

            return firstLineContains;
        }

    }
}
