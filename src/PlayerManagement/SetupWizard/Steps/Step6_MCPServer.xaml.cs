using System.Windows;
using System.Windows.Controls;
using Tailgrab.Common;

namespace Tailgrab.PlayerManagement.SetupWizard.Steps
{
    public partial class Step6_MCPServer : System.Windows.Controls.UserControl
    {
        public Step6_MCPServer()
        {
            InitializeComponent();
            EnableMCPCheckbox.Checked += MCPEnabled_Changed;
            EnableMCPCheckbox.Unchecked += MCPEnabled_Changed;
            LoadSettings();
        }

        private void LoadSettings()
        {
            // MCP settings might not be in CommonConst yet, so we use generic keys
            EnableMCPCheckbox.IsChecked = ConfigStore.GetStoredKeyBool(CommonConst.Registry_AI_MCP_Server_Enabled, false);

            var mcpServerPort = ConfigStore.GetStoredKeyString(CommonConst.Registry_AI_MCP_Server_Port);
            MCPPortBox.Text = mcpServerPort ?? CommonConst.Default_AI_MCP_Server_Port;
            MCPEnabled_Changed(null, null);
        }

        private void MCPEnabled_Changed(object? sender, RoutedEventArgs? e)
        {
            MCPSettingsSection.Visibility = EnableMCPCheckbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        public bool IsMCPEnabled() => EnableMCPCheckbox.IsChecked == true;

        public int GetMCPPort()
        {
            if (int.TryParse(MCPPortBox.Text, out int port))
            {
                return port;
            }
            return 3000;
        }

        public void SaveSettings()
        {
            ConfigStore.PutStoredKeyBool(CommonConst.Registry_AI_MCP_Server_Enabled, IsMCPEnabled());
            ConfigStore.PutStoredKeyString(CommonConst.Registry_AI_MCP_Server_Port, GetMCPPort().ToString() ?? CommonConst.Default_AI_MCP_Server_Port);
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
    }
}
