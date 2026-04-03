using System.Windows.Media;

namespace Tailgrab.PlayerManagement
{
    public class ColorOption
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public System.Windows.Media.Brush Brush { get; set; }

        public ColorOption(string name, string value)
        {
            Name = name;
            Value = value;
            Brush = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(value)!;
        }

        public override string ToString() => Name;
    }
}
