using NLog;
using System.Windows;
using System.Windows.Controls;
using Tailgrab.Common;

namespace Tailgrab.PlayerManagement.SetupWizard.Steps
{
    public partial class Step5_AIEvaluation : System.Windows.Controls.UserControl
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public Step5_AIEvaluation()
        {
            InitializeComponent();
            EnableAICheckbox.Checked += AIEnabled_Changed;
            EnableAICheckbox.Unchecked += AIEnabled_Changed;
            LoadSettings();
        }

        private void LoadSettings()
        {
            EnableAICheckbox.IsChecked = !string.IsNullOrEmpty(ConfigStore.LoadSecret(CommonConst.Registry_Ollama_API_Key));
            OLLAMAEndpointBox.Text = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Endpoint) ?? CommonConst.Default_Ollama_API_Endpoint;
            OLLAMAModelBox.Text = ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Model) ?? CommonConst.Default_Ollama_API_Model;
            AIEnabled_Changed(null, null);
        }

        private void AIEnabled_Changed(object sender, RoutedEventArgs e)
        {
            AISettingsSection.Visibility = EnableAICheckbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool IsAIEnabled() => EnableAICheckbox.IsChecked == true;
        public string GetOLLAMAEndpoint() => OLLAMAEndpointBox.Text?.Trim() ?? CommonConst.Default_Ollama_API_Endpoint;
        public string GetOLLAMAModel() => OLLAMAModelBox.Text?.Trim() ?? CommonConst.Default_Ollama_API_Model;
        public string? GetOLLAMAKey() => string.IsNullOrEmpty(OLLAMAKeyBox.Password) ? null : OLLAMAKeyBox.Password;

        public void SaveSettings()
        {
            if (IsAIEnabled())
            {
                ConfigStore.PutStoredKeyString(CommonConst.Registry_Ollama_API_Endpoint, GetOLLAMAEndpoint());
                ConfigStore.PutStoredKeyString(CommonConst.Registry_Ollama_API_Model, GetOLLAMAModel());
                if (!string.IsNullOrEmpty(OLLAMAKeyBox.Password))
                {
                    ConfigStore.SaveSecret(CommonConst.Registry_Ollama_API_Key, OLLAMAKeyBox.Password);
                }
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
