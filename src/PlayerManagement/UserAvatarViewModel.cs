using System.ComponentModel;
using System.Windows.Media;
using Tailgrab.Common;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;

namespace Tailgrab.PlayerManagement
{
    public class UserAvatarViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string AvatarId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? IsPC { get; set; } = string.Empty;
        public string? IsQuest { get; set; } = string.Empty;
        public string? IsIOS { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsOwnedByUser { get; set; }
        public bool ExistsInDatabase { get; set; }

        public AlertDisplayItem? PCPerformance { get; set; }
        public AlertDisplayItem? QuestPerformance { get; set; }
        public AlertDisplayItem? IOSPerformance { get; set; }


        private AlertTypeEnum _databaseAlertType = AlertTypeEnum.None;
        public AlertTypeEnum DatabaseAlertType
        {
            get => _databaseAlertType;
            set
            {
                if (_databaseAlertType != value)
                {
                    _databaseAlertType = value;
                    OnPropertyChanged(nameof(DatabaseAlertType));
                    OnPropertyChanged(nameof(CanAdd));
                }
            }
        }

        private AlertTypeEnum _alertType = AlertTypeEnum.None;
        public AlertTypeEnum AlertType
        {
            get => _alertType;
            set
            {
                if (_alertType != value)
                {
                    _alertType = value;
                    OnPropertyChanged(nameof(AlertType));
                    OnPropertyChanged(nameof(CanAdd));
                    UpdateAlertColors();
                }
            }
        }

        private WpfBrush _alertBackground = WpfBrushes.Transparent;
        public WpfBrush AlertBackground
        {
            get => _alertBackground;
            set
            {
                if (_alertBackground != value)
                {
                    _alertBackground = value;
                    OnPropertyChanged(nameof(AlertBackground));
                }
            }
        }

        private WpfBrush _alertForeground = new SolidColorBrush(WpfColor.FromRgb(0xE6, 0xE6, 0xE6));
        public WpfBrush AlertForeground
        {
            get => _alertForeground;
            set
            {
                if (_alertForeground != value)
                {
                    _alertForeground = value;
                    OnPropertyChanged(nameof(AlertForeground));
                }
            }
        }

        public bool CanAdd
        {
            get
            {
                // If record doesn't exist in database, button is enabled
                if (!ExistsInDatabase && AlertType != AlertTypeEnum.None)
                    return true;

                // If record exists but AlertType differs from database value, button is enabled
                if (ExistsInDatabase && AlertType != DatabaseAlertType)
                    return true;

                // If record exists and AlertType matches database value, button is disabled
                return false;
            }
        }

        public void UpdateAlertColors()
        {
            if (AlertType != AlertTypeEnum.None)
            {
                string colorClass = PlayerManager.GetAlertColor(AlertClassEnum.Group, AlertType);

                // Get the colors based on the highlight class
                string backgroundKey = $"HIGHLIGHT_{colorClass.ToUpper()}_BG";
                string foregroundKey = $"HIGHLIGHT_{colorClass.ToUpper()}_FG";

                string bgColor = ConfigStore.GetStoredKeyString($"{CommonConst.ConfigRegistryPath}\\{backgroundKey}", string.Empty) ?? "Transparent";
                string fgColor = ConfigStore.GetStoredKeyString($"{CommonConst.ConfigRegistryPath}\\{foregroundKey}", string.Empty) ?? "#FFE6E6E6";

                try
                {
                    AlertBackground = (WpfBrush)new BrushConverter().ConvertFromString(bgColor)!;
                    AlertForeground = (WpfBrush)new BrushConverter().ConvertFromString(fgColor)!;
                }
                catch
                {
                    AlertBackground = WpfBrushes.Transparent;
                    AlertForeground = new SolidColorBrush(WpfColor.FromRgb(0xE6, 0xE6, 0xE6));
                }
            }
            else
            {
                AlertBackground = WpfBrushes.Transparent;
                AlertForeground = new SolidColorBrush(WpfColor.FromRgb(0xE6, 0xE6, 0xE6));
            }
        }

        public override string ToString()
        {
            return $"UserAvatarViewModel: AvatarId={AvatarId}, Name={Name}, ThumbnailUrl={ThumbnailUrl}, Description={Description}, IsPC={IsPC}, IsQuest={IsQuest}, IsIOS={IsIOS}, OwnerId={OwnerId}, IsOwnedByUser={IsOwnedByUser}, ExistsInDatabase={ExistsInDatabase}, AlertType={AlertType}";
        }
    }
}
