using System.Diagnostics;
using System.Windows;

namespace Tailgrab;

/// <summary>
/// Interaction logic for UpdateNotificationDialog.xaml
/// </summary>
public partial class UpdateNotificationDialog : Window
{
    private const string ReleasesUrl = "https://github.com/jlong23/Tailgrab/releases";

    public UpdateNotificationDialog(string latestVersion, string latestVersionName, string currentVersion)
    {
        InitializeComponent();

        CurrentVersionText.Text = $"Current version: {currentVersion}";
        LatestVersionText.Text = $"New version available: {latestVersion}";
        LatestVersionName.Text = $"{latestVersionName}";
    }

    private void ViewReleasesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Open the releases page in the default browser
            Process.Start(new ProcessStartInfo
            {
                FileName = ReleasesUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // Close the dialog
        this.Close();
    }

    private void IgnoreButton_Click(object sender, RoutedEventArgs e)
    {
        // Simply close the dialog
        this.Close();
    }
}
