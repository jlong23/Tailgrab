using System;
using System.Collections.Generic;
using System.Text;

namespace Tailgrab.Common
{
    public static class Utility
    {
        public static string GetCurrentDateTimeString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static string I18NString(string key) {

            var resources = System.Windows.Application.Current.Resources;
            // Implementation for retrieving the internationalized string based on the key
            return (string)resources[key];
        }
    }
}
