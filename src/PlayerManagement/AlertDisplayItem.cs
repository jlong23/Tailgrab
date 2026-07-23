using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace Tailgrab.PlayerManagement
{
    /// <summary>
    /// Represents a display item for alert messages with icon and text
    /// </summary>
    public class AlertDisplayItem
    {
        public Geometry? IconGeometry { get; set; }
        public WpfBrush IconBrush { get; set; } = WpfBrushes.White;

        public string IconClass { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AlertColor { get; set; } = string.Empty;
    }
}
