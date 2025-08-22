using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MooseTracks.Views
{
    public partial class ExtractionPage : UserControl
    {
        // NEW: track output path behavior
        private bool _outputManuallySet = false;
        private bool _settingOutputProgrammatically = false;

        public ExtractionPage()
        {
            InitializeComponent();

            // Enable drag + drop for input
            InputFilePathBox.AllowDrop = true;
            InputFilePathBox.PreviewDragOver += InputFilePathBox_PreviewDragOver;
            InputFilePathBox.Drop += InputFilePathBox_Drop;

            // NEW: mark when user sets output manually (so we don't override)
            OutputPathBox.TextChanged += (s, e) =>
            {
                if (_settingOutputProgrammatically) return;
                _outputManuallySet = !string.IsNullOrWhiteSpace(OutputPathBox.Text);
            };
        }

        // NEW: helper to set default output to the input file's folder (unless user chose one)
        private void SetDefaultOutputFromInput(string inputPath)
        {
            if (_outputManuallySet) return;

            var dir = Path.GetDirectoryName(inputPath);
            if (string.IsNullOrWhiteSpace(dir)) return;

            _settingOutputProgrammatically = true;
            OutputPathBox.Text = dir;
            _settingOutputProgrammatically = false;
        }

        #region Browse Buttons
        private void BrowseInputButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Media files|*.mkv;*.mp4;*.avi;*.mov;*.ts;*.m2ts;*.mp3;*.flac;*.aac|All files|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                InputFilePathBox.Text = dlg.FileName;

                // NEW: default output to input folder if user hasn't chosen otherwise
                SetDefaultOutputFromInput(dlg.FileName);

                LoadFileInfo(dlg.FileName);
            }
        }

        private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();

            // NEW: start in current output (if valid) else the input's folder, else Documents
            var inputDir = Path.GetDirectoryName(InputFilePathBox.Text);
            dlg.SelectedPath = Directory.Exists(OutputPathBox.Text)
                ? OutputPathBox.Text
                : (Directory.Exists(inputDir)
                    ? inputDir
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _settingOutputProgrammatically = true;
                OutputPathBox.Text = dlg.SelectedPath;
                _settingOutputProgrammatically = false;

                _outputManuallySet = true; // user explicitly chose a folder
            }
        }
        #endregion

        #region Drag and Drop
        private void InputFilePathBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void InputFilePathBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    InputFilePathBox.Text = files[0];

                    // NEW: default output to input folder if user hasn't chosen otherwise
                    SetDefaultOutputFromInput(files[0]);

                    LoadFileInfo(files[0]);
                }
            }
        }
        #endregion

        #region File Info + Stream Listing
        private async void LoadFileInfo(string filePath)
        {
            try
            {
                // NEW: ensure default output path is set (unless user picked a custom one)
                SetDefaultOutputFromInput(filePath);

                var (doc, rawJson) = await RunFfprobeAsync(filePath);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("ffprobe returned no data.");

                // General file info
                FileNameText.Text = Path.GetFileName(filePath);

                if (doc.RootElement.TryGetProperty("format", out var format))
                {
                    BitrateText.Text = format.TryGetProperty("bit_rate", out var br) ? br.GetString() : "Unknown";

                    if (format.TryGetProperty("duration", out var durStr) &&
                        double.TryParse(durStr.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dur))
                    {
                        DurationText.Text = TimeSpan.FromSeconds(dur).ToString(@"hh\:mm\:ss");
                    }
                    else
                    {
                        DurationText.Text = "Unknown";
                    }
                }
                else
                {
                    BitrateText.Text = "Unknown";
                    DurationText.Text = "Unknown";
                }

                SizeText.Text = (new FileInfo(filePath).Length / (1024.0 * 1024.0)).ToString("F2") + " MB";

                // Clear and repopulate stream list
                ExtractionOutputBox.Items.Clear();

                int index = 0;
                if (doc.RootElement.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
                {
                    foreach (var stream in streams.EnumerateArray())
                    {
                        string type = stream.TryGetProperty("codec_type", out var codecType) ? codecType.GetString() ?? "unknown" : "unknown";
                        string codec = stream.TryGetProperty("codec_name", out var codecName) ? codecName.GetString() ?? "unknown" : "unknown";

                        string desc = $"[{index}] {type.ToUpper()} - {codec}";

                        if (type == "audio")
                        {
                            if (stream.TryGetProperty("channels", out var ch))
                                desc += $" ({ch} ch)";
                            if (stream.TryGetProperty("sample_rate", out var sr))
                                desc += $" {sr.GetString()} Hz";
                        }
                        else if (type == "video")
                        {
                            if (stream.TryGetProperty("width", out var w) &&
                                stream.TryGetProperty("height", out var h))
                                desc += $" {w.GetInt32()}x{h.GetInt32()}";
                            if (stream.TryGetProperty("avg_frame_rate", out var fr) && fr.ValueKind == JsonValueKind.String)
                                desc += $" {fr.GetString()}";
                        }
                        else if (type == "subtitle")
                        {
                            if (stream.TryGetProperty("tags", out var tags) &&
                                tags.ValueKind == JsonValueKind.Object &&
                                tags.TryGetProperty("language", out var lang))
                                desc += $" [{lang.GetString()}]";
                        }

                        ExtractionOutputBox.Items.Add(new StreamItem
                        {
                            Index = index,
                            Type = type,
                            Codec = codec,
                            Description = desc
                        });

                        index++;
                    }
                }

                // Show first audio codec (if any)
                var firstAudio = ExtractionOutputBox.Items.OfType<StreamItem>().FirstOrDefault(i => i.Type == "audio");
                CodecText.Text = firstAudio?.Codec ?? "No audio";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading file info: " + ex.Message);
            }
        }

        private static async Task<(JsonDocument doc, string raw)> RunFfprobeAsync(string filePath)
        {
            var ffprobePath = ResolveToolPath("ffprobe");

            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string output;
            using (var process = Process.Start(psi))
            {
                output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
            }

            var doc = JsonDocument.Parse(output);
            return (doc, output);
        }

        private static async Task<TimeSpan> GetDurationAsync(string filePath)
        {
            try
            {
                var (doc, _) = await RunFfprobeAsync(filePath);
                if (doc.RootElement.TryGetProperty("format", out var format) &&
                    format.TryGetProperty("duration", out var durStr) &&
                    double.TryParse(durStr.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dur))
                {
                    return TimeSpan.FromSeconds(dur);
                }
            }
            catch { /* ignore */ }
            return TimeSpan.Zero;
        }
        #endregion

        #region Extraction (stream copy, correct containers, proper progress)
        private async void ExtractTracksButton_Click(object sender, RoutedEventArgs e)
        {
            string input = InputFilePathBox.Text;
            string outputDir = OutputPathBox.Text;

            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Please select an input file.");
                return;
            }

            // NEW: safety net — default output to input folder if empty
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                outputDir = Path.GetDirectoryName(input) ?? Environment.CurrentDirectory;
                _settingOutputProgrammatically = true;
                OutputPathBox.Text = outputDir;
                _settingOutputProgrammatically = false;
            }

            // Ensure the directory exists
            try { Directory.CreateDirectory(outputDir); } catch { /* ignore, ffmpeg may still handle */ }

            var selected = ExtractionOutputBox.SelectedItems.Cast<StreamItem>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one stream to extract.");
                return;
            }

            var totalDuration = await GetDurationAsync(input);
            if (totalDuration.TotalSeconds <= 0)
            {
                TimeSpan.TryParse(DurationText.Text, out totalDuration);
            }
            if (totalDuration.TotalSeconds <= 0)
            {
                totalDuration = TimeSpan.FromSeconds(1);
            }

            var baseName = Path.GetFileNameWithoutExtension(input);
            var sb = new StringBuilder();
            sb.Append("-y ");
            sb.Append($"-i \"{input}\" ");

            foreach (var s in selected)
            {
                var (ext, extraFlags) = DetermineOutputForStream(s.Type, s.Codec);
                var safeBase = MakeSafeFileName(baseName);
                var outPath = Path.Combine(outputDir, $"{safeBase}_stream_{s.Index}.{ext}");

                sb.Append($"-map 0:{s.Index} -c copy ");
                if (!string.IsNullOrEmpty(extraFlags))
                    sb.Append($"{extraFlags} ");
                sb.Append($"\"{outPath}\" ");
            }

            var args = sb.ToString();

            var runner = new FFmpegRunner(ResolveToolPath("ffmpeg"));

            runner.ProgressChanged += progress =>
            {
                Dispatcher.Invoke(() => FFmpegProgressBar.Value = progress);
            };

            runner.LogReceived += line =>
            {
                Dispatcher.Invoke(() =>
                {
                    FFmpegOutputBox.AppendText(line + Environment.NewLine);
                    FFmpegOutputBox.ScrollToEnd();
                });
            };

            runner.Completed += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    FFmpegProgressBar.Value = 100;
                    FFmpegOutputBox.AppendText("✅ Extraction complete" + Environment.NewLine);
                });
            };

            FFmpegProgressBar.Value = 0;
            FFmpegOutputBox.Clear();

            await runner.RunAsync(args, totalDuration);
        }

        private static (string ext, string extraFlags) DetermineOutputForStream(string type, string codec)
        {
            codec = (codec ?? "").ToLowerInvariant();
            type = (type ?? "").ToLowerInvariant();

            if (type == "audio")
            {
                switch (codec)
                {
                    case "aac": return ("m4a", "");
                    case "mp3": return ("mp3", "");
                    case "flac": return ("flac", "");
                    case "opus": return ("opus", "");
                    case "ac3": return ("ac3", "");
                    case "eac3": return ("eac3", "");
                    case "dts": return ("dts", "");
                    case "truehd": return ("thd", "");
                    default: return ("mka", "");
                }
            }
            else if (type == "video")
            {
                return ("mkv", "");
            }
            else if (type == "subtitle")
            {
                switch (codec)
                {
                    case "subrip": return ("srt", "");
                    case "ass":
                    case "ssa": return ("ass", "");
                    case "hdmv_pgs_subtitle": return ("sup", "");
                    default: return ("mks", "");
                }
            }

            return ("bin", "");
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
        #endregion

        private void ExtractionOutputBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        #region Helpers
        private static string ResolveToolPath(string toolName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = toolName,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    process.WaitForExit(2000);
                    if (process.ExitCode == 0)
                        return toolName; // Found in PATH
                }
            }
            catch
            {
                // ignored
            }

            var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", toolName + (OperatingSystem.IsWindows() ? ".exe" : ""));
            if (File.Exists(exePath))
                return exePath;

            throw new FileNotFoundException($"{toolName} not found in PATH or local ffmpeg folder.");
        }
        #endregion
    }

    public class StreamItem
    {
        public int Index { get; set; }
        public string Type { get; set; } = "";
        public string Codec { get; set; } = "";
        public string Description { get; set; } = "";

        public override string ToString() => Description;
    }
}
