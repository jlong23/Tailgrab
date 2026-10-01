using NLog;
using System.Windows;
using System.Windows.Controls;
using Tailgrab.Common;

namespace Tailgrab.PlayerManagement.SetupWizard.Steps
{
    public partial class Step2_GistConfiguration : System.Windows.Controls.UserControl
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public Step2_GistConfiguration()
        {
            InitializeComponent();
            HaveSharedGist.Checked += ConfigType_Changed;
            NoSharedGist.Checked += ConfigType_Changed;
            IsGistAdmin.Checked += Admin_Changed;
            NotGistAdmin.Checked += Admin_Changed;
        }

        private void ConfigType_Changed(object sender, RoutedEventArgs e)
        {
            bool useShared = HaveSharedGist.IsChecked == true;
            SharedGistSection.Visibility = useShared ? Visibility.Visible : Visibility.Collapsed;
            ManualUrlsSection.Visibility = useShared ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Admin_Changed(object sender, RoutedEventArgs e)
        {
            PATSection.Visibility = (IsGistAdmin.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool UseSharedGist() => HaveSharedGist.IsChecked == true;
        public bool IsSkipped() => SkipGistCheckbox.IsChecked == true;
        public string GetSharedGistId() => SharedGistIdBox.Text?.Trim() ?? string.Empty;
        public string? GetGitHubPAT() => string.IsNullOrEmpty(GitHubPATBox.Password) ? null : GitHubPATBox.Password;
        public string GetAvatarGistUrl() => AvatarGistUrlBox.Text?.Trim() ?? string.Empty;
        public string GetGroupGistUrl() => GroupGistUrlBox.Text?.Trim() ?? string.Empty;

        public void SaveSettings()
        {
            if (IsSkipped())
            {
                ConfigStore.RemoveStoredKeyString(CommonConst.Registry_Github_Gist_PAT);
                ConfigStore.RemoveStoredKeyString(CommonConst.Registry_Github_Gist_ID);
                ConfigStore.RemoveStoredKeyString(CommonConst.Registry_Github_Use_Automation);
                ConfigStore.RemoveStoredKeyString(CommonConst.Registry_Avatar_Gist);
                ConfigStore.RemoveStoredKeyString(CommonConst.Registry_Group_Gist);
                return;
            }

            if (UseSharedGist())
            {
                string gistId = GetSharedGistId();
                ConfigStore.PutStoredKeyString(CommonConst.Registry_Github_Gist_ID, gistId);
                ConfigStore.PutStoredKeyBool(CommonConst.Registry_Github_Use_Automation, true);

                if (IsGistAdmin.IsChecked == true && !string.IsNullOrEmpty(GitHubPATBox.Password))
                {
                    ConfigStore.SaveSecret(CommonConst.Registry_Github_Gist_PAT, GitHubPATBox.Password);
                }
            }
            else
            {
                string avatarUrl = GetAvatarGistUrl();
                string groupUrl = GetGroupGistUrl();
                ConfigStore.PutStoredKeyString(CommonConst.Registry_Avatar_Gist, avatarUrl);
                ConfigStore.PutStoredKeyString(CommonConst.Registry_Group_Gist, groupUrl);
            }
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
