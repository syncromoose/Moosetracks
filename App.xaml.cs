using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Globalization; // for InvariantCulture parsing

namespace MooseTracks
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load theme before any window is shown
            LoadTheme();

            // Now show main window
            MainWindow = new MainWindow();
            MainWindow.Show();
        }

        private void LoadTheme()
        {
            try
            {
                string settingsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
                string configFile = Path.Combine(settingsFolder, "settings.cfg");

                string themeName = "default";
                if (File.Exists(configFile))
                {
                    string themeLine = File.ReadLines(configFile)
                                           .FirstOrDefault(l => l.StartsWith("Theme=", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(themeLine))
                        themeName = themeLine.Substring("Theme=".Length).Trim();
                }

                string themeFile = Path.Combine(settingsFolder, $"{themeName}.theme");

                // Defaults (align with your app’s expected baseline)
                Color bg = Colors.White;
                Color fg = Colors.Black;
                Color br = Colors.Gray;

                // Boxes defaults (same as SettingsPage defaults)
                Color boxes = Color.FromRgb(59, 130, 246); // #3B82F6
                double boxesFillPercent = 10.0;            // 0..100

                // Border thickness defaults (layout thickness and overlay stroke)
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
                    }
                }

                // Replace resources so {DynamicResource} picks them up
                ReplaceBrush("AppBackground", bg);
                ReplaceBrush("AppForeground", fg);
                ReplaceBrush("AppBorderBrush", br);

                ReplaceBrush("BoxesBorderBrush", boxes);
                ReplaceBrush("BoxesBackgroundBrush", boxes, boxesFillPercent / 100.0);
                Application.Current.Resources["BoxesColor"] = boxes;

                // IMPORTANT: set both the layout thickness and the overlay stroke thickness
                Application.Current.Resources["AppBorderThickness"] = new Thickness(borderThickness);
                Application.Current.Resources["AppBorderStrokeThickness"] = borderThickness;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading theme: {ex.Message}");
            }
        }

        private static void ReplaceBrush(string key, Color c, double? opacity = null)
        {
            var b = new SolidColorBrush(c);
            if (opacity.HasValue) b.Opacity = Math.Max(0.0, Math.Min(1.0, opacity.Value));
            if (b.CanFreeze) b.Freeze();
            Application.Current.Resources[key] = b;
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
    }
}
