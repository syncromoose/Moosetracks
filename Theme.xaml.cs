using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MooseTracks.Views
{
    public partial class Theme : UserControl
    {
        // Folder where your program executable is located
        private readonly string settingsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
        private string configFile => Path.Combine(settingsFolder, "settings.cfg");
        private string logFile => Path.Combine(settingsFolder, "log.txt");

        // Theme colors as class-level fields
        private Color backgroundColor = Colors.Black;
        private Color foregroundColor = Colors.White;
        private Color borderColor = Color.FromRgb(255, 165, 0);

        // Boxes colour (drives all GroupBoxes + FFmpeg area)
        private Color boxesColor = Color.FromRgb(59, 130, 246); // #3B82F6 default
        private double boxesFillPercent = 10.0; // 0..100 -> BoxesBackgroundBrush.Opacity

        // Progress bar foreground (new)
        private Color progressBarColor = Color.FromRgb(59, 130, 246); // default to same blue

        // Border thickness (uniform) 0..4
        private double borderThickness = 1.0;

        private bool isInitialized; // Flag to prevent premature event handling

        public Theme()
        {
            Log("Entering Theme constructor");
            try
            {
                InitializeComponent();
                Log("InitializeComponent completed");

                EnsureSettingsFolder();
                Log("EnsureSettingsFolder completed");

                LoadThemeList();
                Log("LoadThemeList completed");

                LoadCurrentTheme();
                Log("LoadCurrentTheme completed");

                InitializeSliders();              // includes Boxes sliders + thickness + progress bar
                Log("InitializeSliders completed");

                ApplyTheme(backgroundColor, foregroundColor, borderColor);
                ApplyBoxesTheme(boxesColor, boxesFillPercent);
                ApplyProgressBarColor(progressBarColor); // NEW
                ApplyBorderThickness(borderThickness); // push current thickness to resources

                isInitialized = true; // set AFTER we’ve applied once
            }
            catch (Exception ex)
            {
                Log($"Error in Theme constructor: {ex.Message}");
                MessageBox.Show($"Error initializing Theme page: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Log("Exiting Theme constructor");
        }

        // ---------- Theme list / defaults ----------

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
                            if (BackgroundRSlider != null) BackgroundRSlider.Value = rgb.Value.r;
                            if (BackgroundGSlider != null) BackgroundGSlider.Value = rgb.Value.g;
                            if (BackgroundBSlider != null) BackgroundBSlider.Value = rgb.Value.b;
                            Log($"Loaded Background: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}");
                        }
                    }
                    else if (line.StartsWith("Foreground=", StringComparison.OrdinalIgnoreCase))
                    {
                        var rgb = ParseRGB(line.Substring("Foreground=".Length));
                        if (rgb != null)
                        {
                            foregroundColor = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                            if (ForegroundRSlider != null) ForegroundRSlider.Value = rgb.Value.r;
                            if (ForegroundGSlider != null) ForegroundGSlider.Value = rgb.Value.g;
                            if (ForegroundBSlider != null) ForegroundBSlider.Value = rgb.Value.b;
                            Log($"Loaded Foreground: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}");
                        }
                    }
                    else if (line.StartsWith("BorderBrush=", StringComparison.OrdinalIgnoreCase))
                    {
                        var rgb = ParseRGB(line.Substring("BorderBrush=".Length));
                        if (rgb != null)
                        {
                            borderColor = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                            if (BorderRSlider != null) BorderRSlider.Value = rgb.Value.r;
                            if (BorderGSlider != null) BorderGSlider.Value = rgb.Value.g;
                            if (BorderBSlider != null) BorderBSlider.Value = rgb.Value.b;
                            Log($"Loaded BorderBrush: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}");
                        }
                    }
                    else if (line.StartsWith("BoxesColor=", StringComparison.OrdinalIgnoreCase))
                    {
                        var rgb = ParseRGB(line.Substring("BoxesColor=".Length));
                        if (rgb != null)
                        {
                            boxesColor = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                            if (BoxesRSlider != null) BoxesRSlider.Value = rgb.Value.r;
                            if (BoxesGSlider != null) BoxesGSlider.Value = rgb.Value.g;
                            if (BoxesBSlider != null) BoxesBSlider.Value = rgb.Value.b;
                            Log($"Loaded BoxesColor: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}");
                        }
                    }
                    else if (line.StartsWith("BoxesFill=", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = line.Substring("BoxesFill=".Length).Trim();
                        if (double.TryParse(val, out var pct))
                        {
                            boxesFillPercent = Math.Max(0, Math.Min(100, pct));
                            if (BoxesFillSlider != null) BoxesFillSlider.Value = boxesFillPercent;
                            Log($"Loaded BoxesFill: {boxesFillPercent}%");
                        }
                    }
                    else if (line.StartsWith("BorderThickness=", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = line.Substring("BorderThickness=".Length).Trim();
                        if (double.TryParse(val, out var t))
                        {
                            borderThickness = Clamp(Math.Round(t), 0, 4);
                            if (BorderThicknessSlider != null) BorderThicknessSlider.Value = borderThickness;
                            Log($"Loaded BorderThickness: {borderThickness}");
                        }
                    }
                    else if (line.StartsWith("ProgressBarColor=", StringComparison.OrdinalIgnoreCase)) // NEW
                    {
                        var rgb = ParseRGB(line.Substring("ProgressBarColor=".Length));
                        if (rgb != null)
                        {
                            progressBarColor = Color.FromRgb(rgb.Value.r, rgb.Value.g, rgb.Value.b);
                            if (ProgressRSlider != null) ProgressRSlider.Value = rgb.Value.r;
                            if (ProgressGSlider != null) ProgressGSlider.Value = rgb.Value.g;
                            if (ProgressBSlider != null) ProgressBSlider.Value = rgb.Value.b;
                            Log($"Loaded ProgressBarColor: {rgb.Value.r},{rgb.Value.g},{rgb.Value.b}");
                        }
                    }
                }

                // Apply immediately
                ApplyTheme(backgroundColor, foregroundColor, borderColor);
                ApplyBoxesTheme(boxesColor, boxesFillPercent);
                ApplyProgressBarColor(progressBarColor); // NEW
                ApplyBorderThickness(borderThickness);

                Log($"Exiting LoadThemeFromFile: {themeFile}");
            }
            catch (Exception ex)
            {
                Log($"Error in LoadThemeFromFile: {ex.Message}");
                MessageBox.Show($"Error loading theme from file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- Live updates from controls ----------

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

        private void BorderThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isInitialized) { Log("BorderThicknessSlider_ValueChanged skipped: not initialized"); return; }
            try
            {
                var v = Clamp(Math.Round(e.NewValue), 0, 4);
                borderThickness = v;
                ApplyBorderThickness(borderThickness);
                Log($"Applied border thickness: {borderThickness}");
            }
            catch (Exception ex)
            {
                Log($"Error in BorderThicknessSlider_ValueChanged: {ex}");
                MessageBox.Show($"Error updating border thickness: {ex.Message}", "Error",
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

                lines.RemoveAll(l => l.StartsWith("Theme=", StringComparison.OrdinalIgnoreCase));
                lines.Add($"Theme={defaultTheme}");
                File.WriteAllLines(configFile, lines);

                MessageBox.Show($"Theme '{defaultTheme}' set as default.", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                Log($"Default theme set to: {defaultTheme}");
            }
            catch (Exception ex)
            {
                Log($"Error in SaveDefaultThemeButton_Click: {ex.Message}");
                MessageBox.Show($"Error saving default theme: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

        // NEW: Progress bar color live updates
        private void ProgressColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isInitialized) { Log("ProgressColorSlider_ValueChanged skipped: not initialized"); return; }
            try
            {
                progressBarColor = Color.FromRgb(
                    (byte)(ProgressRSlider?.Value ?? 59),
                    (byte)(ProgressGSlider?.Value ?? 130),
                    (byte)(ProgressBSlider?.Value ?? 246)
                );
                ApplyProgressBarColor(progressBarColor);
                Log($"Applied ProgressBarColor: {progressBarColor.R},{progressBarColor.G},{progressBarColor.B}");
            }
            catch (Exception ex)
            {
                Log($"Error in ProgressColorSlider_ValueChanged: {ex}");
                MessageBox.Show($"Error updating progress bar colour: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- Save/apply ----------

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
                    $"BoxesFill={boxesFillPercent:F1}",
                    $"BorderThickness={Math.Round(borderThickness):F0}",
                    $"ProgressBarColor={progressBarColor.R},{progressBarColor.G},{progressBarColor.B}" // NEW
                });

                LoadThemeList();
                if (ThemeComboBox != null) ThemeComboBox.SelectedItem = themeName;

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

                // Persist as default in settings.cfg
                string[] lines = File.Exists(configFile) ? File.ReadAllLines(configFile) : Array.Empty<string>();
                lines = lines.Where(l => l != null && !l.StartsWith("Theme=")).Append($"Theme={themeName}").ToArray();
                File.WriteAllLines(configFile, lines);

                // Save concrete colours to a .theme file as well
                string themeFile = Path.Combine(settingsFolder, $"{themeName}.theme");
                File.WriteAllLines(themeFile, new[]
                {
                    $"Background={backgroundColor.R},{backgroundColor.G},{backgroundColor.B}",
                    $"Foreground={foregroundColor.R},{foregroundColor.G},{foregroundColor.B}",
                    $"BorderBrush={borderColor.R},{borderColor.G},{borderColor.B}",
                    $"BoxesColor={boxesColor.R},{boxesColor.G},{boxesColor.B}",
                    $"BoxesFill={boxesFillPercent:F1}",
                    $"BorderThickness={Math.Round(borderThickness):F0}",
                    $"ProgressBarColor={progressBarColor.R},{progressBarColor.G},{progressBarColor.B}" // NEW
                });

                ApplyTheme(backgroundColor, foregroundColor, borderColor);
                ApplyBoxesTheme(boxesColor, boxesFillPercent);
                ApplyProgressBarColor(progressBarColor); // NEW
                ApplyBorderThickness(borderThickness);

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

        // ---------- Helpers ----------

        private void InitializeSliders()
        {
            try
            {
                Log("Entering InitializeSliders");

                // Seed from app resources
                if (Application.Current.Resources["AppBackground"] is SolidColorBrush appBg) backgroundColor = appBg.Color;
                if (Application.Current.Resources["AppForeground"] is SolidColorBrush appFg) foregroundColor = appFg.Color;
                if (Application.Current.Resources["AppBorderBrush"] is SolidColorBrush appBd) borderColor = appBd.Color;

                if (Application.Current.Resources["BoxesBorderBrush"] is SolidColorBrush b) boxesColor = b.Color;
                if (Application.Current.Resources["BoxesBackgroundBrush"] is SolidColorBrush bg)
                {
                    boxesFillPercent = Math.Round(bg.Opacity * 100.0, 1);
                }

                if (Application.Current.Resources["ProgressBarForeground"] is SolidColorBrush pbf) // NEW
                    progressBarColor = pbf.Color;

                if (Application.Current.Resources["AppBorderThickness"] is Thickness th)
                {
                    borderThickness = Clamp(th.Left, 0, 4); // assume uniform
                }

                // Sliders
                if (BackgroundRSlider != null) BackgroundRSlider.Value = backgroundColor.R;
                if (BackgroundGSlider != null) BackgroundGSlider.Value = backgroundColor.G;
                if (BackgroundBSlider != null) BackgroundBSlider.Value = backgroundColor.B;

                if (ForegroundRSlider != null) ForegroundRSlider.Value = foregroundColor.R;
                if (ForegroundGSlider != null) ForegroundGSlider.Value = foregroundColor.G;
                if (ForegroundBSlider != null) ForegroundBSlider.Value = foregroundColor.B;

                if (BorderRSlider != null) BorderRSlider.Value = borderColor.R;
                if (BorderGSlider != null) BorderGSlider.Value = borderColor.G;
                if (BorderBSlider != null) BorderBSlider.Value = borderColor.B;

                if (BoxesRSlider != null) BoxesRSlider.Value = boxesColor.R;
                if (BoxesGSlider != null) BoxesGSlider.Value = boxesColor.G;
                if (BoxesBSlider != null) BoxesBSlider.Value = boxesColor.B;
                if (BoxesFillSlider != null) BoxesFillSlider.Value = boxesFillPercent;

                if (ProgressRSlider != null) ProgressRSlider.Value = progressBarColor.R; // NEW
                if (ProgressGSlider != null) ProgressGSlider.Value = progressBarColor.G; // NEW
                if (ProgressBSlider != null) ProgressBSlider.Value = progressBarColor.B; // NEW

                if (BorderThicknessSlider != null) BorderThicknessSlider.Value = borderThickness;

                Log("Exiting InitializeSliders");
            }
            catch (Exception ex)
            {
                Log($"Error in InitializeSliders: {ex.Message}");
                MessageBox.Show($"Error initializing sliders: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void ApplyProgressBarColor(Color c) // NEW
        {
            ReplaceBrush("ProgressBarForeground", c);
        }

        private void ApplyBorderThickness(double t)
        {
            var v = Clamp(Math.Round(t), 0, 4);
            ReplaceResource("AppBorderThickness", new Thickness(v));
            ReplaceResource("AppBorderStrokeThickness", v);

            foreach (var dict in Application.Current.Resources.MergedDictionaries)
            {
                if (dict.Contains("AppBorderThickness"))
                {
                    dict["AppBorderThickness"] = new Thickness(v);
                    break;
                }
            }
        }

        private void ReplaceResource(string key, object value)
        {
            if (Dispatcher.CheckAccess())
                Application.Current.Resources[key] = value;
            else
                Dispatcher.Invoke(() => Application.Current.Resources[key] = value);
        }

        private void ReplaceBrush(string key, Color color, double? opacity = null)
        {
            var brush = new SolidColorBrush(color);
            if (opacity.HasValue) brush.Opacity = Math.Max(0.0, Math.Min(1.0, opacity.Value));
            if (brush.CanFreeze) brush.Freeze();
            ReplaceResource(key, brush);
        }

        private void ApplyBoxesTheme(Color color, double fillPercent)
        {
            try
            {
                Log("Entering ApplyBoxesTheme");
                var fill = Math.Max(0, Math.Min(100, fillPercent)) / 100.0;

                // Existing updates
                ReplaceBrush("BoxesBorderBrush", color);
                ReplaceBrush("BoxesBackgroundBrush", color, fill);
                ReplaceResource("BoxesColor", color);

                // Keep tooltip look in sync: composite on background
                var appBgBrush = Application.Current.Resources["AppBackground"] as SolidColorBrush;
                var bg = appBgBrush?.Color ?? Colors.Black;

                byte r = (byte)Math.Round(color.R * fill + bg.R * (1 - fill));
                byte g = (byte)Math.Round(color.G * fill + bg.G * (1 - fill));
                byte b = (byte)Math.Round(color.B * fill + bg.B * (1 - fill));
                var composite = Color.FromRgb(r, g, b);

                ReplaceBrush("AppTooltipBackground", composite);

                Log($"Applied Boxes theme + tooltip composite: {r},{g},{b} (fill {fillPercent}%)");
                Log("Exiting ApplyBoxesTheme");
            }
            catch (Exception ex)
            {
                Log($"Error in ApplyBoxesTheme: {ex}");
                MessageBox.Show($"Error applying boxes theme: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static double Clamp(double v, double min, double max) =>
            v < min ? min : (v > max ? max : v);

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
                Directory.CreateDirectory(settingsFolder);
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { /* ignore logging errors */ }
        }
    }
}
