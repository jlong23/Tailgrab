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
            return aIEvalutionEnum switch
            {
                AIEvalutionEnum.NOT_AVAILABLE => Geometry.Parse("M11.83,9L15,12.16C15,12.11 15,12.05 15,12A3,3 0 0,0 12,9C11.94,9 11.89,9 11.83,9M7.53,9.8L9.08,11.35C9.03,11.56 9,11.77 9,12A3,3 0 0,0 12,15C12.22,15 12.44,14.97 12.65,14.92L14.2,16.47C13.53,16.8 12.79,17 12,17A5,5 0 0,1 7,12C7,11.21 7.2,10.47 7.53,9.8M2,4.27L4.28,6.55L4.73,7C3.08,8.3 1.78,10 1,12C2.73,16.39 7,19.5 12,19.5C13.55,19.5 15.03,19.2 16.38,18.66L16.81,19.08L19.73,22L21,20.73L3.27,3M12,7A5,5 0 0,1 17,12C17,12.64 16.87,13.26 16.64,13.82L19.57,16.75C21.07,15.5 22.27,13.86 23,12C21.27,7.61 17,4.5 12,4.5C10.6,4.5 9.26,4.75 8,5.2L10.17,7.35C10.74,7.13 11.35,7 12,7Z"),
                AIEvalutionEnum.INVALID_RESPONSE => Geometry.Parse("M12 18.5C12 19 12.07 19.5 12.18 20H6.5C5 20 3.69 19.5 2.61 18.43C1.54 17.38 1 16.09 1 14.58C1 13.28 1.39 12.12 2.17 11.1S4 9.43 5.25 9.15C5.67 7.62 6.5 6.38 7.75 5.43S10.42 4 12 4C13.95 4 15.6 4.68 16.96 6.04C18.32 7.4 19 9.05 19 11C20.15 11.13 21.1 11.63 21.86 12.5C22.1 12.76 22.29 13.05 22.46 13.36C21.36 12.5 20 12 18.5 12C14.91 12 12 14.91 12 18.5M23 18.5C23 21 21 23 18.5 23S14 21 14 18.5 16 14 18.5 14 23 16 23 18.5M20 21.08L15.92 17C15.65 17.42 15.5 17.94 15.5 18.5C15.5 20.16 16.84 21.5 18.5 21.5C19.06 21.5 19.58 21.35 20 21.08M21.5 18.5C21.5 16.84 20.16 15.5 18.5 15.5C17.94 15.5 17.42 15.65 17 15.92L21.08 20C21.35 19.58 21.5 19.06 21.5 18.5Z"),
                AIEvalutionEnum.OK => Geometry.Parse("M56 74.31 43.66 62a5.68 5.68 0 0 0-8 8L52 86.35a5.53 5.53 0 0 0 8 0L92.37 54a5.68 5.68 0 0 0-8-8zM64 121.31a55.76 55.76 0 0 1-22.35-4.51 57.25 57.25 0 0 1-30.44-30.45A55.83 55.83 0 0 1 6.7 64a55.66 55.66 0 0 1 4.51-22.35 57.25 57.25 0 0 1 30.44-30.44A55.83 55.83 0 0 1 64 6.7a55.83 55.83 0 0 1 22.35 4.51 57.25 57.25 0 0 1 30.44 30.44A55.32 55.32 0 0 1 121.3 64a56.17 56.17 0 0 1-4.51 22.35 57.25 57.25 0 0 1-30.44 30.44A55.42 55.42 0 0 1 64 121.3"),
                AIEvalutionEnum.EXPLICIT_SEXUAL => Geometry.Parse("M76.82 8.43c-4.46 4.46-6.77 12.23-5.09 18.31 0.9 3.22 1 7.11-1.32 9.47l-34.2 34.2c-2.36 2.36-6.25 2.22-9.48 1.32-6.07-1.68-13.84 0.63-18.3 5.09A15.11 15.11 0 0 0 29.8 98.2v0a15.11 15.11 0 0 0 21.38 21.37v0c4.46-4.46 6.77-12.23 5.09-18.31-0.9-3.22-1-7.11 1.32-9.47l34.2-34.2c2.36-2.36 6.25-2.22 9.48-1.32 6.07 1.68 13.84-0.63 18.3-5.09A15.11 15.11 0 0 0 98.2 29.8 15.11 15.11 0 0 0 76.82 8.43"),
                AIEvalutionEnum.HARASSMENT_AND_BULLYING => Geometry.Parse("M124 64a60 60 0 1 0-60 60 60 60 0 0 0 60-60M64 29.5a4.49 4.49 0 0 1 4.5 4.5v36a4.5 4.5 0 0 1-9 0V34a4.49 4.49 0 0 1 4.5-4.5M64 94a6 6 0 1 0-6-6 6 6 0 0 0 6 6"),
                AIEvalutionEnum.SELF_HARM => Geometry.Parse("M115.35 4.42c20 33.42-48.55 108.14-48.55 108.14l-17.11-17.1-27.94 28.12L9 110.83z"),
                _ => Geometry.Parse("M11.83,9L15,12.16C15,12.11 15,12.05 15,12A3,3 0 0,0 12,9C11.94,9 11.89,9 11.83,9M7.53,9.8L9.08,11.35C9.03,11.56 9,11.77 9,12A3,3 0 0,0 12,15C12.22,15 12.44,14.97 12.65,14.92L14.2,16.47C13.53,16.8 12.79,17 12,17A5,5 0 0,1 7,12C7,11.21 7.2,10.47 7.53,9.8M2,4.27L4.28,6.55L4.73,7C3.08,8.3 1.78,10 1,12C2.73,16.39 7,19.5 12,19.5C13.55,19.5 15.03,19.2 16.38,18.66L16.81,19.08L19.73,22L21,20.73L3.27,3M12,7A5,5 0 0,1 17,12C17,12.64 16.87,13.26 16.64,13.82L19.57,16.75C21.07,15.5 22.27,13.86 23,12C21.27,7.61 17,4.5 12,4.5C10.6,4.5 9.26,4.75 8,5.2L10.17,7.35C10.74,7.13 11.35,7 12,7Z"),
            };
        }

        public static WpfBrush MapEnumToBrush(AIEvalutionEnum aIEvalutionEnum)
        {
            return aIEvalutionEnum switch
            {
                AIEvalutionEnum.NOT_AVAILABLE => WpfBrushes.Gray,
                AIEvalutionEnum.INVALID_RESPONSE => WpfBrushes.Yellow,
                AIEvalutionEnum.OK => WpfBrushes.Green,
                AIEvalutionEnum.EXPLICIT_SEXUAL => WpfBrushes.Red,
                AIEvalutionEnum.HARASSMENT_AND_BULLYING => WpfBrushes.Orange,
                AIEvalutionEnum.SELF_HARM => WpfBrushes.Red,
                _ => WpfBrushes.Transparent,
            };
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
