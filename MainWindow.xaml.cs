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
            ShowExtractionPage();
            HighlightButton(ExtractionButton);
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

        private void ApplyThemeToWindow()
        {
            try
            {
                Log("Entering ApplyThemeToWindow");

                // Apply to MainWindow
                if (Application.Current.Resources["AppBackground"] is Brush bg)
                    this.Background = bg;
                else
                    this.Background = new SolidColorBrush(Colors.White); // Fallback

                if (Application.Current.Resources["AppForeground"] is Brush fg)
                    this.Foreground = fg;
                else
                    this.Foreground = new SolidColorBrush(Colors.Black); // Fallback

                // Apply to MainContentControl
                if (MainContentControl != null)
                {
                    if (Application.Current.Resources["AppBackground"] is Brush contentBg)
                        MainContentControl.Background = contentBg;
                    else
                        MainContentControl.Background = new SolidColorBrush(Colors.White); // Fallback
                }

                // Optionally refresh child controls (e.g., pages)
                if (MainContentControl?.Content is FrameworkElement page)
                {
                    page.Resources.MergedDictionaries.Add(Application.Current.Resources);
                }

                Log("Exiting ApplyThemeToWindow");
            }
            catch (Exception ex)
            {
                Log($"Error in ApplyThemeToWindow: {ex.Message}");
            }
        }
        #endregion

        #region Button Highlighting
        private void ResetButtonStyles()
        {
            try
            {
                if (ExtractionButton != null) ExtractionButton.Style = (Style)FindResource("AppButtonStyle");
                if (TranscodingButton != null) TranscodingButton.Style = (Style)FindResource("AppButtonStyle");
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
        #endregion

        #region Button Click Handlers
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

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsPage();
            HighlightButton(SettingsButton);
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "MooseTracks v0.8\n\nDeveloped by Syncromoose\n\nAll rights reserved.\n\n" +
                "Special Thanks to\nOberje of The Fingerbobs,\nFilm X Desire,\nGetToTheChopper",
                "About MooseTracks",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            HighlightButton(AboutButton);
        }
        #endregion

        #region Page Navigation
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

        private void ShowSettingsPage()
        {
            if (MainContentControl != null)
                MainContentControl.Content = new Views.SettingsPage();
        }
        #endregion
    }
}
