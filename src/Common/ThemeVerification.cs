using NLog;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WPFApplication = System.Windows.Application;
using WPFTextBox = System.Windows.Controls.TextBox;
using WPFListView = System.Windows.Controls.ListView;
using WPFListViewItem = System.Windows.Controls.ListViewItem;
using WPFDataGrid = System.Windows.Controls.DataGrid;
using WPFDataGridCell = System.Windows.Controls.DataGridCell;
using WPFDataGridRow = System.Windows.Controls.DataGridRow;
using WPFScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WPFButton = System.Windows.Controls.Button;
using WPFToggleButton = System.Windows.Controls.Primitives.ToggleButton;
using WPFTabControl = System.Windows.Controls.TabControl;
using WPFTabItem = System.Windows.Controls.TabItem;
using WPFGroupBox = System.Windows.Controls.GroupBox;
using WPFComboBox = System.Windows.Controls.ComboBox;
using WPFComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WPFCheckBox = System.Windows.Controls.CheckBox;
using WPFRadioButton = System.Windows.Controls.RadioButton;
using WPFLabel = System.Windows.Controls.Label;
using WPFProgressBar = System.Windows.Controls.ProgressBar;
using WPFSlider = System.Windows.Controls.Slider;
using WPFGridViewColumnHeader = System.Windows.Controls.GridViewColumnHeader;
using WPFDataGridColumnHeader = System.Windows.Controls.Primitives.DataGridColumnHeader;
using WPFPopup = System.Windows.Controls.Primitives.Popup;
using WPFThumb = System.Windows.Controls.Primitives.Thumb;

namespace Tailgrab.Common;

/// <summary>
/// Verifies that all expected control types have been themed at application startup.
/// Logs warnings for controls that lack theme definitions to catch future gaps.
/// </summary>
public class ThemeVerification
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Defines the set of control types that must be themed for consistent application appearance.
    /// </summary>
    private static readonly Type[] RequiredThemedControlTypes =
    {
        typeof(WPFTextBox),
        typeof(WPFListView),
        typeof(WPFListViewItem),
        typeof(WPFGridViewColumnHeader),
        typeof(WPFDataGrid),
        typeof(WPFDataGridColumnHeader),
        typeof(WPFDataGridCell),
        typeof(WPFDataGridRow),
        typeof(WPFScrollBar),
        typeof(WPFThumb),
        typeof(WPFButton),
        typeof(WPFToggleButton),
        typeof(WPFTabControl),
        typeof(WPFTabItem),
        typeof(WPFGroupBox),
        typeof(WPFComboBox),
        typeof(WPFComboBoxItem),
        typeof(WPFCheckBox),
        typeof(WPFRadioButton),
        typeof(WPFLabel),
        typeof(WPFProgressBar),
        typeof(WPFSlider),
        typeof(Border)
    };

    /// <summary>
    /// Verifies that all required control types have theme definitions in the application resources.
    /// Call this at application startup to ensure theme consistency.
    /// </summary>
    /// <param name="app">The WPF Application instance.</param>
    /// <returns>The count of missing theme definitions found (0 means all controls are themed).</returns>
    public static int VerifyThemeCompleteness(WPFApplication app)
    {
        if (app?.Resources == null)
        {
            Logger.Warn("ThemeVerification: Application or application resources is null. Skipping verification.");
            return -1;
        }

        int missingCount = 0;
        var missingControls = new List<string>();

        foreach (var controlType in RequiredThemedControlTypes)
        {
            if (!app.Resources.Contains(controlType))
            {
                missingCount++;
                missingControls.Add(controlType.Name);
                Logger.Warn($"Theme verification: Missing style definition for {controlType.FullName}");
            }
        }

        if (missingCount > 0)
        {
            Logger.Warn($"Theme verification complete: {missingCount} control types lack theme definitions. " +
                        $"Missing: {string.Join(", ", missingControls)}");
        }
        else
        {
            Logger.Info("Theme verification complete: All required control types are properly themed.");
        }

        return missingCount;
    }

    /// <summary>
    /// Verifies that key theme resource brushes are defined.
    /// Returns a report of which theme resources are available.
    /// </summary>
    /// <param name="app">The WPF Application instance.</param>
    /// <returns>A formatted report of theme resource status.</returns>
    public static string GenerateThemeResourceReport(WPFApplication app)
    {
        if (app?.Resources == null)
        {
            return "Application or resources is null.";
        }

        var requiredBrushes = new[]
        {
            "Brush.Window.Background",
            "Brush.Control.Background",
            "Brush.Control.Foreground",
            "Brush.Border.Regular",
            "Brush.Highlight.Background",
            "Brush.Highlight.Foreground",
            "Brush.Control.Disabled.Background",
            "Brush.Control.Disabled.Foreground",
            "Brush.Control.MouseOver.Background",
            "Brush.Control.Pressed.Background",
            "Brush.Border.Transparent"
        };

        var report = new System.Text.StringBuilder();
        report.AppendLine("=== Theme Resource Verification Report ===");
        report.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
        report.AppendLine();

        int foundCount = 0;
        var missingBrushes = new List<string>();

        foreach (var brushKey in requiredBrushes)
        {
            if (app.Resources.Contains(brushKey))
            {
                foundCount++;
                report.AppendLine($"✓ {brushKey}");
            }
            else
            {
                missingBrushes.Add(brushKey);
                report.AppendLine($"✗ {brushKey} (MISSING)");
            }
        }

        report.AppendLine();
        report.AppendLine($"Summary: {foundCount}/{requiredBrushes.Length} theme brushes found.");

        if (missingBrushes.Count > 0)
        {
            report.AppendLine($"Missing brushes: {string.Join(", ", missingBrushes)}");
        }

        return report.ToString();
    }
}
