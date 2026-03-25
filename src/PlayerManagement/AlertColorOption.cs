using System.Windows.Media;

namespace Tailgrab.PlayerManagement
{
    public class AlertColorOption
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public System.Windows.Media.Brush BackgroundBrush { get; set; }
        public System.Windows.Media.Brush ForegroundBrush { get; set; }

        public AlertColorOption(string name, string value, System.Windows.Media.Brush backgroundBrush, System.Windows.Media.Brush foregroundBrush)
        {
            Name = name;
            Value = value;
            BackgroundBrush = backgroundBrush;
            ForegroundBrush = foregroundBrush;
        }

        public override string ToString() => Name;
    }
}
