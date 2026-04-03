using Microsoft.Win32;
using NLog;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace Tailgrab.Common
{
    public class WindowLayoutManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const string LayoutRegistryPath = "Software\\DeviousFox\\Tailgrab\\Layout";

        // Default values from XAML
        public const double DefaultWindowWidth = 1080;
        public const double DefaultWindowHeight = 750;

        // Active Players Tab Column Widths
        public const double DefaultActiveDisplayNameWidth = 180;
        public const double DefaultActiveAgeWidth = 60;
        public const double DefaultActiveAvatarNameWidth = 180;
        public const double DefaultActiveInstanceStartWidth = 160;
        public const double DefaultActiveAlertMessagesWidth = 330;
        public const double DefaultActiveCopyWidth = 60;
        public const double DefaultActiveReportWidth = 60;

        // Past Players Tab Column Widths
        public const double DefaultPastDisplayNameWidth = 180;
        public const double DefaultPastAgeWidth = 60;
        public const double DefaultPastAvatarNameWidth = 180;
        public const double DefaultPastInstanceEndWidth = 160;
        public const double DefaultPastAlertMessagesWidth = 330;
        public const double DefaultPastCopyWidth = 60;
        public const double DefaultPastReportWidth = 60;

        // Known Avatars Column Widths
        public const double DefaultAvatarAlertWidth = 80;
        public const double DefaultAvatarNameWidth = 150;
        public const double DefaultAvatarIdWidth = 250;
        public const double DefaultAvatarUserNameWidth = 150;
        public const double DefaultAvatarUpdatedWidth = 150;
        public const double DefaultAvatarBrowserWidth = 80;

        // Known Groups Column Widths
        public const double DefaultGroupAlertWidth = 120;
        public const double DefaultGroupNameWidth = 200;
        public const double DefaultGroupIdWidth = 300;
        public const double DefaultGroupUpdatedWidth = 200;
        public const double DefaultGroupBrowserWidth = 80;

        // Known Users Column Widths
        public const double DefaultUserDisplayNameWidth = 200;
        public const double DefaultUserIdWidth = 300;
        public const double DefaultUserElapsedWidth = 120;
        public const double DefaultUserUpdatedWidth = 200;
        public const double DefaultUserBrowserWidth = 80;

        // Open Logs Column Widths
        public const double DefaultLogFileNameWidth = 400;
        public const double DefaultLogOpenedWidth = 180;
        public const double DefaultLogLastLineWidth = 180;
        public const double DefaultLogLinesProcessedWidth = 120;
        public const double DefaultLogActionWidth = 80;

        // GridSplitter positions (stored as GridLength values)
        public const double DefaultActiveRowSplitterHeight = 100;
        public const double DefaultPastRowSplitterHeight = 100;

        // GridSplitter positions (stored as GridLength values)
        public const double DefaultActiveColSplitterWidth = -1;
        public const double DefaultPastColSplitterWidth = -1;

        #region Save Methods

        public static void SaveWindowSize(Window window)
        {
            try
            {
                if (window.WindowState == WindowState.Normal)
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(LayoutRegistryPath))
                    {
                        key.SetValue("WindowWidth", window.Width);
                        key.SetValue("WindowHeight", window.Height);
                    }
                    logger.Debug($"Saved window size: {window.Width}x{window.Height}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save window size to registry.");
            }
        }

        public static void SaveWindowPosition(Window window)
        {
            try
            {
                if (window.WindowState == WindowState.Normal)
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(LayoutRegistryPath))
                    {
                        key.SetValue("WindowLeft", window.Left);
                        key.SetValue("WindowTop", window.Top);
                    }
                    logger.Debug($"Saved window position: Left={window.Left}, Top={window.Top}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save window position to registry.");
            }
        }

        public static void SaveColumnWidth(string columnName, double width)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(LayoutRegistryPath))
                {
                    key.SetValue($"Column_{columnName}", width);
                }
                logger.Debug($"Saved column width for {columnName}: {width}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to save column width for {columnName}.");
            }
        }

        public static void SaveSplitterPosition(string splitterName, double height)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(LayoutRegistryPath))
                {
                    key.SetValue($"Splitter_{splitterName}", height);
                }
                logger.Debug($"Saved splitter height for {splitterName}: {height}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to save splitter height for {splitterName}.");
            }
        }
        #endregion

        #region Load Methods
        public static void LoadWindowSize(Window window)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(LayoutRegistryPath))
                {
                    if (key != null)
                    {
                        var width = key.GetValue("WindowWidth");
                        var height = key.GetValue("WindowHeight");

                        if (width != null && height != null)
                        {
                            window.Width = Convert.ToDouble(width);
                            window.Height = Convert.ToDouble(height);
                            logger.Debug($"Loaded window size: {window.Width}x{window.Height}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load window size from registry.");
            }
        }

        public static void LoadWindowPosition(Window window)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(LayoutRegistryPath))
                {
                    if (key != null)
                    {
                        var left = key.GetValue("WindowLeft");
                        var top = key.GetValue("WindowTop");

                        if (left != null && top != null)
                        {
                            double leftPos = Convert.ToDouble(left);
                            double topPos = Convert.ToDouble(top);

                            // Validate that the position is within visible screen bounds
                            if (IsPositionValid(leftPos, topPos, window.Width, window.Height))
                            {
                                window.Left = leftPos;
                                window.Top = topPos;
                                logger.Debug($"Loaded window position: Left={window.Left}, Top={window.Top}");
                            }
                            else
                            {
                                logger.Warn("Saved window position is outside visible screen bounds. Using default position.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load window position from registry.");
            }
        }

        public static double LoadColumnWidth(string columnName, double defaultWidth)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(LayoutRegistryPath))
                {
                    if (key != null)
                    {
                        var value = key.GetValue($"Column_{columnName}");
                        if (value != null)
                        {
                            double width = Convert.ToDouble(value);
                            logger.Debug($"Loaded column width for {columnName}: {width}");
                            return width;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to load column width for {columnName}.");
            }
            return defaultWidth;
        }

        public static double LoadSplitterHeight(string splitterName, double defaultHeight)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(LayoutRegistryPath))
                {
                    if (key != null)
                    {
                        var value = key.GetValue($"Splitter_{splitterName}");
                        if (value != null)
                        {
                            double height = Convert.ToDouble(value);
                            logger.Debug($"Loaded splitter height for {splitterName}: {height}");
                            return height;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to load splitter height for {splitterName}.");
            }
            return defaultHeight;
        }

        #endregion

        #region Reset Methods

        public static void ResetLayoutSettings()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(LayoutRegistryPath, writable: true))
                {
                    if (key != null)
                    {
                        // Delete all layout values
                        foreach (var valueName in key.GetValueNames())
                        {
                            key.DeleteValue(valueName);
                        }
                        logger.Info("Reset all layout settings to defaults.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to reset layout settings.");
                throw;
            }
        }

        #endregion

        #region Helper Methods

        private static bool IsPositionValid(double left, double top, double width, double height)
        {
            // Check if at least part of the window is visible on any screen
            var windowRect = new Rect(left, top, width, height);

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var screenRect = new Rect(
                    screen.WorkingArea.Left,
                    screen.WorkingArea.Top,
                    screen.WorkingArea.Width,
                    screen.WorkingArea.Height);

                if (windowRect.IntersectsWith(screenRect))
                {
                    return true;
                }
            }

            return false;
        }

        public static void ApplyLayoutToGridView(GridView gridView, Dictionary<string, double> columnWidths)
        {
            for (int i = 0; i < gridView.Columns.Count; i++)
            {
                var column = gridView.Columns[i];
                var header = column.Header as GridViewColumnHeader;
                if (header != null)
                {
                    string columnName = header.Tag as string ?? header.Content?.ToString() ?? $"Column{i}";
                    if (columnWidths.TryGetValue(columnName, out double width))
                    {
                        column.Width = width;
                    }
                }
            }
        }

        #endregion
    }
}
