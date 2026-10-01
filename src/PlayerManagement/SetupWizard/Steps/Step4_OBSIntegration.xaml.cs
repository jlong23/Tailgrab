using NLog;
using System.Windows;
using System.Windows.Controls;
using Tailgrab.Clients.OBS;
using Tailgrab.Common;

namespace Tailgrab.PlayerManagement.SetupWizard.Steps
{
    public partial class Step4_OBSIntegration : System.Windows.Controls.UserControl
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public Step4_OBSIntegration()
        {
            InitializeComponent();
            EnableOBSCheckbox.Checked += OBSEnabled_Changed;
            EnableOBSCheckbox.Unchecked += OBSEnabled_Changed;

            StartReplayBufferCheckbox.Checked += ReplayBuffer_Changed;
            StartReplayBufferCheckbox.Unchecked += ReplayBuffer_Changed;

            RecordKickBanCheckbox.Checked += KickBan_Changed;
            RecordKickBanCheckbox.Unchecked += KickBan_Changed;

            RecordImageCheckbox.Checked += ImageSpawn_Changed;
            RecordImageCheckbox.Unchecked += ImageSpawn_Changed;

            CreateMP4ChaptersCheckbox.Checked += MP4Chapters_Changed;
            CreateMP4ChaptersCheckbox.Unchecked += MP4Chapters_Changed;

            CreateMKVChaptersCheckbox.Checked += MKVChapters_Changed;
            CreateMKVChaptersCheckbox.Unchecked += MKVChapters_Changed;
            LoadSettings();
        }

        private void LoadSettings()
        {
            EnableOBSCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_Enable, false);
            OBSWebSocketURIBox.Text = ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_WSURI) ?? CommonConst.Default_OBS_WSURI;
            StartReplayBufferCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_StartReplayBuffer, false);
            StartVirtualCameraCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_StartVirtualCamera, false);
            RecordOnWorldJoinCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_StartRecordOnWorldJoin, false);
            RecordKickBanCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_SaveReplayBufferOnKickBan, false);
            KickBanSceneNameBox.Text = ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_Kick_Ban_Scene_Name) ?? string.Empty;
            KickBanBrowserNameBox.Text = ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_Kick_Ban_Browser_Name) ?? string.Empty;

            RecordImageCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_UserImageSpawnEvents, false);
            ImageEventSceneNameBox.Text = ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_ImageSpawn_SceneName) ?? string.Empty;
            ImageEventBrowserNameBox.Text = ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_ImageSpawn_BrowserName) ?? string.Empty;

            CreateMP4ChaptersCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_CreateChaptersMp4, false);
            string? storedFFMpegPath = ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_FFMpegPath);
            if(CreateMP4ChaptersCheckbox.IsChecked == true && string.IsNullOrEmpty(storedFFMpegPath))
            {
                // If no stored path, try to find ffmpeg in the system PATH
                string? ffmpegPath = Utility.FindFfmpeg();
                if (!string.IsNullOrEmpty(ffmpegPath))
                {
                    FFMpegPathBox.Text = ffmpegPath;
                }
                else
                {
                    FFMpegPathBox.Text = string.Empty;
                }
            }
            else
            {
                FFMpegPathBox.Text = storedFFMpegPath;
            }

            CreateMKVChaptersCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_CreateChaptersMkv, false);
            string? storedMKVMergePath = ConfigStore.GetStoredKeyString(CommonConst.Registry_OBS_MKVMergePath);
            if (CreateMKVChaptersCheckbox.IsChecked == true && string.IsNullOrEmpty(storedMKVMergePath))
            {
                // If no stored path, try to find mkvmerge in the system PATH
                string? mkvmergePath = Utility.FindMkvMerge();
                if (!string.IsNullOrEmpty(mkvmergePath))
                {
                    MKVMergePathBox.Text = mkvmergePath;
                }
                else
                {
                    MKVMergePathBox.Text = string.Empty;
                }
            }
            else
            {
                MKVMergePathBox.Text = storedMKVMergePath;
            }

            OBSEnabled_Changed(null, null);

        }

        private void OBSEnabled_Changed(object sender, RoutedEventArgs e)
        {
            OBSSettingsSection.Visibility = EnableOBSCheckbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ReplayBuffer_Changed(object sender, RoutedEventArgs e)
        {
            CustomSpawnSceneSection.Visibility = (RecordImageCheckbox.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            KickBanSceneSection.Visibility = (RecordKickBanCheckbox.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void KickBan_Changed(object sender, RoutedEventArgs e)
        {
            KickBanSceneSection.Visibility = (RecordKickBanCheckbox.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ImageSpawn_Changed(object sender, RoutedEventArgs e)
        {
            CustomSpawnSceneSection.Visibility = (RecordImageCheckbox.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MP4Chapters_Changed(object sender, RoutedEventArgs e)
        {
            FFMpegSection.Visibility = (CreateMP4ChaptersCheckbox.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MKVChapters_Changed(object sender, RoutedEventArgs e)
        {
            MKVMergeSection.Visibility = (CreateMKVChaptersCheckbox.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool IsOBSEnabled() => EnableOBSCheckbox.IsChecked == true;
        public string GetOBSWebSocketURI() => OBSWebSocketURIBox.Text?.Trim() ?? string.Empty;
        public string? GetOBSPassword() => string.IsNullOrEmpty(OBSPasswordBox.Password) ? null : OBSPasswordBox.Password;

        public void SaveSettings()
        {
            ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_Enable, EnableOBSCheckbox.IsChecked == true);
            ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_WSURI, GetOBSWebSocketURI());
            if (!string.IsNullOrEmpty(OBSPasswordBox.Password))
            {
                ConfigStore.SaveSecret(CommonConst.Registry_OBS_Password, OBSPasswordBox.Password);
            }
            ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_StartReplayBuffer, StartReplayBufferCheckbox.IsChecked == true);
            ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_StartVirtualCamera, StartVirtualCameraCheckbox.IsChecked == true);
            ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_StartRecordOnWorldJoin, RecordOnWorldJoinCheckbox.IsChecked == true);

            ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_SaveReplayBufferOnKickBan, RecordKickBanCheckbox.IsChecked == true);
            ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_Kick_Ban_Scene_Name, KickBanSceneNameBox.Text?.Trim() ?? string.Empty);
            ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_Kick_Ban_Browser_Name, KickBanBrowserNameBox.Text?.Trim() ?? string.Empty);


            ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_UserImageSpawnEvents, RecordImageCheckbox.IsChecked == true);
            ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_ImageSpawn_SceneName, ImageEventSceneNameBox.Text?.Trim() ?? string.Empty);
            ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_ImageSpawn_BrowserName, ImageEventBrowserNameBox.Text?.Trim() ?? string.Empty);

            ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_CreateChaptersMp4, CreateMP4ChaptersCheckbox.IsChecked == true);
            ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_FFMpegPath, FFMpegPathBox.Text?.Trim() ?? string.Empty);

            ConfigStore.PutStoredKeyBool(CommonConst.Registry_OBS_CreateChaptersMkv, CreateMKVChaptersCheckbox.IsChecked == true);
            ConfigStore.PutStoredKeyString(CommonConst.Registry_OBS_MKVMergePath, MKVMergePathBox.Text?.Trim() ?? string.Empty);
        }

        public void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorMessageText.Visibility = Visibility.Visible;
        }

        public void ClearError()
        {
            ErrorMessageText.Visibility = Visibility.Collapsed;
        }

        private void Hyperlink_RequestNavigate(object? sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                logger.Info($"Opening print URL: {e.Uri}");
                var psi = new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
                {
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to open print URL");
            }
            e.Handled = true;
        }

    }
}
