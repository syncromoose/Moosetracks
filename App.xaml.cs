using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

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

                // Defaults (match your app defaults)
                Color bg = Colors.White;
                Color fg = Colors.Black;
                Color br = Colors.Gray;

                // Boxes defaults (same as SettingsPage defaults)
                Color boxes = Color.FromRgb(59, 130, 246); // #3B82F6
                double boxesFillPercent = 10.0;            // 0..100

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
                            if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                                                System.Globalization.CultureInfo.InvariantCulture, out var pct))
                                boxesFillPercent = Math.Max(0, Math.Min(100, pct));
                        }
                    }
                }

                // Replace resources so {DynamicResource} picks them up
                var bgBrush = new SolidColorBrush(bg);
                var fgBrush = new SolidColorBrush(fg);
                var brBrush = new SolidColorBrush(br);
                var boxesBorder = new SolidColorBrush(boxes);
                var boxesBackground = new SolidColorBrush(boxes) { Opacity = boxesFillPercent / 100.0 };

                // Freezing is fine since you REPLACE brushes later (you don't mutate them)
                if (bgBrush.CanFreeze) bgBrush.Freeze();
                if (fgBrush.CanFreeze) fgBrush.Freeze();
                if (brBrush.CanFreeze) brBrush.Freeze();
                if (boxesBorder.CanFreeze) boxesBorder.Freeze();
                if (boxesBackground.CanFreeze) boxesBackground.Freeze();

                Application.Current.Resources["AppBackground"] = bgBrush;
                Application.Current.Resources["AppForeground"] = fgBrush;
                Application.Current.Resources["AppBorderBrush"] = brBrush;

                // NEW: ensure these exist before any page loads
                Application.Current.Resources["BoxesBorderBrush"] = boxesBorder;
                Application.Current.Resources["BoxesBackgroundBrush"] = boxesBackground;
                Application.Current.Resources["BoxesColor"] = boxes; // optional color key
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading theme: {ex.Message}");
            }
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
