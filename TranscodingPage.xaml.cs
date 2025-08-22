using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

namespace MooseTracks.Views
{
    public partial class TranscodingPage : UserControl
    {
        private readonly FFmpegRunner _ffmpegRunner;

        // NEW: track output path behavior
        private bool _outputManuallySet = false;
        private bool _settingOutputProgrammatically = false;

        public TranscodingPage()
        {
            InitializeComponent();

            // Initialize FFmpegRunner with path resolution
            string ffmpegPath = ResolveFFmpegPath();
            _ffmpegRunner = new FFmpegRunner(ffmpegPath);

            // Prepare UI lists
            EnsureFormatOptions();
            EnsureFpsOptions();
            EnsureBitrateOptions();

            // Drag & Drop for input file path box
            InputFilePathBox.AllowDrop = true;
            InputFilePathBox.PreviewDragOver += (s, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
            InputFilePathBox.Drop += InputFilePathBox_Drop;

            // NEW: detect manual edits to output path so we don't override them
            OutputPathBox.TextChanged += (s, e) =>
            {
                if (_settingOutputProgrammatically) return;
                _outputManuallySet = !string.IsNullOrWhiteSpace(OutputPathBox.Text);
            };

            // Default disabled FPS controls
            SetFpsControlsEnabled(false);
        }

        private string ResolveFFmpegPath()
        {
            // Try system PATH first
            string ffmpegPath = "ffmpeg";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                    if (proc.ExitCode == 0)
                        return ffmpegPath;
                }
            }
            catch
            {
                // FFmpeg not in PATH
            }

            // Fallback to local folder
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "bin", "ffmpeg.exe");
            if (File.Exists(localPath))
                return localPath;

            throw new FileNotFoundException("FFmpeg executable not found in PATH or local folder.");
        }

        // NEW: helper to default output to the input file's folder (unless user chose one)
        private void SetDefaultOutputFromInput(string inputPath)
        {
            if (_outputManuallySet) return;

            var dir = Path.GetDirectoryName(inputPath);
            if (string.IsNullOrWhiteSpace(dir)) return;

            _settingOutputProgrammatically = true;
            OutputPathBox.Text = dir;
            _settingOutputProgrammatically = false;
        }

        // =========================
        // Browse / Drag & Drop
        // =========================

        private void BrowseInputButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Media files|*.mkv;*.mp4;*.avi;*.mov;*.ts;*.m2ts;*.mp3;*.flac;*.aac;*.wav;*.w64;*.ogg;*.opus|All files|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                InputFilePathBox.Text = dlg.FileName;

                // NEW: default output to input folder if user hasn't chosen otherwise
                SetDefaultOutputFromInput(dlg.FileName);

                LoadFileInfoAndStreams(dlg.FileName);
            }
        }

        private void InputFilePathBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                InputFilePathBox.Text = files[0];

                // NEW: default output to input folder if user hasn't chosen otherwise
                SetDefaultOutputFromInput(files[0]);

                LoadFileInfoAndStreams(files[0]);
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

        // =========================
        // FPS Controls
        // =========================

        private void EnableFrameRateChangeCheckBox_Checked(object sender, RoutedEventArgs e) => SetFpsControlsEnabled(true);
        private void EnableFrameRateChangeCheckBox_Unchecked(object sender, RoutedEventArgs e) => SetFpsControlsEnabled(false);

        private void SetFpsControlsEnabled(bool enabled)
        {
            PreservePitchCheckbox.IsEnabled = enabled;
            SourceFrameRateComboBox.IsEnabled = enabled;
            TargetFrameRateComboBox.IsEnabled = enabled;
        }

        private void EnsureFpsOptions()
        {
            var wanted = new[] { "23.976", "24", "25", "29.976", "30" };
            void ResetCombo(ComboBox cb)
            {
                cb.Items.Clear();
                foreach (var s in wanted)
                    cb.Items.Add(new ComboBoxItem { Content = s });
                cb.SelectedIndex = 0;
            }
            ResetCombo(SourceFrameRateComboBox);
            ResetCombo(TargetFrameRateComboBox);
        }

        private void EnsureFormatOptions()
        {
            var formats = new[] { "wav", "wav64", "aac", "ac3", "dts", "eac3", "flac", "ogg", "opus", "mp3" };
            FormatComboBox.Items.Clear();
            foreach (var f in formats)
                FormatComboBox.Items.Add(new ComboBoxItem { Content = f });
            FormatComboBox.SelectedIndex = 0;
            FormatComboBox.SelectionChanged += FormatComboBox_SelectionChanged;
        }

        private void EnsureBitrateOptions()
        {
            UpdateBitrateOptions((FormatComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLowerInvariant());
        }

        private void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var format = (FormatComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLowerInvariant();
            UpdateBitrateOptions(format);
        }

        private void UpdateBitrateOptions(string format)
        {
            BitrateComboBox.Items.Clear();
            var bitrates = GetBitrateOptionsForFormat(format);
            foreach (var bitrate in bitrates)
                BitrateComboBox.Items.Add(new ComboBoxItem { Content = bitrate });
            BitrateComboBox.SelectedIndex = bitrates.Length > 0 ? 0 : -1;
        }

        private string[] GetBitrateOptionsForFormat(string format)
        {
            switch (format)
            {
                case "aac":
                    return new[] { "128k", "192k", "256k", "320k" };
                case "ac3":
                    return new[] { "192k", "384k", "640k" };
                case "eac3":
                    return new[] { "192k", "384k", "768k" };
                case "dts":
                    return new[] { "768k", "1536k" };
                case "opus":
                    return new[] { "96k", "128k", "160k", "192k" };
                case "mp3":
                    return new[] { "128k", "192k", "256k", "320k" };
                case "wav":
                case "wav64":
                case "flac":
                case "ogg":
                    return new string[0];
                default:
                    return new string[0];
            }
        }

        // =========================
        // File Info + Audio Streams
        // =========================

        private async void LoadFileInfoAndStreams(string filePath)
        {
            try
            {
                // NEW: ensure default output path is set (unless user picked a custom one)
                SetDefaultOutputFromInput(filePath);

                var (doc, _) = await RunFfprobeAsync(filePath);
                FileNameText.Text = Path.GetFileName(filePath);
                SizeText.Text = (new FileInfo(filePath).Length / (1024.0 * 1024.0)).ToString("F2") + " MB";

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

                ExtractionOutputBox.Items.Clear();
                if (doc.RootElement.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
                {
                    foreach (var (stream, idx) in streams.EnumerateArray().Select((s, i) => (s, i)))
                    {
                        var type = stream.TryGetProperty("codec_type", out var codecType) ? codecType.GetString() ?? "unknown" : "unknown";
                        if (!string.Equals(type, "audio", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string codec = stream.TryGetProperty("codec_name", out var codecName) ? codecName.GetString() ?? "unknown" : "unknown";
                        string desc = $"[{idx}] AUDIO - {codec}";
                        if (stream.TryGetProperty("channels", out var ch))
                            desc += $" ({ch} ch)";
                        if (stream.TryGetProperty("sample_rate", out var sr))
                            desc += $" {sr.GetString()} Hz";

                        ExtractionOutputBox.Items.Add(new AudioStreamItem
                        {
                            Index = idx,
                            Codec = codec,
                            Description = desc
                        });
                    }
                }

                var first = ExtractionOutputBox.Items.OfType<AudioStreamItem>().FirstOrDefault();
                if (first != null)
                {
                    CodecText.Text = first.Codec;
                    ChannelsText.Text = ParseInBrackets(first.Description, "ch") ?? "Unknown";
                    SampleRateText.Text = ParseSampleRate(first.Description) ?? "Unknown";
                }
                else
                {
                    CodecText.Text = "No audio";
                    ChannelsText.Text = "-";
                    SampleRateText.Text = "-";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading file info: " + ex.Message);
            }
        }

        private static string ParseInBrackets(string s, string suffix)
        {
            var start = s.IndexOf('(');
            var end = s.IndexOf(')');
            if (start >= 0 && end > start)
            {
                var content = s.Substring(start + 1, end - start - 1);
                if (content.Trim().EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return content.Replace(suffix, "", StringComparison.OrdinalIgnoreCase).Trim();
                }
            }
            return null;
        }

        private static string ParseSampleRate(string s)
        {
            var hzPos = s.IndexOf("Hz", StringComparison.OrdinalIgnoreCase);
            if (hzPos > 0)
            {
                var part = s.Substring(0, hzPos).Trim();
                var lastSpace = part.LastIndexOf(' ');
                if (lastSpace >= 0)
                    return part.Substring(lastSpace + 1) + " Hz";
            }
            return null;
        }

        private async Task<(JsonDocument doc, string raw)> RunFfprobeAsync(string filePath)
        {
            var ffprobePath = ResolveFFmpegPath().Replace("ffmpeg", "ffprobe"); // Use same path logic for ffprobe
            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string output;
            using (var proc = Process.Start(psi))
            {
                output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();
            }

            var doc = JsonDocument.Parse(output);
            return (doc, output);
        }

        private async Task<TimeSpan> ProbeDurationAsync(string filePath)
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
            catch { }
            return TimeSpan.Zero;
        }

        // =========================
        // Transcoding (audio only)
        // =========================

        private async void TranscodeTracksButton_Click(object sender, RoutedEventArgs e)
        {
            string input = InputFilePathBox.Text;
            string outputDir = OutputPathBox.Text;

            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Please choose an input file.");
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
            try { Directory.CreateDirectory(outputDir); } catch { /* ignore */ }

            var selectedStreams = ExtractionOutputBox.SelectedItems.Cast<AudioStreamItem>().ToList();
            if (selectedStreams.Count == 0)
            {
                MessageBox.Show("Please select at least one audio stream from the list.");
                return;
            }

            var selectedFormat = (FormatComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(selectedFormat))
            {
                MessageBox.Show("Please select an output format.");
                return;
            }

            string selectedBitrate = (BitrateComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

            bool fpsEnabled = EnableFrameRateChangeCheckBox.IsChecked == true;
            bool preservePitch = PreservePitchCheckbox.IsChecked == true;
            double tempoFactor = 1.0;

            if (fpsEnabled)
            {
                if (!TryGetFps(SourceFrameRateComboBox, out var srcFps) ||
                    !TryGetFps(TargetFrameRateComboBox, out var dstFps))
                {
                    MessageBox.Show("Please choose valid source/target FPS.");
                    return;
                }
                tempoFactor = dstFps / srcFps;
            }

            var totalStreams = selectedStreams.Count;
            int completedStreams = 0;

            FFmpegOutputBox.Clear();
            FFmpegProgressBar.Value = 0;

            var inputBase = Path.GetFileNameWithoutExtension(input);
            var totalDuration = await ProbeDurationAsync(input);
            if (totalDuration.TotalSeconds <= 0) totalDuration = TimeSpan.FromSeconds(1);

            foreach (var stream in selectedStreams)
            {
                var (ext, codecArgs, containerArgs) = CodecArgsForFormat(selectedFormat, selectedBitrate);
                var safeBase = MakeSafeFileName(inputBase);
                var outPath = Path.Combine(outputDir, $"{safeBase}_a{stream.Index}.{ext}");

                var filter = BuildAudioFilterChain(tempoFactor, preservePitch, GainBox.Text, DownmixCheckBox.IsChecked == true);

                var args = new List<string>
                {
                    "-y",
                    $"-i \"{input}\"",
                    $"-map 0:{stream.Index}"
                };

                if (!string.IsNullOrEmpty(filter))
                    args.Add($"-af \"{filter}\"");

                if (!string.IsNullOrEmpty(codecArgs))
                    args.Add(codecArgs);

                if (DownmixCheckBox.IsChecked == true)
                    args.Add("-ac 2");

                if (!string.IsNullOrEmpty(containerArgs))
                    args.Add(containerArgs);

                args.Add($"\"{outPath}\"");

                // Define handlers as local variables to allow unsubscribing
                Action<double> progressHandler = pct =>
                {
                    var overall = ((completedStreams + (pct / 100.0)) / totalStreams) * 100.0;
                    Dispatcher.Invoke(() => FFmpegProgressBar.Value = overall);
                };

                Action<string> logHandler = line =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        FFmpegOutputBox.AppendText(line + Environment.NewLine);
                        FFmpegOutputBox.ScrollToEnd();
                    });
                };

                Action completedHandler = () =>
                {
                    completedStreams++;
                    var overall = (completedStreams / (double)totalStreams) * 100.0;
                    Dispatcher.Invoke(() =>
                    {
                        FFmpegProgressBar.Value = overall;
                        FFmpegOutputBox.AppendText($"✅ Finished stream {stream.Index} -> {Path.GetFileName(outPath)}{Environment.NewLine}");
                    });
                };

                // Subscribe
                _ffmpegRunner.ProgressChanged += progressHandler;
                _ffmpegRunner.LogReceived += logHandler;
                _ffmpegRunner.Completed += completedHandler;

                try
                {
                    await _ffmpegRunner.RunAsync(string.Join(" ", args), totalDuration);
                }
                finally
                {
                    // Unsubscribe to prevent handler accumulation
                    _ffmpegRunner.ProgressChanged -= progressHandler;
                    _ffmpegRunner.LogReceived -= logHandler;
                    _ffmpegRunner.Completed -= completedHandler;
                }
            }

            Dispatcher.Invoke(() =>
            {
                FFmpegProgressBar.Value = 100;
                FFmpegOutputBox.AppendText("🎉 All selected streams finished." + Environment.NewLine);
            });
        }

        private static string BuildAudioFilterChain(double tempoFactor, bool preservePitch, string gainText, bool downmix)
        {
            var filters = new List<string>();

            if (Math.Abs(tempoFactor - 1.0) > 0.0001)
            {
                if (preservePitch)
                {
                    foreach (var f in SplitAtempoChain(tempoFactor))
                        filters.Add($"atempo={f.ToString("0.########", CultureInfo.InvariantCulture)}");
                }
                else
                {
                    filters.Add($"asetrate=sample_rate*{tempoFactor.ToString("0.########", CultureInfo.InvariantCulture)}");
                    filters.Add("aresample=sample_rate");
                }
            }

            if (!string.IsNullOrWhiteSpace(gainText) &&
                double.TryParse(gainText, NumberStyles.Any, CultureInfo.InvariantCulture, out double gainDb) &&
                Math.Abs(gainDb) > 0.0001)
            {
                filters.Add($"volume={gainDb.ToString("0.###", CultureInfo.InvariantCulture)}dB");
            }

            return string.Join(",", filters);
        }

        private static IEnumerable<double> SplitAtempoChain(double factor)
        {
            var remaining = factor;
            var result = new List<double>();
            if (remaining <= 0) remaining = 1.0;

            if (remaining >= 1.0)
            {
                while (remaining > 2.0)
                {
                    result.Add(2.0);
                    remaining /= 2.0;
                }
                result.Add(remaining);
            }
            else
            {
                while (remaining < 0.5)
                {
                    result.Add(0.5);
                    remaining /= 0.5;
                }
                result.Add(remaining);
            }

            return result;
        }

        private static (string ext, string codecArgs, string containerArgs) CodecArgsForFormat(string format, string bitrate)
        {
            switch (format)
            {
                case "wav":
                    return ("wav", "-c:a pcm_s16le", "");
                case "wav64":
                    return ("w64", "-c:a pcm_s16le", "-f w64");
                case "aac":
                    return ("m4a", $"-c:a aac -b:a {bitrate ?? "192k"}", "");
                case "ac3":
                    return ("ac3", $"-c:a ac3 -b:a {bitrate ?? "640k"}", "");
                case "dts":
                    return ("dts", $"-c:a dca -b:a {bitrate ?? "1536k"} -strict -2", "");
                case "eac3":
                    return ("eac3", $"-c:a eac3 -b:a {bitrate ?? "768k"}", "");
                case "flac":
                    return ("flac", "-c:a flac", "");
                case "ogg":
                    return ("ogg", "-c:a libvorbis -q:a 5", "");
                case "opus":
                    return ("opus", $"-c:a libopus -b:a {bitrate ?? "160k"}", "");
                case "mp3":
                    return ("mp3", $"-c:a libmp3lame -b:a {bitrate ?? "192k"}", "");
                default:
                    return ("wav", "-c:a pcm_s16le", "");
            }
        }

        private static bool TryGetFps(ComboBox combo, out double fps)
        {
            fps = 0;
            var s = (combo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrWhiteSpace(s)) return false;
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out fps);
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private void ExtractionOutputBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private class AudioStreamItem
        {
            public int Index { get; set; }
            public string Codec { get; set; }
            public string Description { get; set; }
            public override string ToString() => Description;
        }
    }
}
