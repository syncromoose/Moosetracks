using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel; // INotifyPropertyChanged
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
    public partial class ContainerPage : UserControl
    {
        // Track output path behavior
        private bool _outputManuallySet = false;
        private bool _settingOutputProgrammatically = false;

        // Input file list for ffmpeg (-i order). Index 0 is the main input.
        private readonly List<string> _inputFiles = new();

        public ContainerPage()
        {
            InitializeComponent();

            // Drag & drop for the input path box (single file)
            InputFilePathBox.AllowDrop = true;
            InputFilePathBox.PreviewDragOver += InputFilePathBox_PreviewDragOver;
            InputFilePathBox.Drop += InputFilePathBox_Drop;

            // Mark when user sets output manually (so we don't override)
            OutputPathBox.TextChanged += (s, e) =>
            {
                if (_settingOutputProgrammatically) return;
                _outputManuallySet = !string.IsNullOrWhiteSpace(OutputPathBox.Text);
            };

            // Track list drag & drop (external files to add to container)
            ExtractionOutputBox.AllowDrop = true;
            ExtractionOutputBox.PreviewDragOver += TracksList_PreviewDragOver;
            ExtractionOutputBox.Drop += TracksList_Drop;
        }

        // Helper: default output to the input file's folder (unless user chose one)
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
                Filter = "Media files|*.mkv;*.mp4;*.mov;*.avi;*.ts;*.m2ts;*.mp3;*.flac;*.aac;*.wav;*.w64|All files|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                InputFilePathBox.Text = dlg.FileName;

                // Default output to input folder if user hasn't chosen otherwise
                SetDefaultOutputFromInput(dlg.FileName);

                _ = LoadPrimaryFileAsync(dlg.FileName);
            }
        }

        private void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();

            // Start in current output (if valid) else the input's folder, else Documents
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

        #region Drag and Drop (Input box)
        private void InputFilePathBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void InputFilePathBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                InputFilePathBox.Text = files[0];

                // Default output to input folder if user hasn't chosen otherwise
                SetDefaultOutputFromInput(files[0]);

                _ = LoadPrimaryFileAsync(files[0]);
            }
        }
        #endregion

        #region Drag and Drop (Track list: add external files)
        private void TracksList_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private async void TracksList_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

                var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (dropped == null || dropped.Length == 0) return;

                // keep only real files, remove duplicates
                var files = dropped
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (files.Count == 0) return;

                e.Handled = true;

                if (_inputFiles.Count == 0)
                {
                    // No primary yet: first becomes PRIMARY, rest become EXTERNAL inputs.
                    var first = files[0];
                    InputFilePathBox.Text = first;
                    await LoadPrimaryFileAsync(first);

                    for (int i = 1; i < files.Count; i++)
                    {
                        var p = files[i];
                        if (!_inputFiles.Any(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase)))
                            await AddExternalInputAsync(p);
                    }
                }
                else
                {
                    // Primary already set: ALL dropped files become EXTERNAL inputs (no replacement).
                    foreach (var p in files)
                    {
                        if (_inputFiles.Any(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase)))
                            continue; // skip duplicates (including the existing primary)
                        await AddExternalInputAsync(p);
                    }
                }

                // Do NOT SelectAll here — checkboxes (IsIncluded) are already default true for new items.
                // Selection is reserved for applying per-track options.
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error adding file(s): " + ex.Message);
            }
        }


        #endregion

        #region Load file info + stream listing
        // Load the primary (main) file and reset state
        private async Task LoadPrimaryFileAsync(string filePath)
        {
            SetDefaultOutputFromInput(filePath);

            _inputFiles.Clear();
            _inputFiles.Add(filePath);

            ExtractionOutputBox.Items.Clear();

            await LoadFileInfoAsync(filePath, inputIndex: 0);

            // Keep all items included by default
            foreach (TrackItem ti in ExtractionOutputBox.Items)
                ti.IsIncluded = true;
        }

        // Load/append an external file (appears after the main input)
        private async Task AddExternalInputAsync(string filePath)
        {
            var inputIndex = _inputFiles.Count;
            _inputFiles.Add(filePath);

            await LoadFileInfoAsync(filePath, inputIndex);

            foreach (TrackItem ti in ExtractionOutputBox.Items)
                ti.IsIncluded = true;
        }

        private static string GetLanguageFromTags(JsonElement stream)
        {
            try
            {
                if (stream.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
                {
                    if (tags.TryGetProperty("language", out var lang) && lang.ValueKind == JsonValueKind.String)
                        return (lang.GetString() ?? "").Trim();
                    if (tags.TryGetProperty("LANGUAGE", out var lang2) && lang2.ValueKind == JsonValueKind.String)
                        return (lang2.GetString() ?? "").Trim();
                }
            }
            catch { /* ignore */ }
            return "";
        }

        private static bool GetDispositionFlag(JsonElement stream, string name)
        {
            try
            {
                if (stream.TryGetProperty("disposition", out var disp) && disp.ValueKind == JsonValueKind.Object)
                {
                    if (disp.TryGetProperty(name, out var flag))
                    {
                        if (flag.ValueKind == JsonValueKind.Number) return flag.GetInt32() == 1;
                        if (flag.ValueKind == JsonValueKind.String && int.TryParse(flag.GetString(), out var i)) return i == 1;
                        if (flag.ValueKind == JsonValueKind.True) return true;
                    }
                }
            }
            catch { /* ignore */ }
            return false;
        }

        private async Task LoadFileInfoAsync(string filePath, int inputIndex)
        {
            var (doc, _) = await RunFfprobeAsync(filePath);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("ffprobe returned no data.");

            // Update right-side info **only** for the primary input (index 0)
            if (inputIndex == 0)
            {
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

                    string container =
                        (format.TryGetProperty("format_long_name", out var fln) ? fln.GetString() : null)
                        ?? (format.TryGetProperty("format_name", out var fn) ? fn.GetString() : null)
                        ?? "Unknown";
                    ContainerText.Text = container;
                }
                else
                {
                    BitrateText.Text = "Unknown";
                    DurationText.Text = "Unknown";
                    ContainerText.Text = "Unknown";
                }
            }

            // Headline only for primary file (to fill File Info quickly)
            string? firstVideoCodec = inputIndex == 0 ? null : null;
            string? firstAudioCodec = inputIndex == 0 ? null : null;
            string? firstAudioSampleRate = inputIndex == 0 ? null : null;
            string? firstAudioChannels = inputIndex == 0 ? null : null;

            if (doc.RootElement.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                int streamIndex = 0;
                foreach (var stream in streams.EnumerateArray())
                {
                    string type = stream.TryGetProperty("codec_type", out var codecType) ? codecType.GetString() ?? "unknown" : "unknown";
                    string codec = stream.TryGetProperty("codec_name", out var codecName) ? codecName.GetString() ?? "unknown" : "unknown";

                    // Capture headline from main file
                    if (inputIndex == 0)
                    {
                        if (string.Equals(type, "video", StringComparison.OrdinalIgnoreCase) && firstVideoCodec == null)
                            firstVideoCodec = codec;
                        if (string.Equals(type, "audio", StringComparison.OrdinalIgnoreCase) && firstAudioCodec == null)
                        {
                            firstAudioCodec = codec;
                            if (stream.TryGetProperty("channels", out var ch))
                                firstAudioChannels = ch.ToString();
                            if (stream.TryGetProperty("sample_rate", out var sr))
                                firstAudioSampleRate = sr.GetString();
                        }
                    }

                    // Language + disposition
                    string language = GetLanguageFromTags(stream);
                    bool isDefault = GetDispositionFlag(stream, "default");
                    bool isForced = GetDispositionFlag(stream, "forced");

                    // Base label
                    string desc = $"[{inputIndex}:{streamIndex}] {type.ToUpper()} - {codec}";
                    if (type == "audio")
                    {
                        if (stream.TryGetProperty("channels", out var ch))
                            desc += $" ({ch} ch)";
                        if (stream.TryGetProperty("sample_rate", out var sr) && sr.ValueKind == JsonValueKind.String)
                            desc += $" {sr.GetString()} Hz";
                    }
                    else if (type == "video")
                    {
                        if (stream.TryGetProperty("width", out var w) && stream.TryGetProperty("height", out var h))
                            desc += $" {w.GetInt32()}x{h.GetInt32()}";
                        if (stream.TryGetProperty("avg_frame_rate", out var fr) && fr.ValueKind == JsonValueKind.String)
                            desc += $" {fr.GetString()}";
                    }
                    else if (type == "attachment")
                    {
                        if (stream.TryGetProperty("tags", out var atags) && atags.ValueKind == JsonValueKind.Object)
                        {
                            if (atags.TryGetProperty("filename", out var fname) && fname.ValueKind == JsonValueKind.String)
                                desc += $" ({fname.GetString()})";
                            else if (atags.TryGetProperty("mimetype", out var mime) && mime.ValueKind == JsonValueKind.String)
                                desc += $" ({mime.GetString()})";
                        }
                    }

                    // Append language on any stream if present
                    if (!string.IsNullOrEmpty(language))
                        desc += $" [{language}]";

                    var item = new TrackItem
                    {
                        InputIndex = inputIndex,
                        StreamIndex = streamIndex,
                        Type = (type ?? "unknown").ToLowerInvariant(),
                        Codec = (codec ?? "unknown").ToLowerInvariant(),
                        Language = language,      // FIX: use the real variable
                        IsDefault = isDefault,    // FIX: use the real variable
                        IsForced = isForced,      // FIX: use the real variable
                        DelayMs = 0,
                        IsIncluded = true,        // ticked by default
                        BaseLabel = desc
                    };
                    ExtractionOutputBox.Items.Add(item);
                    streamIndex++;
                }
            }

            if (inputIndex == 0)
            {
                CodecText.Text = firstVideoCodec ?? firstAudioCodec ?? "—";
                ChannelsText.Text = firstAudioChannels ?? "—";
                SampleRateText.Text = string.IsNullOrWhiteSpace(firstAudioSampleRate) ? "—" : $"{firstAudioSampleRate} Hz";
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

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
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

        #region Apply per-track options (Language/Default/Forced/Delay)
        private void OptApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // Apply ONLY to highlighted (selected) rows
            var targets = ExtractionOutputBox.SelectedItems.Cast<TrackItem>().ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show("Select one or more tracks (highlight them) to apply options.");
                return;
            }

            string chosenLang2 = GetSelectedLanguage2();
            string chosenLang3 = ToIso639_2T(chosenLang2);

            int delayMs = 0;
            if (!string.IsNullOrWhiteSpace(OptDelayTextBox.Text))
                int.TryParse(OptDelayTextBox.Text.Trim(), out delayMs);

            bool setDefault = OptDefaultCheckBox.IsChecked == true;
            bool setForced = OptForcedCheckBox.IsChecked == true;

            foreach (var t in targets)
            {
                if (!string.IsNullOrWhiteSpace(chosenLang2))
                    t.Language = !string.IsNullOrWhiteSpace(chosenLang3) ? chosenLang3 : chosenLang2;

                t.IsDefault = setDefault;
                t.IsForced = setForced;
                t.DelayMs = delayMs;

                t.NotifyUpdated(); // refresh the live label in the list
            }

            FFmpegOutputBox.AppendText($"Applied options to {targets.Count} selected track(s).\n");
            FFmpegOutputBox.ScrollToEnd();
        }


        private string GetSelectedLanguage2()
        {
            var raw = (OptLanguageComboBox?.SelectedItem as ComboBoxItem)?.Content as string;
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var space = raw.IndexOf(' ');
            return space > 0 ? raw.Substring(0, space).Trim() : raw.Trim();
        }

        private static string ToIso639_2T(string iso2)
        {
            if (string.IsNullOrWhiteSpace(iso2)) return "";
            iso2 = iso2.Trim().ToLowerInvariant();

            return iso2 switch
            {
                "en" => "eng",
                "fr" => "fra",
                "de" => "deu",
                "es" => "spa",
                "it" => "ita",
                "pt" => "por",
                "ru" => "rus",
                "ja" => "jpn",
                "zh" => "zho",
                "ko" => "kor",
                "ar" => "ara",
                "nl" => "nld",
                "sv" => "swe",
                "pl" => "pol",
                "und" => "und",
                _ => iso2
            };
        }
        #endregion

        #region Remux (single output container, selected tracks, progress, rules)
        private async void ExtractTracksButton_Click(object sender, RoutedEventArgs e)
        {
            string input = _inputFiles.Count > 0 ? _inputFiles[0] : null;
            string outputDir = OutputPathBox.Text;

            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            {
                MessageBox.Show("Please select a valid input file.");
                return;
            }

            if (string.IsNullOrWhiteSpace(outputDir))
            {
                outputDir = Path.GetDirectoryName(input) ?? Environment.CurrentDirectory;
                _settingOutputProgrammatically = true;
                OutputPathBox.Text = outputDir;
                _settingOutputProgrammatically = false;
            }

            try { Directory.CreateDirectory(outputDir); } catch { /* ignore */ }

            var selected = ExtractionOutputBox.Items.Cast<TrackItem>()
                 .Where(t => t.IsIncluded)
                 .ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Please check at least one track to include.");
                return;
            }

            string containerExt = GetChosenContainerExtension();
            if (string.IsNullOrEmpty(containerExt))
            {
                MessageBox.Show("Please choose an output container (MP4/MKV/MOV).");
                return;
            }

            // Validate container compatibility and build lists
            var (accepted, rejected) = FilterByContainer(selected, containerExt);

            // Preflight: show plan & rejects up-front
            FFmpegOutputBox.Clear();
            FFmpegOutputBox.AppendText("=== Remux Plan ===\n");
            FFmpegOutputBox.AppendText($"Container: .{containerExt}\n");
            foreach (var t in accepted)
                FFmpegOutputBox.AppendText($" + {t.BaseLabel}\n");
            foreach (var r in rejected)
                FFmpegOutputBox.AppendText($" - {r.BaseLabel}  (excluded: {r.RejectReason})\n");
            FFmpegOutputBox.AppendText("==================\n\n");

            if (accepted.Count == 0)
            {
                MessageBox.Show("No compatible tracks for the selected container.");
                return;
            }

            // Duration
            var totalDuration = await GetDurationAsync(input);
            if (totalDuration.TotalSeconds <= 0)
            {
                TimeSpan.TryParse(DurationText.Text, out totalDuration);
            }
            if (totalDuration.TotalSeconds <= 0)
            {
                totalDuration = TimeSpan.FromSeconds(1);
            }

            // Build args
            var baseName = Path.GetFileNameWithoutExtension(input);
            var safeBase = MakeSafeFileName(baseName);
            var outPath = Path.Combine(outputDir, $"{safeBase}_remux.{containerExt}");

            var sb = new StringBuilder();
            sb.Append("-y ");

            // 1) Add base inputs
            foreach (var f in _inputFiles)
                sb.Append($"-i \"{f}\" ");

            int baseInputCount = _inputFiles.Count;

            // 2) Add delayed inputs as needed (keyed by filePath+delay)
            var delayedMap = new Dictionary<(string file, int delayMs), int>();
            foreach (var t in accepted.Where(t => t.DelayMs != 0))
            {
                var filePath = _inputFiles[t.InputIndex];
                var key = (filePath, t.DelayMs);
                if (!delayedMap.ContainsKey(key))
                {
                    double secs = t.DelayMs / 1000.0;
                    sb.Append($"-itsoffset {secs.ToString(CultureInfo.InvariantCulture)} -i \"{filePath}\" ");
                    delayedMap[key] = baseInputCount + delayedMap.Count;
                }
            }

            // 3) Map selected tracks (use delayed input index if needed)
            int vidOut = 0, audOut = 0, subOut = 0;
            var postFlags = new StringBuilder();

            foreach (var t in accepted)
            {
                int mapInputIdx;
                if (t.DelayMs != 0)
                {
                    var key = (_inputFiles[t.InputIndex], t.DelayMs);
                    mapInputIdx = delayedMap[key];
                }
                else
                {
                    mapInputIdx = t.InputIndex;
                }

                sb.Append($"-map {mapInputIdx}:{t.StreamIndex} ");

                string typeCode = t.Type.StartsWith("v") ? "v" :
                                  t.Type.StartsWith("a") ? "a" :
                                  t.Type.StartsWith("s") ? "s" : "";

                int outIdx = -1;
                if (typeCode == "v") outIdx = vidOut++;
                if (typeCode == "a") outIdx = audOut++;
                if (typeCode == "s") outIdx = subOut++;

                if (outIdx >= 0 && !string.IsNullOrWhiteSpace(typeCode))
                {
                    if (!string.IsNullOrWhiteSpace(t.Language))
                        postFlags.Append($"-metadata:s:{typeCode}:{outIdx} language={t.Language} ");

                    var flags = new List<string>();
                    if (t.IsDefault) flags.Add("default");
                    if (t.IsForced) flags.Add("forced");
                    if (flags.Count > 0)
                        postFlags.Append($"-disposition:{typeCode}:{outIdx} {string.Join("+", flags)} ");
                }
            }

            // 4) Stream copy
            sb.Append("-c copy ");

            // 5) Append metadata/dispositions
            sb.Append(postFlags.ToString());

            // 6) Output path
            sb.Append($"\"{outPath}\"");

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
                    FFmpegOutputBox.AppendText("✅ Remux complete" + Environment.NewLine);
                });
            };

            FFmpegProgressBar.Value = 0;
            await runner.RunAsync(args, totalDuration);
        }

        private (List<TrackItem> accepted, List<TrackItem> rejected) FilterByContainer(List<TrackItem> selected, string ext)
        {
            var accepted = new List<TrackItem>();
            var rejected = new List<TrackItem>();

            foreach (var t in selected)
            {
                string reason = null;
                switch (ext.ToLowerInvariant())
                {
                    case "mp4":
                        // Conservative rules
                        if (t.Type == "audio" && (t.Codec.Contains("flac") || t.Codec.Contains("dts") ||
                                                  t.Codec.Contains("truehd") || t.Codec.Contains("opus") ||
                                                  t.Codec.Contains("pcm") || t.Codec.Contains("eac3")))
                            reason = "codec not supported in MP4";
                        if (t.Type == "subtitle" && !(t.Codec.Contains("mov_text") || t.Codec.Contains("tx3g")))
                            reason = "subtitle must be mov_text in MP4";
                        break;

                    case "mov":
                        if (t.Type == "audio" && (t.Codec.Contains("flac") || t.Codec.Contains("dts") ||
                                                  t.Codec.Contains("truehd") || t.Codec.Contains("opus") ||
                                                  t.Codec.Contains("eac3")))
                            reason = "audio codec not typical for MOV";
                        if (t.Type == "subtitle" && !(t.Codec.Contains("mov_text") || t.Codec.Contains("tx3g")))
                            reason = "subtitle must be mov_text in MOV";
                        break;

                    case "mkv":
                        // MKV is flexible; allow everything
                        break;
                }

                if (reason == null)
                    accepted.Add(t);
                else
                {
                    t.RejectReason = reason;
                    rejected.Add(t);
                }
            }

            return (accepted, rejected);
        }

        private string GetChosenContainerExtension()
        {
            if (ContainerMp4Radio?.IsChecked == true) return "mp4";
            if (ContainerMkvRadio?.IsChecked == true) return "mkv";
            if (ContainerMovRadio?.IsChecked == true) return "mov";
            return "";
        }
        #endregion

        private void ExtractionOutputBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // No-op; inclusion is controlled by the checkbox binding to IsIncluded.
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

        // Item for the track list (works for main and external inputs)
        private sealed class TrackItem : INotifyPropertyChanged
        {
            public int InputIndex { get; set; }   // ffmpeg -i order (0 = primary)
            public int StreamIndex { get; set; }  // index within that input
            public string Type { get; set; } = "";
            public string Codec { get; set; } = "";

            // Track metadata and options
            public string Language { get; set; } = "";  // e.g. "eng"
            public bool IsDefault { get; set; }
            public bool IsForced { get; set; }
            public int DelayMs { get; set; } = 0;

            // Independent include toggle (decoupled from ListBox selection)
            private bool _isIncluded = true;
            public bool IsIncluded
            {
                get => _isIncluded;
                set
                {
                    if (_isIncluded != value)
                    {
                        _isIncluded = value;
                        OnPropertyChanged(nameof(IsIncluded));
                        OnPropertyChanged(nameof(DisplayText));
                    }
                }
            }

            // Base label
            private string _baseLabel = "";
            public string BaseLabel
            {
                get => _baseLabel;
                set
                {
                    var v = value ?? "";
                    if (_baseLabel != v)
                    {
                        _baseLabel = v;
                        OnPropertyChanged(nameof(BaseLabel));
                        OnPropertyChanged(nameof(DisplayText));
                    }
                }
            }

            // Back-compat aliases (if any older binding used these)
            public string BaseDescription { get => BaseLabel; set => BaseLabel = value; }
            public string Description { get => BaseLabel; set => BaseLabel = value; }

            // Optional reason a track was excluded (container rules, etc.)
            public string RejectReason { get; set; } = "";

            // What the UI shows for each row
            public string DisplayText
            {
                get
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(BaseLabel)) parts.Add(BaseLabel);
                    if (!string.IsNullOrWhiteSpace(Language)) parts.Add($"[{Language}]");

                    var flags = new List<string>();
                    if (IsDefault) flags.Add("default");
                    if (IsForced) flags.Add("forced");
                    if (flags.Count > 0) parts.Add($"({string.Join(",", flags)})");

                    if (DelayMs != 0) parts.Add($"{(DelayMs > 0 ? "+" : "")}{DelayMs}ms");
                    if (!string.IsNullOrWhiteSpace(RejectReason)) parts.Add($"[excluded: {RejectReason}]");

                    return string.Join(" ", parts);
                }
            }

            public void NotifyUpdated() => OnPropertyChanged(nameof(DisplayText));
            public override string ToString() => DisplayText;

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
