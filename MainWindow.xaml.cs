using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MooseTracks
{
    public partial class MainWindow : Window
    {
        private readonly string settingsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
        private readonly string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "settings.cfg");
        private readonly string debugLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "debug.log");

        public MainWindow()
        {
            InitializeComponent(); // XAML now resolves dynamic resources correctly
            EnsureSettingsFolder(); // Ensure settings folder exists
            ShowStartupPageFromConfig();
        }


        #region Logging & Setup
        private void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(debugLog));
                File.AppendAllText(debugLog, $"{DateTime.Now}: {message}\n");
            }
            catch { /* Suppress logging errors */ }
        }

        private void ShowStartupPageFromConfig()
        {
            try
            {
                string value = "Extraction";
                if (File.Exists(configFile))
                {
                    var line = File.ReadLines(configFile)
                                   .FirstOrDefault(l => l.StartsWith("StartupPage=", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(line))
                        value = line.Substring("StartupPage=".Length).Trim();
                }

                Log($"StartupPage from config: '{value}'");

                switch ((value ?? "Extraction").ToLowerInvariant())
                {
                    case "container":
                        ShowContainerPage();
                        HighlightButton(ContainerButton);
                        break;

                    case "transcoding":
                        ShowTranscodingPage();
                        HighlightButton(TranscodingButton);
                        break;

                    case "settings":
                        ShowSettingsPage();
                        HighlightButton(SettingsButton);
                        break;

                    case "extraction":
                    default:
                        ShowExtractionPage();
                        HighlightButton(ExtractionButton);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"ShowStartupPageFromConfig error: {ex.Message}");
                ShowExtractionPage();
                HighlightButton(ExtractionButton);
            }
        }



        private void EnsureSettingsFolder()
        {
            try
            {
                if (!Directory.Exists(settingsFolder))
                    Directory.CreateDirectory(settingsFolder);
            }
            catch (Exception ex)
            {
                Log($"Error in EnsureSettingsFolder: {ex.Message}");
            }
        }

        private void ResetButtonStyles()
        {
            try
            {
                if (ContainerButton != null) ContainerButton.Style = (Style)FindResource("AppButtonStyle");
                if (ExtractionButton != null) ExtractionButton.Style = (Style)FindResource("AppButtonStyle");
                if (TranscodingButton != null) TranscodingButton.Style = (Style)FindResource("AppButtonStyle");
                if (ThemeButton != null) ThemeButton.Style = (Style)FindResource("AppButtonStyle");
                if (SettingsButton != null) SettingsButton.Style = (Style)FindResource("AppButtonStyle");
                if (AboutButton != null) AboutButton.Style = (Style)FindResource("AppButtonStyle");
            }
            catch (Exception ex)
            {
                Log($"Error in ResetButtonStyles: {ex.Message}");
            }
        }

        private void HighlightButton(Button btn)
        {
            try
            {
                ResetButtonStyles();
                if (btn != null && btn != AboutButton)
                    btn.Style = (Style)FindResource("SelectedButtonStyle");
            }
            catch (Exception ex)
            {
                Log($"Error in HighlightButton: {ex.Message}");
            }
        }

        private void ContainerButton_Click(object sender, RoutedEventArgs e)
        {
            ShowContainerPage();
            HighlightButton(ContainerButton);
        }

        private void ExtractionButton_Click(object sender, RoutedEventArgs e)
        {
            ShowExtractionPage();
            HighlightButton(ExtractionButton);
        }

        private void TranscodingButton_Click(object sender, RoutedEventArgs e)
        {
            ShowTranscodingPage();
            HighlightButton(TranscodingButton);
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            ShowThemePage();
            HighlightButton(ThemeButton);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsPage();
            HighlightButton(SettingsButton);
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "MooseTracks v0.94\n\nDeveloped by Syncromoose\n\nAll rights reserved.\n\n" +
                "Special Thanks to\nOberje of The Fingerbobs,\nFilm X Desire,\nGetToTheChopper",
                "About MooseTracks",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            HighlightButton(AboutButton);
        }
        #endregion

        #region Page Navigation
        private void ShowContainerPage()
        {
            if (MainContentControl != null)
                MainContentControl.Content = new Views.ContainerPage();
        }

        private void ShowExtractionPage()
        {
            if (MainContentControl != null)
                MainContentControl.Content = new Views.ExtractionPage();
        }

        private void ShowTranscodingPage()
        {
            if (MainContentControl != null)
                MainContentControl.Content = new Views.TranscodingPage();
        }

        private void ShowThemePage()
        {
            if (MainContentControl != null)
                MainContentControl.Content = new Views.Theme();
        }

        private void ShowSettingsPage()
        {
            if (MainContentControl != null)
                MainContentControl.Content = new Views.SettingsPage();
        }
        #endregion
    }
}
