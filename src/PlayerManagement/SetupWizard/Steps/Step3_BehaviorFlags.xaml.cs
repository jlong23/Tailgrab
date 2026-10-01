using System.Windows.Controls;
using Tailgrab.Common;

namespace Tailgrab.PlayerManagement.SetupWizard.Steps
{
    public partial class Step3_BehaviorFlags : System.Windows.Controls.UserControl
    {
        private AlertTypeEnum _selectedXSOverlayLevel;

        public AlertTypeEnum SelectedXSOverlayLevel
        {
            get => _selectedXSOverlayLevel;
            set
            {
                if (_selectedXSOverlayLevel != value)
                {
                    _selectedXSOverlayLevel = value;
                }
            }
        }

        public Step3_BehaviorFlags()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            DiscoveredAvatarCachingCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Discovered_Avatar_Caching, false);
            DiscoveredGroupCachingCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Discovered_Group_Caching, false);
            ModeratedAvatarCachingCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_Moderated_Avatar_Caching, true);
            var xsOverlayLevel = ConfigStore.GetStoredKeyString(CommonConst.Registry_XSOverlay_Level) ?? CommonConst.XSOverlay_Level_None;
            SelectedXSOverlayLevel = AlertTypeEnumMapper.MapStringToEnum(xsOverlayLevel);
        }

        public void SaveSettings()
        {
            ConfigStore.PutStoredKeyBool(CommonConst.Registry_Discovered_Avatar_Caching, DiscoveredAvatarCachingCheckbox.IsChecked ?? false);
            ConfigStore.PutStoredKeyBool(CommonConst.Registry_Moderated_Avatar_Caching, ModeratedAvatarCachingCheckbox.IsChecked ?? false);
            ConfigStore.PutStoredKeyBool(CommonConst.Registry_Discovered_Group_Caching, DiscoveredGroupCachingCheckbox.IsChecked ?? false);
            ConfigStore.PutStoredKeyString(CommonConst.Registry_XSOverlay_Level, AlertTypeEnumMapper.MapEnumToString(SelectedXSOverlayLevel));
        }
    }
}
