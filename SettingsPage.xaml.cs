using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

namespace MooseTracks.Views
{
    public partial class SettingsPage : UserControl
    {
        // Folder where your program executable is located
        private readonly string settingsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
        private string configFile => Path.Combine(settingsFolder, "settings.cfg");
        private string logFile => Path.Combine(settingsFolder, "log.txt");

        // Model for the startup page dropdown: friendly label + key you actually use
        private sealed class PageOption
        {
            public string Label { get; }
            public string Key { get; } // e.g., "ContainerPage"
            public PageOption(string label, string key) { Label = label; Key = key; }
        }

        private static readonly List<PageOption> _pageOptions = new()
        {
            new("Containers",  "ContainerPage"),
            new("Extraction",  "ExtractionPage"),
            new("Transcoding", "TranscodingPage"),
            new("Theme",       "Theme"),
            new("Settings",    "SettingsPage"),
        };

        public SettingsPage()
        {
            Log("Entering SettingsPage constructor");
            try
            {
                InitializeComponent();
                EnsureSettingsFolder();

                EnsureStartupComboItems();
                LoadStartupDefault();

                LoadFFmpegPath();
                LoadTooltipsEnabled();
            }
            catch (Exception ex)
            {
                Log($"Error in SettingsPage constructor: {ex.Message}");
                MessageBox.Show($"Error initializing Settings page: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Log("Exiting SettingsPage constructor");
        }

        // ---------- Startup page wiring ----------

        private void EnsureStartupComboItems()
        {
            try
            {
                StartupPageCombo.ItemsSource = _pageOptions;
                // DisplayMemberPath/SelectedValuePath are set in XAML
                if (StartupPageCombo.SelectedIndex < 0)
                    StartupPageCombo.SelectedValue = "ExtractionPage"; // default
            }
            catch (Exception ex)
            {
                Log($"Error in EnsureStartupComboItems: {ex.Message}");
            }
        }

        // Map legacy values (old labels or old key spellings) to current keys
        private static string MapLegacyValueToKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "ExtractionPage";
            var v = value.Trim();

            // If already a known key, keep it
            if (_pageOptions.Any(p => p.Key.Equals(v, StringComparison.OrdinalIgnoreCase)))
                return _pageOptions.First(p => p.Key.Equals(v, StringComparison.OrdinalIgnoreCase)).Key;

            // Accept labels or older names
            return v.ToLowerInvariant() switch
            {
                "container" => "ContainerPage",
                "containers" => "ContainerPage",
                "extraction" => "ExtractionPage",
                "transcoding" => "TranscodingPage",
                "theme" => "Theme",
                "settings" => "SettingsPage",
                _ => "ExtractionPage"
            };
        }

        private void LoadStartupDefault()
        {
            try
            {
                if (!File.Exists(configFile))
                {
                    StartupPageCombo.SelectedValue = "ExtractionPage";
                    return;
                }

                var line = File.ReadLines(configFile).FirstOrDefault(l => l.StartsWith("StartupPage=", StringComparison.OrdinalIgnoreCase));
                var raw = line?.Substring("StartupPage=".Length).Trim();
                var key = MapLegacyValueToKey(raw);

                StartupPageCombo.SelectedValue = key;
            }
            catch (Exception ex)
            {
                Log($"Error in LoadStartupDefault: {ex.Message}");
            }
        }
        //tooltips loading code
        private void LoadTooltipsEnabled()
        {
            try
            {
                bool enabled = true; // default
                if (File.Exists(configFile))
                {
                    foreach (var line in File.ReadLines(configFile))
                    {
                        if (line.StartsWith("Tooltips=", StringComparison.OrdinalIgnoreCase))
                        {
                            var raw = line.Substring("Tooltips=".Length).Trim();
                            enabled = ParseBool(raw, true);
                            break;
                        }
                    }
                }

                EnableTooltipsCheckbox.IsChecked = enabled;

                // Apply immediately to running app so user sees the effect without restart
                Application.Current.Resources["TooltipsEnabled"] = enabled;
            }
            catch (Exception ex)
            {
                Log($"Error in LoadTooltipsEnabled: {ex.Message}");
            }
        }

        private void SaveTooltipsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool enabled = EnableTooltipsCheckbox.IsChecked == true;

                EnsureSettingsFolder();
                var lines = File.Exists(configFile)
                    ? File.ReadAllLines(configFile).ToList()
                    : new List<string>();

                lines.RemoveAll(l => l.StartsWith("Tooltips=", StringComparison.OrdinalIgnoreCase));
                lines.Add($"Tooltips={(enabled ? "True" : "False")}");
                File.WriteAllLines(configFile, lines);

                // Apply immediately
                Application.Current.Resources["TooltipsEnabled"] = enabled;

                MessageBox.Show($"Tooltips {(enabled ? "enabled" : "disabled")}.", "Saved",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"Error in SaveTooltipsButton_Click: {ex.Message}");
                MessageBox.Show($"Error saving tooltips setting: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // small helper (same logic as App.ParseBool; you can share it)
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




        private void SaveStartupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var key = StartupPageCombo?.SelectedValue?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                {
                    MessageBox.Show("Please select a startup page.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                EnsureSettingsFolder();
                var lines = File.Exists(configFile)
                    ? File.ReadAllLines(configFile).ToList()
                    : new List<string>();

                lines.RemoveAll(l => l.StartsWith("StartupPage=", StringComparison.OrdinalIgnoreCase));
                lines.Add($"StartupPage={key}");
                File.WriteAllLines(configFile, lines);

                var label = (_pageOptions.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Label) ?? key;
                MessageBox.Show($"Startup page set to '{label}'.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                Log($"Saved StartupPage={key}");
            }
            catch (Exception ex)
            {
                Log($"Error in SaveStartupButton_Click: {ex.Message}");
                MessageBox.Show($"Error saving startup page: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- FFmpeg path ----------

        private void LoadFFmpegPath()
        {
            try
            {
                if (File.Exists(configFile))
                {
                    string[] lines = File.ReadAllLines(configFile);
                    string ffmpegLine = lines.FirstOrDefault(l => l != null && l.StartsWith("FFmpegPath="));
                    if (ffmpegLine != null)
                        FFmpegPathTextBox.Text = ffmpegLine.Substring("FFmpegPath=".Length).Trim();
                }
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
                var dlg = new OpenFileDialog
                {
                    Filter = "FFmpeg executable|ffmpeg.exe|All files|*.*",
                    FileName = "ffmpeg.exe"
                };
                if (dlg.ShowDialog() == true)
                    FFmpegPathTextBox.Text = dlg.FileName;
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
                string path = FFmpegPathTextBox.Text;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    MessageBox.Show("Please select a valid FFmpeg executable.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                EnsureSettingsFolder();
                string[] lines = File.Exists(configFile) ? File.ReadAllLines(configFile) : Array.Empty<string>();
                lines = lines.Where(l => l != null && !l.StartsWith("FFmpegPath=")).Append($"FFmpegPath={path}").ToArray();
                File.WriteAllLines(configFile, lines);

                MessageBox.Show("FFmpeg path saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"Error in SaveFFmpegButton_Click: {ex.Message}");
                MessageBox.Show($"Error saving FFmpeg path: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- Utilities ----------

        private void EnsureSettingsFolder()
        {
            try
            {
                if (!Directory.Exists(settingsFolder))
                    Directory.CreateDirectory(settingsFolder);
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
