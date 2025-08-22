using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic; // NEW: for List<T>

namespace MooseTracks.Views
{
    public partial class SettingsPage : UserControl
    {
        // Folder where your program executable is located
        private readonly string settingsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
        private string configFile => Path.Combine(settingsFolder, "settings.cfg");
        private string logFile => Path.Combine(settingsFolder, "log.txt");

        // Theme colors as class-level fields
        private Color backgroundColor = Colors.Black;
        private Color foregroundColor = Colors.White;
        private Color borderColor = Color.FromRgb(255, 165, 0);

        // NEW: Boxes colour (drives all GroupBoxes + FFmpeg area)
        private Color boxesColor = Color.FromRgb(59, 130, 246); // #3B82F6 default
        private double boxesFillPercent = 10.0; // 0..100 -> BoxesBackgroundBrush.Opacity

        private bool isInitialized; // Flag to prevent premature event handling

        public SettingsPage()
        {
            Log("Entering SettingsPage constructor");
            try
            {
                InitializeComponent();
                Log("InitializeComponent completed");
                EnsureSettingsFolder();
                Log("EnsureSettingsFolder completed");
                LoadFFmpegPath();
                Log("LoadFFmpegPath completed");
                LoadThemeList();
                Log("LoadThemeList completed");
                LoadCurrentTheme();
                Log("LoadCurrentTheme completed");
                InitializeSliders();              // includes Boxes sliders now
                Log("InitializeSliders completed");
                ApplyTheme(backgroundColor, foregroundColor, borderColor);
                ApplyBoxesTheme(boxesColor, boxesFillPercent);

                isInitialized = true; // set AFTER we’ve applied once
            }
            catch (Exception ex)
            {
                Log($"Error in SettingsPage constructor: {ex.Message}");
                MessageBox.Show($"Error initializing Settings page: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Log("Exiting SettingsPage constructor");
        }

        private void InitializeSliders()
        {
            try
            {
                Log("Entering InitializeSliders");

                // Try to read current app-wide resources to seed fields (so the UI reflects the active theme)
                if (Application.Current.Resources["AppBackground"] is SolidColorBrush appBg) backgroundColor = appBg.Color;
                if (Application.Current.Resources["AppForeground"] is SolidColorBrush appFg) foregroundColor = appFg.Color;
                if (Application.Current.Resources["AppBorderBrush"] is SolidColorBrush appBd) borderColor = appBd.Color;

                // NEW: Also seed Boxes from resources if present
                if (Application.Current.Resources["BoxesBorderBrush"] is SolidColorBrush b) boxesColor = b.Color;
                if (Application.Current.Resources["BoxesBackgroundBrush"] is SolidColorBrush bg)
                {
                    // keep colour consistent with border; user controls fill % separately
                    boxesFillPercent = Math.Round(bg.Opacity * 100.0, 1);
                }

                // Existing sliders
                if (BackgroundRSlider != null) BackgroundRSlider.Value = backgroundColor.R; else Log("BackgroundRSlider is null");
                if (BackgroundGSlider != null) BackgroundGSlider.Value = backgroundColor.G; else Log("BackgroundGSlider is null");
                if (BackgroundBSlider != null) BackgroundBSlider.Value = backgroundColor.B; else Log("BackgroundBSlider is null");

                if (ForegroundRSlider != null) ForegroundRSlider.Value = foregroundColor.R; else Log("ForegroundRSlider is null");
                if (ForegroundGSlider != null) ForegroundGSlider.Value = foregroundColor.G; else Log("ForegroundGSlider is null");
                if (ForegroundBSlider != null) ForegroundBSlider.Value = foregroundColor.B; else Log("ForegroundBSlider is null");

                if (BorderRSlider != null) BorderRSlider.Value = borderColor.R; else Log("BorderRSlider is null");
                if (BorderGSlider != null) BorderGSlider.Value = borderColor.G; else Log("BorderGSlider is null");
                if (BorderBSlider != null) BorderBSlider.Value = borderColor.B; else Log("BorderBSlider is null");

                // NEW: Boxes sliders
                if (BoxesRSlider != null) BoxesRSlider.Value = boxesColor.R; else Log("BoxesRSlider is null");
                if (BoxesGSlider != null) BoxesGSlider.Value = boxesColor.G; else Log("BoxesGSlider is null");
                if (BoxesBSlider != null) BoxesBSlider.Value = boxesColor.B; else Log("BoxesBSlider is null");
                if (BoxesFillSlider != null) BoxesFillSlider.Value = boxesFillPercent; else Log("BoxesFillSlider is null");

                Log("Exiting InitializeSliders");
            }
            catch (Exception ex)
            {
                Log($"Error in InitializeSliders: {ex.Message}");
                MessageBox.Show($"Error initializing sliders: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFFmpegPath()
        {
            try
            {
                Log("Entering LoadFFmpegPath");
                if (File.Exists(configFile))
                {
                    string[] lines = File.ReadAllLines(configFile);
                    string ffmpegLine = lines.FirstOrDefault(l => l != null && l.StartsWith("FFmpegPath="));
                    if (ffmpegLine != null)
                    {
                        FFmpegPathTextBox.Text = ffmpegLine.Substring("FFmpegPath=".Length).Trim();
                        Log($"Loaded FFmpeg path: {FFmpegPathTextBox.Text}");
                    }
                }
                Log("Exiting LoadFFmpegPath");
            }
            catch (Exception ex)
            {
                Log($"Error in LoadFFmpegPath: {ex.Message}");
                MessageBox.Show($"Error loading FFmpeg path: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseFFmpegButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("Entering BrowseFFmpegButton_Click");
                var dlg = new OpenFileDialog
                {
                    Filter = "FFmpeg executable|ffmpeg.exe|All files|*.*",
                    FileName = "ffmpeg.exe"
                };
                if (dlg.ShowDialog() == true)
                {
                    FFmpegPathTextBox.Text = dlg.FileName;
                    Log($"Selected FFmpeg path: {dlg.FileName}");
                }
                Log("Exiting BrowseFFmpegButton_Click");
            }
            catch (Exception ex)
            {
                Log($"Error in BrowseFFmpegButton_Click: {ex.Message}");
                MessageBox.Show($"Error browsing FFmpeg path: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveFFmpegButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("Entering SaveFFmpegButton_Click");
                string path = FFmpegPathTextBox.Text;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    Log("Invalid FFmpeg path");
                    MessageBox.Show("Please select a valid FFmpeg executable.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                EnsureSettingsFolder();
                string[] lines = File.Exists(configFile) ? File.ReadAllLines(configFile) : Array.Empty<string>();
                lines = lines.Where(l => l != null && !l.StartsWith("FFmpegPath=")).Append($"FFmpegPath={path}").ToArray();
                File.WriteAllLines(configFile, lines);
                Log($"Saved FFmpeg path: {path}");
                MessageBox.Show("FFmpeg path saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Log("Exiting SaveFFmpegButton_Click");
            }
            catch (Exception ex)
            {
                Log($"Error in SaveFFmpegButton_Click: {ex.Message}");
                MessageBox.Show($"Error saving FFmpeg path: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadThemeList()
        {
            try
            {
                EnsureSettingsFolder();

                var themeFiles = Directory.GetFiles(settingsFolder, "*.theme", SearchOption.TopDirectoryOnly);
                ThemeComboBox?.Items.Clear();
                foreach (var file in themeFiles)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrWhiteSpace(name)) ThemeComboBox.Items.Add(name);
                }

                // Select current default from settings.cfg if present
                string defaultTheme = null;
                if (File.Exists(configFile))
                {
                    var line = File.ReadLines(configFile).FirstOrDefault(l => l.StartsWith("Theme="));
                    if (!string.IsNullOrWhiteSpace(line))
                        defaultTheme = line.Substring("Theme=".Length).Trim();
                }

                if (!string.IsNullOrWhiteSpace(defaultTheme) && ThemeComboBox.Items.Contains(defaultTheme))
                    ThemeComboBox.SelectedItem = defaultTheme;
                else if (ThemeComboBox.Items.Count > 0)
                    ThemeComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading theme list: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDefaultThemeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ThemeComboBox?.SelectedItem == null)
                {
                    MessageBox.Show("Please select a theme to set as default.", "Invalid Input",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string defaultTheme = ThemeComboBox.SelectedItem.ToString();
                EnsureSettingsFolder();

                var lines = File.Exists(configFile)
                    ? File.ReadAllLines(configFile).ToList()
                    : new List<string>();

                lines.RemoveAll(l => l.StartsWith("Theme="));
                lines.Add($"Theme={defaultTheme}");
                File.WriteAllLines(configFile, lines);

                MessageBox.Show($"Theme '{defaultTheme}' set as default.", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving default theme: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCurrentTheme()
        {
            try
            {
                Log("Entering LoadCurrentTheme");
                if (File.Exists(configFile))
                {
                    string[] lines = File.ReadAllLines(configFile);
                    string themeLine = lines.FirstOrDefault(l => l != null && l.StartsWith("Theme="));
                    if (themeLine != null)
                    {
                        string themeName = themeLine.Substring("Theme=".Length).Trim();
                        if (!string.IsNullOrWhiteSpace(themeName))
                        {
                            string themeFile = Path.Combine(settingsFolder, $"{themeName}.theme");
                            if (File.Exists(themeFile))
                            {
                                LoadThemeFromFile(themeFile);
                                if (ThemeComboBox != null)
                                {
                                    ThemeComboBox.SelectedItem = themeName;
                                    Log($"Loaded current theme: {themeName}");
                                }
                            }
                        }
                    }
                }
                Log("Exiting LoadCurrentTheme");
            }
            catch (Exception ex)
            {
                Log($"Error in LoadCurrentTheme: {ex.Message}");
                MessageBox.Show($"Error loading current theme: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInitialized)
            {
                Log("ThemeComboBox_SelectionChanged skipped: not initialized");
                return;
            }
            try
            {
                Log("Entering ThemeComboBox_SelectionChanged");
                if (ThemeComboBox?.SelectedItem != null)
                {
                    string themeName = ThemeComboBox.SelectedItem.ToString();
                    if (!string.IsNullOrWhiteSpace(themeName))
                    {
                        string themeFile = Path.Combine(settingsFolder, $"{themeName}.theme");
                        if (File.Exists(themeFile))
                        {
                            LoadThemeFromFile(themeFile);
                            Log($"Selected theme: {themeName}");
                        }
                    }
                }
                Log("Exiting ThemeComboBox_SelectionChanged");
            }
            catch (Exception ex)
            {
                Log($"Error in ThemeComboBox_SelectionChanged: {ex.Message}");
                MessageBox.Show($"Error selecting theme: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadThemeFromFile(string themeFile)
        {
            try
            {
                Log($"Entering LoadThemeFromFile: {themeFile}");
                if (!File.Exists(themeFile))
                {
                    Log($"Theme file does not exist: {themeFile}");
                    return;
                }
                string[] lines = File.ReadAllLines(themeFile);
                foreach (string line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                {
                    if (line.StartsWith("Background=", StringComparison.OrdinalIgnoreCase))
                    {
                        var rgb = ParseRGB(line.Substring("Background=".Length));
                        if (rgb != null)
                        {
                            backgroundColor = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                            if (BackgroundRSlider != null) BackgroundRSlider.Value = rgb.Value.r; else Log("BackgroundRSlider is null in LoadThemeFromFile");
                            if (BackgroundGSlider != null) BackgroundGSlider.Value = rgb.Value.g; else Log("BackgroundGSlider is null in LoadThemeFromFile");
                            if (BackgroundBSlider != null) BackgroundBSlider.Value = rgb.Value.b; else Log("BackgroundBSlider is null in LoadThemeFromFile");
                            Log($"Loaded Background: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}");
                        }
                    }
                    else if (line.StartsWith("Foreground=", StringComparison.OrdinalIgnoreCase))
                    {
                        var rgb = ParseRGB(line.Substring("Foreground=".Length));
                        if (rgb != null)
                        {
                            foregroundColor = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                            if (ForegroundRSlider != null) ForegroundRSlider.Value = rgb.Value.r; else Log("ForegroundRSlider is null in LoadThemeFromFile");
                            if (ForegroundGSlider != null) ForegroundGSlider.Value = rgb.Value.g; else Log("ForegroundGSlider is null in LoadThemeFromFile");
                            if (ForegroundBSlider != null) ForegroundBSlider.Value = rgb.Value.b; else Log("ForegroundBSlider is null in LoadThemeFromFile");
                            Log($"Loaded Foreground: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}");
                        }
                    }
                    else if (line.StartsWith("BorderBrush=", StringComparison.OrdinalIgnoreCase))
                    {
                        var rgb = ParseRGB(line.Substring("BorderBrush=".Length));
                        if (rgb != null)
                        {
                            borderColor = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                            if (BorderRSlider != null) BorderRSlider.Value = rgb.Value.r; else Log("BorderRSlider is null in LoadThemeFromFile");
                            if (BorderGSlider != null) BorderGSlider.Value = rgb.Value.g; else Log("BorderGSlider is null in LoadThemeFromFile");
                            if (BorderBSlider != null) BorderBSlider.Value = rgb.Value.b; else Log("BorderBSlider is null in LoadThemeFromFile");
                            Log($"Loaded BorderBrush: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}");
                        }
                    }
                    // NEW: Boxes color + fill
                    else if (line.StartsWith("BoxesColor=", StringComparison.OrdinalIgnoreCase))
                    {
                        var rgb = ParseRGB(line.Substring("BoxesColor=".Length));
                        if (rgb != null)
                        {
                            boxesColor = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                            if (BoxesRSlider != null) BoxesRSlider.Value = rgb.Value.r; else Log("BoxesRSlider is null in LoadThemeFromFile");
                            if (BoxesGSlider != null) BoxesGSlider.Value = rgb.Value.g; else Log("BoxesGSlider is null in LoadThemeFromFile");
                            if (BoxesBSlider != null) BoxesBSlider.Value = rgb.Value.b; else Log("BoxesBSlider is null in LoadThemeFromFile");
                            Log($"Loaded BoxesColor: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}");
                        }
                    }
                    else if (line.StartsWith("BoxesFill=", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = line.Substring("BoxesFill=".Length).Trim();
                        if (double.TryParse(val, out var pct))
                        {
                            boxesFillPercent = Math.Max(0, Math.Min(100, pct));
                            if (BoxesFillSlider != null) BoxesFillSlider.Value = boxesFillPercent; else Log("BoxesFillSlider is null in LoadThemeFromFile");
                            Log($"Loaded BoxesFill: {boxesFillPercent}%");
                        }
                    }
                }

                // Apply boxes to live resources immediately so other pages reflect it
                ApplyTheme(backgroundColor, foregroundColor, borderColor);
                ApplyBoxesTheme(boxesColor, boxesFillPercent);

                Log($"Exiting LoadThemeFromFile: {themeFile}");
            }
            catch (Exception ex)
            {
                Log($"Error in LoadThemeFromFile: {ex.Message}");
                MessageBox.Show($"Error loading theme from file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isInitialized) { Log("ColorSlider_ValueChanged skipped: not initialized"); return; }
            try
            {
                Log("Entering ColorSlider_ValueChanged");
                backgroundColor = Color.FromRgb(
                    (byte)(BackgroundRSlider?.Value ?? 0),
                    (byte)(BackgroundGSlider?.Value ?? 0),
                    (byte)(BackgroundBSlider?.Value ?? 0)
                );
                foregroundColor = Color.FromRgb(
                    (byte)(ForegroundRSlider?.Value ?? 255),
                    (byte)(ForegroundGSlider?.Value ?? 255),
                    (byte)(ForegroundBSlider?.Value ?? 255)
                );
                borderColor = Color.FromRgb(
                    (byte)(BorderRSlider?.Value ?? 255),
                    (byte)(BorderGSlider?.Value ?? 165),
                    (byte)(BorderBSlider?.Value ?? 0)
                );

                // Apply immediately (replace, on UI thread)
                ReplaceBrush("AppBackground", backgroundColor);
                ReplaceBrush("AppForeground", foregroundColor);
                ReplaceBrush("AppBorderBrush", borderColor);

                Log("Exiting ColorSlider_ValueChanged");
            }
            catch (Exception ex)
            {
                Log($"Error in ColorSlider_ValueChanged: {ex}");
                MessageBox.Show($"Error updating colors: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // NEW: Handles Boxes R/G/B and Fill% sliders — applies immediately app-wide
        private void BoxesColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isInitialized) { Log("BoxesColorSlider_ValueChanged skipped: not initialized"); return; }
            try
            {
                Log("Entering BoxesColorSlider_ValueChanged");
                boxesColor = Color.FromRgb(
                    (byte)(BoxesRSlider?.Value ?? 59),
                    (byte)(BoxesGSlider?.Value ?? 130),
                    (byte)(BoxesBSlider?.Value ?? 246)
                );
                boxesFillPercent = (BoxesFillSlider?.Value ?? 10.0);
                ApplyBoxesTheme(boxesColor, boxesFillPercent);
                Log("Exiting BoxesColorSlider_ValueChanged");
            }
            catch (Exception ex)
            {
                Log($"Error in BoxesColorSlider_ValueChanged: {ex}");
                MessageBox.Show($"Error updating box colour: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void EnsureSettingsFolder()
        {
            try
            {
                if (!Directory.Exists(settingsFolder))
                {
                    Directory.CreateDirectory(settingsFolder);
                    Log($"Settings folder created at: {settingsFolder}");
                }
                else
                {
                    Log($"Settings folder already exists: {settingsFolder}");
                }
            }
            catch (Exception ex)
            {
                Log($"Error creating settings folder: {ex.Message}");
                MessageBox.Show($"Could not create settings folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(settingsFolder); // Ensure folder exists
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // If logging fails, silently ignore (optional)
            }
        }

        private void SaveThemeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("Entering SaveThemeButton_Click");

                string themeName = ThemeNameTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(themeName))
                {
                    Log("Invalid theme name");
                    MessageBox.Show("Please enter a theme name.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                EnsureSettingsFolder();

                string themeFile = Path.Combine(settingsFolder, $"{themeName}.theme");
                File.WriteAllLines(themeFile, new[]
                {
                    $"Background={backgroundColor.R},{backgroundColor.G},{backgroundColor.B}",
                    $"Foreground={foregroundColor.R},{foregroundColor.G},{foregroundColor.B}",
                    $"BorderBrush={borderColor.R},{borderColor.G},{borderColor.B}",
                    $"BoxesColor={boxesColor.R},{boxesColor.G},{boxesColor.B}",
                    $"BoxesFill={boxesFillPercent:F1}"
                });

                LoadThemeList();

                if (ThemeComboBox != null)
                {
                    ThemeComboBox.SelectedItem = themeName;
                }

                Log($"Saved theme: {themeName}");
                MessageBox.Show($"Theme '{themeName}' saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Log("Exiting SaveThemeButton_Click");
            }
            catch (Exception ex)
            {
                Log($"Error in SaveThemeButton_Click: {ex.Message}");
                MessageBox.Show($"Error saving theme: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyThemeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("Entering ApplyThemeButton_Click");
                EnsureSettingsFolder();
                string themeName = ThemeComboBox?.SelectedItem?.ToString() ?? "Custom";
                string[] lines = File.Exists(configFile) ? File.ReadAllLines(configFile) : Array.Empty<string>();
                lines = lines.Where(l => l != null && !l.StartsWith("Theme=")).Append($"Theme={themeName}").ToArray();
                File.WriteAllLines(configFile, lines);

                string themeFile = Path.Combine(settingsFolder, $"{themeName}.theme");
                File.WriteAllLines(themeFile, new[]
                {
                    $"Background={backgroundColor.R},{backgroundColor.G},{backgroundColor.B}",
                    $"Foreground={foregroundColor.R},{foregroundColor.G},{foregroundColor.B}",
                    $"BorderBrush={borderColor.R},{borderColor.G},{borderColor.B}",
                    // NEW:
                    $"BoxesColor={boxesColor.R},{boxesColor.G},{boxesColor.B}",
                    $"BoxesFill={boxesFillPercent:F1}"
                });

                ApplyTheme(backgroundColor, foregroundColor, borderColor);
                ApplyBoxesTheme(boxesColor, boxesFillPercent); // NEW: push boxes to app resources

                Log($"Applied theme: {themeName}");
                MessageBox.Show("Theme applied successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Log("Exiting ApplyThemeButton_Click");
            }
            catch (Exception ex)
            {
                Log($"Error in ApplyThemeButton_Click: {ex.Message}");
                MessageBox.Show($"Error applying theme: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (byte r, byte g, byte b)? ParseRGB(string rgb)
        {
            try
            {
                Log($"Entering ParseRGB: {rgb}");
                if (string.IsNullOrWhiteSpace(rgb)) return null;
                var parts = rgb.Split(',');
                if (parts.Length != 3) return null;
                if (byte.TryParse(parts[0], out byte r) &&
                    byte.TryParse(parts[1], out byte g) &&
                    byte.TryParse(parts[2], out byte b))
                {
                    Log($"Parsed RGB: {r},{g},{b}");
                    return (r, g, b);
                }
                return null;
            }
            catch (Exception ex)
            {
                Log($"Error in ParseRGB: {ex.Message}");
                return null;
            }
        }

        private void ApplyTheme(Color background, Color foreground, Color border)
        {
            try
            {
                Log("Entering ApplyTheme");
                ReplaceBrush("AppBackground", background);
                ReplaceBrush("AppForeground", foreground);
                ReplaceBrush("AppBorderBrush", border);
                Log("Exiting ApplyTheme");
            }
            catch (Exception ex)
            {
                Log($"Error in ApplyTheme: {ex}");
                MessageBox.Show($"Error applying theme: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void ReplaceResource(string key, object value)
        {
            // Ensure we're on the UI thread when touching Application.Current.Resources
            if (Dispatcher.CheckAccess())
                Application.Current.Resources[key] = value;
            else
                Dispatcher.Invoke(() => Application.Current.Resources[key] = value);
        }

        private void ReplaceBrush(string key, Color color, double? opacity = null)
        {
            var brush = new SolidColorBrush(color);
            if (opacity.HasValue) brush.Opacity = Math.Max(0.0, Math.Min(1.0, opacity.Value));
            // Freezing is safe (we always REPLACE, never mutate)
            if (brush.CanFreeze) brush.Freeze();
            ReplaceResource(key, brush);
        }


        // NEW: centralised function to apply boxes theme live
        private void ApplyBoxesTheme(Color color, double fillPercent)
        {
            try
            {
                Log("Entering ApplyBoxesTheme");
                var fill = Math.Max(0, Math.Min(100, fillPercent)) / 100.0;

                ReplaceBrush("BoxesBorderBrush", color);
                ReplaceBrush("BoxesBackgroundBrush", color, fill);
                ReplaceResource("BoxesColor", color); // optional, but handy

                Log($"Applied Boxes theme: {color.R},{color.G},{color.B}, Fill {fillPercent}%");
                Log("Exiting ApplyBoxesTheme");
            }
            catch (Exception ex)
            {
                Log($"Error in ApplyBoxesTheme: {ex}");
                MessageBox.Show($"Error applying boxes theme: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
