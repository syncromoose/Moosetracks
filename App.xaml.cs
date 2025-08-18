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

                // Defaults (used if theme file missing or incomplete)
                Color bg = Colors.White;
                Color fg = Colors.Black;
                Color br = Colors.Gray;

                if (File.Exists(themeFile))
                {
                    foreach (var line in File.ReadLines(themeFile))
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
                    }
                }

                // Replace the existing keys so DynamicResource picks up the change
                var bgBrush = new SolidColorBrush(bg);
                var fgBrush = new SolidColorBrush(fg);
                var brBrush = new SolidColorBrush(br);
                bgBrush.Freeze(); fgBrush.Freeze(); brBrush.Freeze();

                Application.Current.Resources["AppBackground"] = bgBrush;
                Application.Current.Resources["AppForeground"] = fgBrush;
                Application.Current.Resources["AppBorderBrush"] = brBrush;
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
