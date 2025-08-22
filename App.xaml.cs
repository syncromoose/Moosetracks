using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Controls; // ToolTipService

namespace MooseTracks
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Block tooltips globally when disabled (runtime enforcement)
            EventManager.RegisterClassHandler(
                typeof(FrameworkElement),
                ToolTipService.ToolTipOpeningEvent,
                new ToolTipEventHandler(PreventToolTipOpeningWhenDisabled),
                handledEventsToo: true);

            // Load theme + tooltip enablement before any window is shown
            LoadTheme();

            MainWindow = new MainWindow();
            MainWindow.Show();

            // Ensure the IsEnabled flag flows to all already-open windows
            ApplyGlobalTooltipEnablement(GetTooltipsEnabledFromResources());
        }

        // Cancels any tooltip if TooltipsEnabled == false
        private static void PreventToolTipOpeningWhenDisabled(object sender, ToolTipEventArgs e)
        {
            if (!GetTooltipsEnabledFromResources())
                e.Handled = true;
        }

        private static bool GetTooltipsEnabledFromResources()
        {
            if (Current?.Resources.Contains("TooltipsEnabled") == true &&
                Current.Resources["TooltipsEnabled"] is bool b)
                return b;
            return true; // default ON
        }

        // Sets resource + pushes attached prop to all open windows (inheritance)
        public static void ApplyGlobalTooltipEnablement(bool enabled)
        {
            if (Current is null) return;

            Current.Resources["TooltipsEnabled"] = enabled;

            foreach (Window w in Current.Windows)
                ToolTipService.SetIsEnabled(w, enabled);
        }

        private void LoadTheme()
        {
            try
            {
                string settingsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
                string configFile = Path.Combine(settingsFolder, "settings.cfg");

                string themeName = "default";
                bool tooltipsEnabled = true; // default ON

                if (File.Exists(configFile))
                {
                    foreach (var line in File.ReadLines(configFile))
                    {
                        if (line.StartsWith("Theme=", StringComparison.OrdinalIgnoreCase))
                            themeName = line.Substring("Theme=".Length).Trim();

                        if (line.StartsWith("Tooltips=", StringComparison.OrdinalIgnoreCase))
                        {
                            var raw = line.Substring("Tooltips=".Length).Trim();
                            tooltipsEnabled = ParseBool(raw, defaultValue: true);
                        }
                    }
                }

                string themeFile = Path.Combine(settingsFolder, $"{themeName}.theme");

                // Defaults
                Color bg = Colors.White;
                Color fg = Colors.Black;
                Color br = Colors.Gray;
                Color progressBar = Color.FromRgb(59, 130, 246); // default
                Color boxes = Color.FromRgb(59, 130, 246); // #3B82F6
                double boxesFillPercent = 10.0;            // 0..100
                double borderThickness = 1.0;              // 0..4

                if (File.Exists(themeFile))
                {
                    foreach (var line in File.ReadLines(themeFile).Where(l => !string.IsNullOrWhiteSpace(l)))
                    {
                        if (line.StartsWith("Background=", StringComparison.OrdinalIgnoreCase))
                        {
                            var rgb = ParseRGB(line.Substring("Background=".Length));
                            if (rgb != null) bg = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                        }
                        else if (line.StartsWith("Foreground=", StringComparison.OrdinalIgnoreCase))
                        {
                            var rgb = ParseRGB(line.Substring("Foreground=".Length));
                            if (rgb != null) fg = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                        }
                        else if (line.StartsWith("BorderBrush=", StringComparison.OrdinalIgnoreCase))
                        {
                            var rgb = ParseRGB(line.Substring("BorderBrush=".Length));
                            if (rgb != null) br = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                        }
                        else if (line.StartsWith("BoxesColor=", StringComparison.OrdinalIgnoreCase))
                        {
                            var rgb = ParseRGB(line.Substring("BoxesColor=".Length));
                            if (rgb != null) boxes = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                        }
                        else if (line.StartsWith("BoxesFill=", StringComparison.OrdinalIgnoreCase))
                        {
                            var s = line.Substring("BoxesFill=".Length).Trim();
                            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                                boxesFillPercent = Math.Max(0, Math.Min(100, pct));
                        }
                        else if (line.StartsWith("BorderThickness=", StringComparison.OrdinalIgnoreCase))
                        {
                            var s = line.Substring("BorderThickness=".Length).Trim();
                            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var t))
                                borderThickness = Math.Max(0, Math.Min(4, Math.Round(t)));
                        }
                        else if (line.StartsWith("ProgressBarColor=", StringComparison.OrdinalIgnoreCase))
                        {
                            var rgb = ParseRGB(line.Substring("ProgressBarColor=".Length));
                            if (rgb != null) progressBar = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                        }
                    }
                }

                // Apply theme resources
                ReplaceBrush("AppBackground", bg);
                ReplaceBrush("AppForeground", fg);
                ReplaceBrush("AppBorderBrush", br);
                ReplaceBrush("ProgressBarForeground", progressBar);
                ReplaceBrush("BoxesBorderBrush", boxes);
                ReplaceBrush("BoxesBackgroundBrush", boxes, boxesFillPercent / 100.0);
                Current.Resources["BoxesColor"] = boxes;

                Current.Resources["AppBorderThickness"] = new Thickness(borderThickness);
                Current.Resources["AppBorderStrokeThickness"] = borderThickness;

                // Apply tooltip enablement globally (resource + windows)
                ApplyGlobalTooltipEnablement(tooltipsEnabled);

                // Solid tooltip bg that visually matches boxes over background
                RefreshTooltipBackgroundFrom(bg, boxes, boxesFillPercent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading theme: {ex.Message}");
            }
        }

        private static void RefreshTooltipBackgroundFrom(Color appBackground, Color boxesColor, double boxesFillPercent)
        {
            double a = Math.Max(0, Math.Min(100, boxesFillPercent)) / 100.0;
            byte Blend(byte f, byte b) => (byte)Math.Round(f * a + b * (1 - a));
            var composite = Color.FromRgb(
                Blend(boxesColor.R, appBackground.R),
                Blend(boxesColor.G, appBackground.G),
                Blend(boxesColor.B, appBackground.B)
            );
            ReplaceBrush("AppTooltipBackground", composite);
        }

        private static void ReplaceBrush(string key, Color c, double? opacity = null)
        {
            var b = new SolidColorBrush(c);
            if (opacity.HasValue) b.Opacity = Math.Max(0.0, Math.Min(1.0, opacity.Value));
            if (b.CanFreeze) b.Freeze();
            Current.Resources[key] = b;
        }

        private (byte r, byte g, byte b)? ParseRGB(string rgb)
        {
            var parts = rgb.Split(',');
            if (parts.Length != 3) return null;
            if (byte.TryParse(parts[0], out byte r) &&
                byte.TryParse(parts[1], out byte g) &&
                byte.TryParse(parts[2], out byte b))
                return (r, g, b);
            return null;
        }

        private static bool ParseBool(string? raw, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
            var v = raw.Trim().ToLowerInvariant();
            return v switch
            {
                "1" or "true" or "yes" or "on" => true,
                "0" or "false" or "no" or "off" => false,
                _ => defaultValue
            };
        }
    }
}
