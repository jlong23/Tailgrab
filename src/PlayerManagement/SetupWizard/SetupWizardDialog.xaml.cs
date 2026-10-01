using NLog;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Tailgrab.Common;
using Tailgrab.Configuration;
using Tailgrab.PlayerManagement.SetupWizard.Steps;

namespace Tailgrab.PlayerManagement.SetupWizard
{
    public partial class SetupWizardDialog : Window
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private int currentStep = 0;
        private List<(string Title, string Description, System.Windows.Controls.UserControl StepControl)> steps = [];
        private Dictionary<int, Func<Task<bool>>> stepValidators = [];

        #region Base Window Setup & Control
        public SetupWizardDialog()
        {
            InitializeComponent();
            InitializeSteps();
            ShowStep(0);
        }

        private void InitializeSteps()
        {
            steps.Add(("VRChat Credentials", "Step 1 of 7: Enter your VRChat Web API credentials", new Step1_VRChatCredentials()));
            steps.Add(("GitHub Gist Configuration", "Step 2 of 7: Configure your shared watched avatar & group settings", new Step2_GistConfiguration()));
            steps.Add(("Behavior Flags", "Step 3 of 7: Configure caching behavior", new Step3_BehaviorFlags()));
            steps.Add(("OBS Integration", "Step 4 of 7: Configure OBS integration", new Step4_OBSIntegration()));
            steps.Add(("AI Evaluation", "Step 5 of 7: Configure AI evaluation", new Step5_AIEvaluation()));
            steps.Add(("MCP Server", "Step 6 of 7: Configure MCP server", new Step6_MCPServer()));
            steps.Add(("Upgrade Migration", "Step 7 of 7: Review and complete setup", new Step7_UpgradeMigration()));

            // Register step validators
            stepValidators[0] = ValidateStep1;
            stepValidators[1] = ValidateStep2;
            stepValidators[2] = ValidateStep3;
            stepValidators[3] = ValidateStep4;
            stepValidators[4] = ValidateStep5;
            stepValidators[5] = ValidateStep6;
            stepValidators[6] = ValidateStep7;
        }

        private void ShowStep(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= steps.Count)
                return;

            currentStep = stepIndex;
            var (title, description, control) = steps[stepIndex];

            StepTitleText.Text = title;
            StepDescriptionText.Text = description;
            StepContentPresenter.Content = control;
            StepCountText.Text = $"Step {stepIndex + 1} of {steps.Count}";
            StepProgressBar.Value = ((stepIndex + 1) * 100.0 / steps.Count);

            // Update button states
            BackButton.IsEnabled = stepIndex > 0;
            NextButton.Content = (stepIndex == steps.Count - 1) ? "Finish" : "Next";
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                NextButton.IsEnabled = false;
                BackButton.IsEnabled = false;

                // Validate current step
                if (stepValidators.ContainsKey(currentStep))
                {
                    bool isValid = await stepValidators[currentStep]();
                    if (!isValid)
                    {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                        NextButton.IsEnabled = true;
                        BackButton.IsEnabled = currentStep > 0;                        
                        return;
                    }
                }
                
                LoadingOverlay.Visibility = Visibility.Collapsed;
                NextButton.IsEnabled = true;
                BackButton.IsEnabled = true;

                // Move to next step or finish
                if (currentStep == steps.Count - 1)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowStep(currentStep + 1);
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error in NextButton_Click: {ex.Message}");
                LoadingOverlay.Visibility = Visibility.Collapsed;
                NextButton.IsEnabled = true;
                BackButton.IsEnabled = currentStep > 0;
                System.Windows.MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentStep > 0)
            {
                ShowStep(currentStep - 1);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region Step Validators

        private async Task<bool> ValidateStep1()
        {
            var step1 = steps[0].StepControl as Step1_VRChatCredentials;
            if (step1 == null)
                return false;

            string username = step1.GetUsername();
            string password = step1.GetPassword();
            string twoFaKey = step1.GetTwoFAKey();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                step1.ShowError("Username and password are required.");
                return false;
            }

            // Test credentials
            var (success, message) = await SetupWizardHelper.TestVRChatCredentials(username, password, twoFaKey);
            if (!success)
            {
                step1.ShowError(message);
                return false;
            }

            step1.ClearError();
            return true;
        }

        private async Task<bool> ValidateStep2()
        {
            var step2 = steps[1].StepControl as Step2_GistConfiguration;
            if (step2 == null)
                return false;

            // If skipped, validation passes
            if (step2.IsSkipped())
            {
                return true;
            }

            bool useSharedGist = step2.UseSharedGist();
            if (useSharedGist)
            {
                string gistId = step2.GetSharedGistId();
                string? pat = step2.GetGitHubPAT();

                if (string.IsNullOrWhiteSpace(gistId))
                {
                    step2.ShowError("Gist ID is required.");
                    return false;
                }

                // Test Gist access
                var (success, message) = await SetupWizardHelper.TestGistUrl(gistId, pat);
                if (!success)
                {
                    step2.ShowError(message);
                    return false;
                }

                step2.ClearError();
                step2.SaveSettings();
                return true;
            }
            else
            {
                // Manual URLs
                string avatarUrl = step2.GetAvatarGistUrl();
                string groupUrl = step2.GetGroupGistUrl();

                // Both are optional if neither is provided
                if (string.IsNullOrWhiteSpace(avatarUrl) && string.IsNullOrWhiteSpace(groupUrl))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(avatarUrl))
                {
                    var (success, message) = await SetupWizardHelper.TestGistUrl(avatarUrl);
                    if (!success)
                    {
                        step2.ShowError($"Avatar Gist URL invalid: {message}");
                        return false;
                    }
                }

                if (!string.IsNullOrWhiteSpace(groupUrl))
                {
                    var (success, message) = await SetupWizardHelper.TestGistUrl(groupUrl);
                    if (!success)
                    {
                        step2.ShowError($"Group Gist URL invalid: {message}");
                        return false;
                    }
                }

                step2.ClearError();
                step2.SaveSettings();
                return true;
            }
        }

        private Task<bool> ValidateStep3()
        {
            // Step 3 has no validation, just checkbox selections
            var step3 = steps[2].StepControl as Step3_BehaviorFlags;
            if (step3 != null)
            {
                step3.SaveSettings();
            }
            return Task.FromResult(true);
        }

        private async Task<bool> ValidateStep4()
        {
            var step4 = steps[3].StepControl as Step4_OBSIntegration;
            if (step4 == null)
                return false;

            if (!step4.IsOBSEnabled())
            {
                step4.SaveSettings();
                return true;
            }

            // Test OBS connection if enabled
            string uri = step4.GetOBSWebSocketURI();
            string? password = step4.GetOBSPassword();

            if (string.IsNullOrWhiteSpace(uri))
            {
                step4.ShowError("WebSocket URI is required.");
                return false;
            }

            var (success, message) = await SetupWizardHelper.TestOBSConnection(uri, password);
            if (!success)
            {
                step4.ShowError(message);
                return false;
            }

            step4.ClearError();
            step4.SaveSettings();
            return true;
        }

        private async Task<bool> ValidateStep5()
        {
            var step5 = steps[4].StepControl as Step5_AIEvaluation;
            if (step5 == null)
                return false;

            if (!step5.IsAIEnabled())
            {
                step5.SaveSettings();
                return true;
            }

            // Test OLLAMA connection if enabled
            string endpoint = step5.GetOLLAMAEndpoint();

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                step5.ShowError("OLLAMA endpoint is required.");
                return false;
            }

            var (success, message) = await SetupWizardHelper.TestOllamaEndpoint(endpoint);
            if (!success)
            {
                step5.ShowError(message);
                return false;
            }

            step5.ClearError();
            step5.SaveSettings();
            return true;
        }

        private Task<bool> ValidateStep6()
        {
            var step6 = steps[5].StepControl as Step6_MCPServer;
            if (step6 == null)
                return Task.FromResult(false);

            if (!step6.IsMCPEnabled())
            {
                step6.SaveSettings();
                return Task.FromResult(true);
            }

            // Validate port
            int port = step6.GetMCPPort();
            var (success, message) = SetupWizardHelper.ValidateMCPPort(port);
            if (!success)
            {
                step6.ShowError(message);
                return Task.FromResult(false);
            }

            step6.ClearError();
            step6.SaveSettings();
            return Task.FromResult(true);
        }

        private Task<bool> ValidateStep7()
        {
            var step7 = steps[6].StepControl as Step7_UpgradeMigration;
            if (step7 != null)
            {
                step7.PopulateStatus();
            }
            return Task.FromResult(true);
        }

        #endregion

    }
}
