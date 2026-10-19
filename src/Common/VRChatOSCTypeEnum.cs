using System;
using System.Collections.Generic;
using System.Text;

namespace Tailgrab.Common
{
    public enum VRChatOSCTypeEnum
    {
        b,

        i,

        f,

        s
    }

    public class VRChatOSCTypeEnumMapper
    {
        public static string MapEnumToDescription(VRChatOSCTypeEnum myEnum)
        {
            var resources = System.Windows.Application.Current.Resources;
            string key = myEnum switch
            {
                VRChatOSCTypeEnum.b => "Boolean",
                VRChatOSCTypeEnum.f => "Float",
                VRChatOSCTypeEnum.i => "Integer",
                VRChatOSCTypeEnum.s => "String",
                _ => "String",
            };
            return (string)resources[key];
        }

        public static VRChatOSCTypeEnum MapStringToEnum(string strType)
        {
            return strType.ToLower() switch
            {
                "s" or "str" or "string" => VRChatOSCTypeEnum.s,
                "b" or "bool" or "boolean" => VRChatOSCTypeEnum.b,
                "f" or "flt" or "float" => VRChatOSCTypeEnum.f,
                "i" or "int" or "integer" => VRChatOSCTypeEnum.i,
                _ => VRChatOSCTypeEnum.s
            };
        }
    }
}