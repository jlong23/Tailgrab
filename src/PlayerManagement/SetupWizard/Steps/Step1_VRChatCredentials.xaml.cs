using NLog;
using System.Windows;
using System.Windows.Controls;

namespace Tailgrab.PlayerManagement.SetupWizard.Steps
{
    public partial class Step1_VRChatCredentials : System.Windows.Controls.UserControl
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public Step1_VRChatCredentials()
        {
            InitializeComponent();
            TwoFANo.Checked += TwoFA_Changed;
            TwoFAEmail.Checked += TwoFA_Changed;
            TwoFAAuthenticator.Checked += TwoFA_Changed;
        }

        private void TwoFA_Changed(object sender, RoutedEventArgs e)
        {
            TwoFAPanel.Visibility = TwoFAAuthenticator.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        public string GetUsername() => UsernameTextBox.Text?.Trim() ?? string.Empty;
        public string GetPassword() => PasswordBox.Password?.Trim() ?? string.Empty;
        public string GetTwoFAKey() => (TwoFAAuthenticator.IsChecked == true) ? (TwoFAKeyBox.Password?.Trim() ?? string.Empty) : string.Empty;

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
