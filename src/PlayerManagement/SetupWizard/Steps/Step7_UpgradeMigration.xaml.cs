using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Tailgrab.Common;

namespace Tailgrab.PlayerManagement.SetupWizard.Steps
{
    public partial class Step7_UpgradeMigration : System.Windows.Controls.UserControl
    {
        public Step7_UpgradeMigration()
        {
            InitializeComponent();
        }

        public void PopulateStatus()
        {
            // Get version information
            try
            {
                string? versionText = System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BuildVersion.txt"))
                    ? System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BuildVersion.txt")).Trim()
                    : "1.1.6";

                InstalledVersionText.Text = versionText;
                CurrentVersionText.Text = versionText;
            }
            catch
            {
                InstalledVersionText.Text = "Unknown";
                CurrentVersionText.Text = "Unknown";
            }

            // Check configuration status
            var configStatus = new List<string>();

            bool hasVRChat = !string.IsNullOrEmpty(ConfigStore.LoadSecret(CommonConst.Registry_VRChat_Web_UserName));
            configStatus.Add($"✓ VRChat Credentials: {(hasVRChat ? "Configured" : "Not Configured")}");

            bool hasGist = !string.IsNullOrEmpty(ConfigStore.GetStoredKeyString(CommonConst.Registry_Avatar_Gist)) ||
                          !string.IsNullOrEmpty(ConfigStore.GetStoredKeyString(CommonConst.Registry_Group_Gist));
            configStatus.Add($"✓ Gist Configuration: {(hasGist ? "Configured" : "Not Configured")}");

            bool hasOBS = ConfigStore.GetStoredKeyBool(CommonConst.Registry_OBS_Enable, false) == true;
            configStatus.Add($"✓ OBS Integration: {(hasOBS ? "Enabled" : "Disabled")}");

            bool hasOllama = !string.IsNullOrEmpty(ConfigStore.GetStoredKeyString(CommonConst.Registry_Ollama_API_Endpoint));
            configStatus.Add($"✓ AI Evaluation: {(hasOllama ? "Configured" : "Not Configured")}");

            // Add items to config status panel
            ConfigStatusStackPanel.Children.Clear();
            foreach (var status in configStatus)
            {
                var tb = new TextBlock
                {
                    Text = status,
                    Foreground = System.Windows.Media.Brushes.DimGray,
                    Margin = new System.Windows.Thickness(0, 0, 0, 8)
                };
                ConfigStatusStackPanel.Children.Add(tb);
            }

            // Populate completed steps
            CompletedStepsListBox.Items.Clear();
            CompletedStepsListBox.Items.Add("Step 1: VRChat Credentials - Tested and validated");
            CompletedStepsListBox.Items.Add("Step 2: Gist Configuration - Configured");
            CompletedStepsListBox.Items.Add("Step 3: Behavior Flags - Set");
            CompletedStepsListBox.Items.Add("Step 4: OBS Integration - Configured");
            CompletedStepsListBox.Items.Add("Step 5: AI Evaluation - Configured");
            CompletedStepsListBox.Items.Add("Step 6: MCP Server - Configured");
        }
    }
}
