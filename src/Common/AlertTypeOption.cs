using Tailgrab.Common;

namespace Tailgrab.Common
{
    public class AlertTypeOption(string displayName, AlertTypeEnum value)
    {
        public string DisplayName { get; set; } = displayName;
        public AlertTypeEnum Value { get; set; } = value;

        public override string ToString() => DisplayName;
    }
}
