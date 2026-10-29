using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NLog;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Tailgrab.Clients.VRChat;
using Tailgrab.Common;
using Tailgrab.DependencyInjection;
using Tailgrab.PlayerManagement;
using Tailgrab.PlayerManagement.Views;

namespace Tailgrab;

public class App
{

    /// <summary>
    /// The path to the user's profile directory.
    /// </summary>
    internal static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    static Logger logger = LogManager.GetCurrentClassLogger();
    static ServiceRegistry? _serviceRegistry;
    static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Gets the service provider for dependency resolution.
    /// </summary>
    public static ServiceRegistry GetServiceRegistry()
    {
        if (_serviceRegistry == null)
        {
            throw new InvalidOperationException("ServiceRegistry has not been initialized. Ensure BuildDependencies() is called in Main().");
        }
        return _serviceRegistry;
    }


    /// <summary>
    /// Watch the VRChat log directory by default and process logs.
    /// Show the TailgrabPanel UI on the STA thread before continuing to watch files.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Initialize logging and prerequisites (before DI)
            NLog.GlobalDiagnosticsContext.Set("StartTime", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            string configFilePath = Path.Combine(CommonConst.APPLICATION_LOCAL_DATA_PATH, "NLog.config");
            LogManager.Setup().LoadConfigurationFromFile(configFilePath);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Parse command-line arguments
            // -l <FilePath>           : use explicit log folder/file path
            // -clear                  : remove application registry settings and exit
            // -backup                 : create a backup of the database and exit
            // -banlist <input.txt>    : bulk ban users from file and export to Excel
            // -output <output.xlsx>   : Excel output file for banlist export
            string? explicitPath = null;
            bool clearRegistry = false;
            bool backup = false;
            string? banListFilePath = null;
            string? outputExcelPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (string.Equals(a, "-l", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    explicitPath = args[i + 1];
                    i++;
                }
                else if (string.Equals(a, "-clear", StringComparison.OrdinalIgnoreCase))
                {
                    clearRegistry = true;
                }
                else if (string.Equals(a, "-backup", StringComparison.OrdinalIgnoreCase))
                {
                    backup = true;
                }
                else if (string.Equals(a, "-banlist", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    banListFilePath = args[i + 1];
                    i++;
                }
                else if (string.Equals(a, "-output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    outputExcelPath = args[i + 1];
                    i++;
                }
            }

            // Handle the clear registry operation if specified
            if (clearRegistry)
            {
                // Exit application after clearing settings
                RegistryMigrationManager.DeleteTailgrabRegistrySettings();
                return;
            }

            // Build dependency injection container
            _serviceProvider = DiContainer.BuildServiceProvider();
            _serviceRegistry = _serviceProvider.GetRequiredService<ServiceRegistry>();
            _serviceRegistry.StartAllServices().GetAwaiter().GetResult();

            // Initialize OfficeClient with ServiceRegistry reference (breaks circular dependency)
            var officeClient = _serviceProvider.GetRequiredService<Tailgrab.Clients.Office.OfficeClient>();
            officeClient.Initialize(_serviceRegistry);

            // Initialize MCP Server after ServiceRegistry is ready
            var mcpServer = _serviceProvider.GetRequiredService<Tailgrab.MCP.McpServer>();
            mcpServer.Initialize(_serviceRegistry);

            TailgrabMigration migration = new TailgrabMigration(_serviceRegistry);
            migration.UpgradeApplication();

            if (backup)
            {
                CreateDatabaseBackup();

                // Exit application after creating backup
                return;
            }

            // Handle banlist operation if specified
            if (!string.IsNullOrWhiteSpace(banListFilePath) && !string.IsNullOrWhiteSpace(outputExcelPath))
            {
                ProcessBanListAsync(banListFilePath, outputExcelPath, _serviceRegistry).GetAwaiter().GetResult();

                // Exit application after processing banlist
                return;
            }

            // Start the MCP Server on a background thread for local network access
            logger.Info($"Starting MCP Server on port {mcpServer.Port}");
            _ = mcpServer.StartAsync();

            // Check for updates before showing the main window
            _ = Task.Run(async () => await CheckForUpdatesAsync());

            // Start the FileTailer to watch VRChat logs and process them
            _serviceRegistry.GetFileTailer()?.Start(explicitPath);

            BuildAppWindow(_serviceRegistry);

            // When the window closes, allow Main to complete. The watcher task will be abandoned; if desired add cancellation.
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unhandled exception in Main");
            return;
        }
    }


    /// <summary>
    /// Create a timestamped backup of the SQLite database.
    /// Exports all database tables to JSON files for easy recovery.
    /// </summary>
    private static void CreateDatabaseBackup()
    {
        try
        {
            // Get database context
            if (_serviceRegistry == null)
            {
                logger.Error("Service registry not initialized. Cannot create backup.");
                return;
            }

            var context = _serviceRegistry.GetDBContext();

            context.CreateDatabaseBackup();

        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to create database backup");
        }
    }

    /// <summary>
    /// Check GitHub for new releases and notify the user if a newer version is available.
    /// </summary>
    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Tailgrab-Update-Checker");

            var response = await httpClient.GetAsync("https://api.github.com/repos/jlong23/Tailgrab/releases/latest");

            if (!response.IsSuccessStatusCode)
            {
                logger.Info($"Failed to check for updates. Status: {response.StatusCode}");
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagNameElement))
            {
                logger.Info("No tag_name found in GitHub release response");
                return;
            }

            if (!root.TryGetProperty("name", out var releaseNameElement))
            {
                logger.Info("No name found in GitHub release response");
                return;
            }

            string latestVersion = tagNameElement.GetString() ?? "";
            string latestVersionName = releaseNameElement.GetString() ?? "";
            string currentVersion = BuildInfo.GetInformationalVersion();

            // Remove 'v' prefix if present for comparison
            latestVersion = latestVersion.ToLower().TrimStart('v');
            currentVersion = currentVersion.ToLower().TrimStart('v');

            // Parse and compare versions
            if (Version.TryParse(latestVersion, out var latest) && 
                Version.TryParse(currentVersion.Split('+')[0], out var current))
            {
                if (latest > current)
                {
                    logger.Info($"New version available: {latestVersion} (current: {currentVersion})");

                    // Show update notification on the UI thread
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        var dialog = new UpdateNotificationDialog(latestVersion, latestVersionName, currentVersion);
                        dialog.ShowDialog();
                    });
                }
                else
                {
                    logger.Info($"Already on latest version: {currentVersion}");
                }
            }
            else
            {
                logger.Info($"Failed to parse versions. Latest: {latestVersion}, Current: {currentVersion}");
            }
        }
        catch (Exception ex)
        {
            logger.Warn(ex, "Failed to check for updates");
        }
    }

    private static CultureInfo SetUserCulture()
    {
        var defaultCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo userCulture = CultureInfo.CurrentCulture;

        logger.Info($"User's current culture: {userCulture.Name}, Defaulting to: {defaultCulture.Name}");

        // Whitelist of supported cultures (only en-US is supported currently)
        var supportedCultures = new[] { "en-US" };

        if (userCulture == null || string.IsNullOrEmpty(userCulture.Name) || userCulture.Name == "Invariant Culture" || userCulture.Name == "und" || !supportedCultures.Contains(userCulture.Name))
        {
            userCulture = defaultCulture;
        }

        CultureInfo.DefaultThreadCurrentCulture = userCulture;
        CultureInfo.DefaultThreadCurrentUICulture = userCulture;

        return userCulture;
    }

    private static void BuildAppWindow(ServiceRegistry serviceRegistryInstance)
    {
        CultureInfo userCulture = SetUserCulture();

        // Start WPF application and show the TailgrabPanel on this STA thread
        var app = new System.Windows.Application();

        // Load app-wide resource dictionaries
        app.Resources.MergedDictionaries.Add(
            (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
                new Uri("/tailgrab;component/src/Resources/AlertClassIcons.xaml", UriKind.Relative)));
        app.Resources.MergedDictionaries.Add(
            (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
                new Uri($"/tailgrab;component/src/Resources/localization.{userCulture.Name}.xaml", UriKind.Relative)));

        // Dark theme resources
        var darkWindow = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));
        var darkControl = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48));
        var lightText = new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230));
        var accent = new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 44, 55));

        var highlightDark = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 109));
        var highlightDarkText = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 129));


        // Override common system brushes
        app.Resources[System.Windows.SystemColors.WindowBrushKey] = darkWindow;
        app.Resources[System.Windows.SystemColors.ControlBrushKey] = darkControl;
        app.Resources[System.Windows.SystemColors.HighlightBrushKey] = accent;
        app.Resources[System.Windows.SystemColors.ControlTextBrushKey] = lightText;
        app.Resources[System.Windows.SystemColors.WindowTextBrushKey] = lightText;
        app.Resources[System.Windows.SystemColors.HighlightTextBrushKey] = lightText;

        // Basic control styles
        var textBoxStyle = new Style(typeof(System.Windows.Controls.TextBox));
        textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        textBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));
        app.Resources[typeof(System.Windows.Controls.TextBox)] = textBoxStyle;

        var listViewStyle = new Style(typeof(System.Windows.Controls.ListView));
        listViewStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        listViewStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Controls.ListView)] = listViewStyle;

        // ListViewItem style - darker row highlight and mouse-over
        var listViewItemStyle = new Style(typeof(System.Windows.Controls.ListViewItem));
        listViewItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        listViewItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        listViewItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, System.Windows.Media.Brushes.Transparent));

        var selectedTrigger = new Trigger { Property = System.Windows.Controls.ListViewItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        selectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        listViewItemStyle.Triggers.Add(selectedTrigger);

        var mouseOverTrigger = new Trigger { Property = System.Windows.Controls.ListViewItem.IsMouseOverProperty, Value = true };
        mouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        mouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        listViewItemStyle.Triggers.Add(mouseOverTrigger);

        app.Resources[typeof(System.Windows.Controls.ListViewItem)] = listViewItemStyle;

        // GridViewColumnHeader style for list headers
        var headerStyle = new Style(typeof(System.Windows.Controls.GridViewColumnHeader));
        headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));

        var headerMouseOverTrigger = new Trigger { Property = System.Windows.Controls.GridViewColumnHeader.IsMouseOverProperty, Value = true };
        headerMouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        headerMouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        headerStyle.Triggers.Add(headerMouseOverTrigger);

        var headerPressedTrigger = new Trigger { Property = System.Windows.Controls.GridViewColumnHeader.IsPressedProperty, Value = true };
        headerPressedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        headerPressedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        headerStyle.Triggers.Add(headerPressedTrigger);

        app.Resources[typeof(System.Windows.Controls.GridViewColumnHeader)] = headerStyle;

        // DataGrid dark theme styles for Config -> Avatars DB
        var dataGridStyle = new Style(typeof(System.Windows.Controls.DataGrid));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.DataGrid.RowBackgroundProperty, darkWindow));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.DataGrid.AlternatingRowBackgroundProperty, darkControl));
        dataGridStyle.Setters.Add(new Setter(System.Windows.Controls.DataGrid.GridLinesVisibilityProperty, System.Windows.Controls.DataGridGridLinesVisibility.None));
        app.Resources[typeof(System.Windows.Controls.DataGrid)] = dataGridStyle;

        var dgHeaderStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        dgHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        dgHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        dgHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));
        app.Resources[typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)] = dgHeaderStyle;

        var dgCellStyle = new Style(typeof(System.Windows.Controls.DataGridCell));
        dgCellStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        dgCellStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        dgCellStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, System.Windows.Media.Brushes.Transparent));
        var cellSelectedTrigger = new Trigger { Property = System.Windows.Controls.DataGridCell.IsSelectedProperty, Value = true };
        cellSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        cellSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        dgCellStyle.Triggers.Add(cellSelectedTrigger);
        app.Resources[typeof(System.Windows.Controls.DataGridCell)] = dgCellStyle;

        var dgRowStyle = new Style(typeof(System.Windows.Controls.DataGridRow));
        dgRowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        dgRowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        var rowSelectedTrigger = new Trigger { Property = System.Windows.Controls.DataGridRow.IsSelectedProperty, Value = true };
        rowSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        rowSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        dgRowStyle.Triggers.Add(rowSelectedTrigger);
        app.Resources[typeof(System.Windows.Controls.DataGridRow)] = dgRowStyle;

        // ScrollBar and Thumb styles
        var scrollBarStyle = new Style(typeof(System.Windows.Controls.Primitives.ScrollBar));
        scrollBarStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        app.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = scrollBarStyle;

        var thumbStyle = new Style(typeof(System.Windows.Controls.Primitives.Thumb));
        thumbStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        app.Resources[typeof(System.Windows.Controls.Primitives.Thumb)] = thumbStyle;

        var buttonStyle = new Style(typeof(System.Windows.Controls.Button));
        buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        buttonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Controls.Button)] = buttonStyle;

        var tabControlStyle = new Style(typeof(System.Windows.Controls.TabControl));
        tabControlStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        tabControlStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        tabControlStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, darkControl));
        app.Resources[typeof(System.Windows.Controls.TabControl)] = tabControlStyle;

        // TabItem style - for individual tab headers
        var tabItemStyle = new Style(typeof(System.Windows.Controls.TabItem));
        tabItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        tabItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        tabItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, darkControl));
        tabItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(8, 4, 8, 4)));

        // Disabled tab - slightly lighter background
        var tabDisabledTrigger = new Trigger { Property = System.Windows.Controls.TabItem.IsEnabledProperty, Value = false };
        tabDisabledTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(55, 55, 58))));
        tabDisabledTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(130, 130, 130))));
        tabItemStyle.Triggers.Add(tabDisabledTrigger);

        // Selected tab - highlighted dark theme
        var tabSelectedTrigger = new Trigger { Property = System.Windows.Controls.TabItem.IsSelectedProperty, Value = true };
        tabSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightDark));
        tabSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightDarkText));
        tabItemStyle.Triggers.Add(tabSelectedTrigger);

        // Mouse over tab - lighter highlight (only for enabled tabs)
        var tabMouseOverTrigger = new Trigger { Property = System.Windows.Controls.TabItem.IsMouseOverProperty, Value = true };
        tabMouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 63))));
        tabMouseOverTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        tabItemStyle.Triggers.Add(tabMouseOverTrigger);

        app.Resources[typeof(System.Windows.Controls.TabItem)] = tabItemStyle;

        var groupBoxStyle = new Style(typeof(System.Windows.Controls.GroupBox));
        groupBoxStyle.Setters.Add(new Setter(System.Windows.Window.BackgroundProperty, darkWindow));
        groupBoxStyle.Setters.Add(new Setter(System.Windows.Window.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Controls.GroupBox)] = groupBoxStyle;

        // ComboBox requires a custom template to properly theme all internal parts
        var comboBoxTemplate = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.ComboBox));
        var templateContent = @"
            <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                           xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                           TargetType='{x:Type ComboBox}'>
                <Grid>
                    <ToggleButton x:Name='ToggleButton' 
                                  Background='#FF2D2D30'
                                  BorderBrush='#FF007ACC'
                                  Foreground='#FFE6E6E6'
                                  IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'
                                  ClickMode='Press'>
                        <ToggleButton.Template>
                            <ControlTemplate TargetType='ToggleButton'>
                                <Border Background='{TemplateBinding Background}' 
                                        BorderBrush='{TemplateBinding BorderBrush}' 
                                        BorderThickness='1'>
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition />
                                            <ColumnDefinition Width='20' />
                                        </Grid.ColumnDefinitions>
                                        <Path Grid.Column='1' HorizontalAlignment='Center' VerticalAlignment='Center'
                                              Data='M 0 0 L 4 4 L 8 0 Z' Fill='#FFE6E6E6' />
                                    </Grid>
                                </Border>
                            </ControlTemplate>
                        </ToggleButton.Template>
                    </ToggleButton>
                    <ContentPresenter x:Name='ContentSite' 
                                      Content='{TemplateBinding SelectionBoxItem}'
                                      ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                                      ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'
                                      Margin='6,3,23,3'
                                      VerticalAlignment='Center'
                                      HorizontalAlignment='Left'
                                      IsHitTestVisible='False' />
                    <Popup x:Name='Popup' 
                           Placement='Bottom'
                           IsOpen='{TemplateBinding IsDropDownOpen}'
                           AllowsTransparency='True' 
                           Focusable='False'
                           PopupAnimation='Slide'>
                        <Grid MinWidth='{TemplateBinding ActualWidth}' MaxHeight='{TemplateBinding MaxDropDownHeight}'>
                            <Border Background='#FF2D2D30' BorderBrush='#FF007ACC' BorderThickness='1'>
                                <ScrollViewer>
                                    <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained' />
                                </ScrollViewer>
                            </Border>
                        </Grid>
                    </Popup>
                </Grid>
            </ControlTemplate>";

        var comboBoxStyle = new Style(typeof(System.Windows.Controls.ComboBox));
        comboBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        comboBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        comboBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));
        comboBoxStyle.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty,
            (System.Windows.Controls.ControlTemplate)System.Windows.Markup.XamlReader.Parse(templateContent)));
        app.Resources[typeof(System.Windows.Controls.ComboBox)] = comboBoxStyle;

        // Ensure ComboBox items and selected text use the dark theme when the control is not focused.
        var comboBoxItemStyle = new Style(typeof(System.Windows.Controls.ComboBoxItem));
        comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, System.Windows.Media.Brushes.Transparent));
        comboBoxItemStyle.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(4)));

        var comboSelectedTrigger = new Trigger { Property = System.Windows.Controls.ComboBoxItem.IsSelectedProperty, Value = true };
        comboSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 63))));
        comboSelectedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        comboBoxItemStyle.Triggers.Add(comboSelectedTrigger);

        var comboHighlightedTrigger = new Trigger { Property = System.Windows.Controls.ComboBoxItem.IsHighlightedProperty, Value = true };
        comboHighlightedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 53))));
        comboHighlightedTrigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        comboBoxItemStyle.Triggers.Add(comboHighlightedTrigger);

        app.Resources[typeof(System.Windows.Controls.ComboBoxItem)] = comboBoxItemStyle;

        // Style for the ComboBox toggle button
        var toggleButtonStyle = new Style(typeof(System.Windows.Controls.Primitives.ToggleButton));
        toggleButtonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkControl));
        toggleButtonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, lightText));
        toggleButtonStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, accent));
        app.Resources[typeof(System.Windows.Controls.Primitives.ToggleButton)] = toggleButtonStyle;

        // Style for ComboBox Popup background
        var popupStyle = new Style(typeof(System.Windows.Controls.Primitives.Popup));
        popupStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, darkWindow));
        app.Resources[typeof(System.Windows.Controls.Primitives.Popup)] = popupStyle;

        var windowStyle = new Style(typeof(System.Windows.Window));
        windowStyle.Setters.Add(new Setter(System.Windows.Window.BackgroundProperty, darkWindow));
        windowStyle.Setters.Add(new Setter(System.Windows.Window.ForegroundProperty, lightText));
        app.Resources[typeof(System.Windows.Window)] = windowStyle;

        var panel = new TailgrabPanel(serviceRegistryInstance);

        panel.PlayerSelected += serviceRegistryInstance.GetPlayerManager().TailgrabPanel_PlayerSelected;

        try
        {
            string LayoutRegistryPath = "Software\\DeviousFox\\Tailgrab\\Layout";
            using (var key = Registry.CurrentUser.OpenSubKey(LayoutRegistryPath))
            {
                if (key != null)
                {
                    var width = key.GetValue("WindowWidth");
                    var height = key.GetValue("WindowHeight");

                    if (width != null && height != null)
                    {
                        panel.Width = Convert.ToDouble(width);
                        panel.Height = Convert.ToDouble(height);
                        logger.Debug($"Loaded window size: {panel.Width}x{panel.Height}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to load window size from registry.");
        }

        app.Run(panel);
    }

    private static async Task ProcessBanListAsync(string inputFilePath, string outputExcelPath, ServiceRegistry serviceRegistry)
    {
        var processedUsers = new List<VRChatClient.VRChatUserProfileEntry>();

        int banCount = 0;
        try
        {
            logger.Info($"Starting ban list processing: input={inputFilePath}, output={outputExcelPath}");

            // Load groups from database (mirrors LoadBanManagementGroupsAsync pattern)
            var banMgmtGroupList = new List<GroupBanViewModel>();
            var groups = await Task.Run(() => serviceRegistry.GetDBContext().GroupManagements.ToList()).ConfigureAwait(false);
            foreach (var group in groups)
            {
                banMgmtGroupList.Add(new GroupBanViewModel
                {
                    GroupId = group.GroupId,
                    GroupName = group.GroupName,
                    Status = "Ready",
                    CanBan = true,
                    CanUnban = false
                });
            }
            logger.Info($"Loaded {banMgmtGroupList.Count} groups for banning");

            var vrchatClient = serviceRegistry.GetVRChatAPIClient();

            // Read and process each URL from file
            if (!File.Exists(inputFilePath))
            {
                logger.Error($"Input file not found: {inputFilePath}");
                return;
            }

            using (var reader = new StreamReader(inputFilePath))
            {
                string? line;
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Extract user ID from URL
                    string userId = ExtractUserIdFromUrl(line.Trim());
                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        logger.Warn($"Could not extract user ID from URL: {line}");
                        continue;
                    }

                    logger.Info($"Processing user: {userId}");

                    // Fetch cached profile
                    VRChatClient.VRChatUserProfileEntry? profile = null;
                    int retries = 0;
                    while (true)
                    {
                        try
                        {
                            profile = await vrchatClient.GetCachedUserProfileAsync(userId).ConfigureAwait(false);
                            if (profile == null)
                            {
                                logger.Warn($"Could not fetch profile for user: {userId}");
                                await Task.Delay(60000).ConfigureAwait(false); // Wait a second before retrying
                                continue;
                            }
                            break;
                        }
                        catch (Exception ex)
                        {
                            logger.Warn(ex, $"Error fetching profile for user: {userId}. Retrying in 60 seconds...");
                            await Task.Delay(60000).ConfigureAwait(false); // Wait a second before retrying\
                            retries++;
                            if (retries >= 3)
                            {
                                logger.Error($"Failed to fetch profile for user: {userId} after {retries} attempts. Skipping...");
                                break;
                            }
                        }
                    }

                    if(profile == null)
                    {
                        logger.Warn($"Profile is null for user: {userId}. Skipping...");
                        continue;
                    }

                    processedUsers.Add(profile);

                    // Ban user from all groups
                    foreach (var group in banMgmtGroupList)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(group.GroupId))
                                continue;

                            bool banSuccess = await vrchatClient.BanUserFromGroup(group.GroupId, userId).ConfigureAwait(false);
                            if (banSuccess)
                            {
                                logger.Info($"Banned user {userId} from group {group.GroupId}");
                            }
                            else
                            {
                                logger.Warn($"Failed to ban user {userId} from group {group.GroupId}");
                            }

                            banCount++;
                            if ( banCount % 10 == 0)
                            {
                                logger.Info($"Processed {banCount} bans so far, pausing 15 seconds to avoid rate limits...");
                                await Task.Delay(15000).ConfigureAwait(false);
                            }

                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"Error banning user {userId} from group {group.GroupId}");
                        }
                    }
                    logger.Info($"Completed processing for {userId}, pausing 1 seconds...");
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error during ban list processing");
        }

        // Export all processed users to Excel
        if (processedUsers.Count > 0)
        {
            await ExportUsersToExcelAsync(processedUsers, outputExcelPath).ConfigureAwait(false);
            logger.Info($"Successfully exported {processedUsers.Count} users to {outputExcelPath}");
        }
        else
        {
            logger.Info("No users were successfully processed");
        }

    }

    private static string ExtractUserIdFromUrl(string url)
    {
        // Expected format: https://vrchat.com/home/user/usr_xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        try
        {
            int userIndex = url.LastIndexOf("/user/", StringComparison.OrdinalIgnoreCase);
            if (userIndex >= 0)
            {
                string userId = url.Substring(userIndex + 6).Split('?')[0].Trim();
                if (!string.IsNullOrWhiteSpace(userId) && userId.StartsWith("usr_"))
                {
                    return userId;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warn($"Error parsing URL: {url}, {ex.Message}");
        }
        return string.Empty;
    }

    private static async Task ExportUsersToExcelAsync(List<VRChatClient.VRChatUserProfileEntry> users, string outputPath)
    {
        await Task.Run(() =>
        {
            try
            {
                using (OfficeOpenXml.ExcelPackage package = new OfficeOpenXml.ExcelPackage())
                {
                    OfficeOpenXml.ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("VRChat Users");

                    // Header row
                    string[] headers = { "UserId", "DisplayName", "JoinDate", "Pronouns", "StatusDescription", "Bio", "BioLinks", "DiscordLinks" };
                    for (int c = 0; c < headers.Length; c++)
                        worksheet.Cells[1, c + 1].Value = headers[c];

                    // Bold the header
                    worksheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;

                    // Data rows
                    int row = 2;
                    foreach (var user in users)
                    {
                        worksheet.Cells[row, 1].Value = user.UserId;
                        worksheet.Cells[row, 2].Value = user.DisplayName;
                        worksheet.Cells[row, 3].Value = user.JoinDate.ToString("yyyy-MM-dd");
                        worksheet.Cells[row, 4].Value = user.Pronouns;
                        worksheet.Cells[row, 5].Value = user.StatusDescription;
                        worksheet.Cells[row, 6].Value = user.Bio;
                        worksheet.Cells[row, 7].Value = string.Join("; ", user.BioLinks);
                        worksheet.Cells[row, 8].Value = string.Join("; ", user.DiscordLinks);
                        row++;
                    }

                    // Auto-fit column widths
                    worksheet.Cells.AutoFitColumns();

                    // Save to file
                    FileInfo fileInfo = new FileInfo(outputPath);
                    package.SaveAs(fileInfo);
                }
                logger.Info($"Excel file created successfully: {outputPath}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error creating Excel file: {outputPath}");
            }
        }).ConfigureAwait(false);
    }
}

public class BuildInfo
{
    public static string GetAssemblyVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "0.0.0.0";
    }

    public static string GetInformationalVersion()
    {
        return Assembly.GetExecutingAssembly()
                       .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                       ?.InformationalVersion
                       ?? GetAssemblyVersion();
    }
}
