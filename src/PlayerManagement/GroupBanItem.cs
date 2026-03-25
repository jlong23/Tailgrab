using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Tailgrab.PlayerManagement
{
    public class GroupBanItem : INotifyPropertyChanged
    {
        private string _groupId = string.Empty;
        private string _groupName = string.Empty;
        private string _status = "Not Checked";
        private bool _canBan = false;
        private bool _canUnban = false;
        private System.Windows.Media.Brush _statusColor = System.Windows.Media.Brushes.Gray;

        public string GroupId
        {
            get => _groupId;
            set
            {
                if (_groupId != value)
                {
                    _groupId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GroupName
        {
            get => _groupName;
            set
            {
                if (_groupName != value)
                {
                    _groupName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    UpdateStatusColor();
                }
            }
        }

        public bool CanBan
        {
            get => _canBan;
            set
            {
                if (_canBan != value)
                {
                    _canBan = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanUnban
        {
            get => _canUnban;
            set
            {
                if (_canUnban != value)
                {
                    _canUnban = value;
                    OnPropertyChanged();
                }
            }
        }

        public System.Windows.Media.Brush StatusColor
        {
            get => _statusColor;
            private set
            {
                if (_statusColor != value)
                {
                    _statusColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private void UpdateStatusColor()
        {
            StatusColor = Status switch
            {
                "Member" => System.Windows.Media.Brushes.LightGreen,
                "Banned" => System.Windows.Media.Brushes.Red,
                "Not Member" => System.Windows.Media.Brushes.Yellow,
                _ => System.Windows.Media.Brushes.Gray
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
