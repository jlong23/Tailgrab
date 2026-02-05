using NLog;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Tailgrab.Common;
using Tailgrab.Models;
using static Tailgrab.Clients.VRChat.VRChatClient;

namespace Tailgrab.PlayerManagement
{
    public enum ReportReasonCode
    {
        Sexual,
        Hateful,
        Gore
    }

    public class ReportReasonItem
    {
        public string DisplayName { get; set; }
        public string Value { get; set; }

        public ReportReasonItem(string displayName, string value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }

    public partial class ReportInventoryWindow : Window
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private ServiceRegistry _serviceRegistry;

        public List<ReportReasonItem> ReportReasons { get; } = new List<ReportReasonItem>
        {
            new ReportReasonItem("Sexual Content", "sexual"),
            new ReportReasonItem("Hateful Content", "hateful"),
            new ReportReasonItem("Gore and Violence", "gore"),
            new ReportReasonItem("Other", "other")
        };

        public ReportInventoryWindow(ServiceRegistry serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
            InitializeComponent();
            DataContext = this;
        }

        public ReportInventoryWindow(ServiceRegistry serviceRegistry, string userId, string inventoryId )
        {
            _serviceRegistry = serviceRegistry;
            InitializeComponent();
            DataContext = this;
            UserIdTextBox.Text = userId.Trim();
            InventoryIdTextBox.Text = inventoryId.Trim();
        }

        private async void OnInputFieldChanged(object sender, TextChangedEventArgs e)
        {
            string userId = UserIdTextBox.Text.Trim();
            string inventoryId = InventoryIdTextBox.Text.Trim();

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(inventoryId))
            {
                return;
            }

            try
            {
                VRChatInventoryItem? inventoryItem = await _serviceRegistry.GetVRChatAPIClient().GetUserInventoryItem(userId, inventoryId);
                if (inventoryItem != null)
                {
                    if( inventoryItem.ItemTypeLabel.Equals("Emoji", StringComparison.OrdinalIgnoreCase))
                    {
                        CategoryTextBox.Text = "Emoji";
                    }
                    else if (inventoryItem.ItemTypeLabel.Equals("Sticker", StringComparison.OrdinalIgnoreCase))
                    {
                        CategoryTextBox.Text = "Sticker";
                    }
                    else
                    {
                        CategoryTextBox.Text = "Unknown";
                    }

                    if (!string.IsNullOrEmpty(inventoryItem.ImageUrl))
                    {
                        // Load and display the image
                        LoadImage(inventoryItem.ImageUrl);

                        await LoadImageEvaluation(inventoryId, userId, inventoryItem.ImageUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error retrieving inventory item for User ID: {userId}, Inventory ID: {inventoryId}");
            }
            
        }

        private async Task LoadImageEvaluation(string inventoryId, string userId, string imageUrl)
        {
            try
            {
                // Check if evaluation already exists in the database
                TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();
                ImageEvaluation? imageEvaluation = dbContext.ImageEvaluations.Find(inventoryId);

                if (imageEvaluation != null)
                {
                    // Load existing evaluation
                    ReportDescriptionTextBox.Text = System.Text.Encoding.UTF8.GetString(imageEvaluation.Evaluation);
                    logger.Debug($"Loaded existing image evaluation for inventory ID: {inventoryId}");
                }
                else
                {
                    // Call Ollama to classify the image
                    ReportDescriptionTextBox.Text = "Loading AI evaluation...";
                    
                    string? classification = await _serviceRegistry.GetOllamaAPIClient().ClassifyImage(inventoryId, userId, imageUrl);
                    
                    if (!string.IsNullOrEmpty(classification))
                    {
                        ReportDescriptionTextBox.Text = classification;
                        logger.Debug($"Generated new image evaluation for inventory ID: {inventoryId}");
                    }
                    else
                    {
                        ReportDescriptionTextBox.Text = "Failed to generate AI evaluation.";
                        logger.Warn($"Failed to generate AI evaluation for inventory ID: {inventoryId}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error loading image evaluation for inventory ID: {inventoryId}");
                ReportDescriptionTextBox.Text = $"Error: {ex.Message}";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            string userId = UserIdTextBox.Text.Trim();
            string inventoryId = InventoryIdTextBox.Text.Trim();
            string category = string.Empty;
            if( CategoryTextBox.Text.Equals("Emoji", StringComparison.OrdinalIgnoreCase))
            {
                category = "emoji";
            }
            else if (CategoryTextBox.Text.Equals("Sticker", StringComparison.OrdinalIgnoreCase))
            {
                category = "sticker";
            }
            else
            {
                category = "Unknown";
            }
            string reportReason = ReportReasonComboBox.SelectedValue?.ToString() ?? string.Empty;
            string reportDescription = ReportDescriptionTextBox.Text;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(inventoryId))
            {
                System.Windows.MessageBox.Show("Please enter both User ID and Inventory ID.", "Validation Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Call the method that will handle the future web service call
            bool success = await SubmitReport(userId, inventoryId, category, reportReason, reportDescription);

            // Show success message
            if (success)
            {
                System.Windows.MessageBox.Show("Your report has been submitted", "Success",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                DialogResult = true;
                Close();
            } 
            else
            {
                System.Windows.MessageBox.Show("Failed to submit report. Please try again later.", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                DialogResult = false;
            }
        }

        private async Task<bool> SubmitReport(string userId, string inventoryId, string category, string reportReason, string reportDescription)
        {
            ModerationReportPayload rpt = new ModerationReportPayload();
            rpt.Type = category;
            rpt.Category = category;
            rpt.Reason = reportReason;
            rpt.ContentId = inventoryId;
            rpt.Description = reportDescription;

            ModerationReportDetails rptDtls = new ModerationReportDetails();
            rptDtls.HolderId = userId;
            rptDtls.InstanceType = "Group Public";
            rptDtls.InstanceAgeGated = false;
            rpt.Details = new List<ModerationReportDetails>() { rptDtls };

            bool success = await _serviceRegistry.GetVRChatAPIClient().SubmitModerationReportAsync(rpt);
            if (success)
            {
                logger.Info($"Report submitted - UserId: {userId}, InventoryId: {inventoryId}, " +
                           $"Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
            }
            else
            {
                logger.Warn($"Failed to submit report - UserId: {userId}, InventoryId: {inventoryId}, " +
                           $"Category: {category}, ReportReason: {reportReason}, Description: {reportDescription}");
            }
            return success;
        }

        private void LoadImage(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                {
                    InventoryImagePreview.Source = null;
                    return;
                }

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imageUrl);
                bitmap.DecodePixelWidth = 200;
                bitmap.DecodePixelHeight = 200;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                //bitmap.Freeze();

                InventoryImagePreview.Source = bitmap;
                logger.Debug($"Loaded image from URL: {imageUrl}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to load image from URL: {imageUrl}");
                InventoryImagePreview.Source = null;
            }
        }
    }
}

