using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using Tailgrab.Common;

namespace Tailgrab.PlayerManagement
{
    public class AIEvaluationManager
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static ServiceRegistry? serviceRegistry;

        public AIEvaluationManager(ServiceRegistry registry)
        {
            if (registry == null) {
                throw new ArgumentNullException(nameof(registry), "ServiceRegistry parameter cannot be null.");
            }
            serviceRegistry = registry;
        }

        // Evaluate the image evaluation text to determine if it contains any known classifications
        public static string? EvaluateImageClass(string? imageEvaluation)
        {
            if (string.IsNullOrEmpty(imageEvaluation))
            {
                return null;
            }

            if (CheckLines(imageEvaluation, CommonConst.AI_EVALUATION_SEXUAL))
            {
                return CommonConst.AI_EVALUATION_SEXUAL;
            }
            else if (CheckLines(imageEvaluation, CommonConst.AI_EVALUATION_HATE))
            {
                return CommonConst.AI_EVALUATION_HATE;
            }
            else if (CheckLines(imageEvaluation, CommonConst.AI_EVALUATION_SELFHARM))
            {
                return CommonConst.AI_EVALUATION_SELFHARM;
            }

            return null;
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
