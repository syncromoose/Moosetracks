using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

public class FFmpegRunner
{
    private readonly string _ffmpegPath;

    public event Action<double>? ProgressChanged;
    public event Action<string>? LogReceived;
    public event Action? Completed;

    public FFmpegRunner(string ffmpegPath)
    {
        _ffmpegPath = ffmpegPath;
    }

    public async Task RunAsync(string arguments, TimeSpan totalDuration)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"{arguments} -progress pipe:1 -nostats",
            RedirectStandardOutput = true,   // -progress writes to stdout (pipe:1)
            RedirectStandardError = true,    // stderr still has logs
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            LogReceived?.Invoke(e.Data);

            // Parse progress lines
            if (e.Data.StartsWith("out_time_ms="))
            {
                var val = e.Data.Substring("out_time_ms=".Length).Trim();
                if (long.TryParse(val, out long micro))
                {
                    var seconds = micro / 1_000_000.0;
                    ReportProgress(seconds, totalDuration);
                }
            }
            else if (e.Data.StartsWith("out_time="))
            {
                var val = e.Data.Substring("out_time=".Length).Trim();
                if (TryParseOutTime(val, out var ts))
                {
                    ReportProgress(ts.TotalSeconds, totalDuration);
                }
            }
            else if (e.Data.StartsWith("progress=end"))
            {
                ProgressChanged?.Invoke(100);
            }
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                LogReceived?.Invoke(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();
        Completed?.Invoke();
    }

    private void ReportProgress(double outSeconds, TimeSpan totalDuration)
    {
        if (totalDuration.TotalSeconds <= 0) return;
        var pct = Math.Max(0, Math.Min(100, outSeconds / totalDuration.TotalSeconds * 100.0));
        ProgressChanged?.Invoke(pct);
    }

    private static bool TryParseOutTime(string s, out TimeSpan ts)
    {
        // Format: HH:MM:SS.microseconds
        ts = default;
        try
        {
            var parts = s.Split(':');
            if (parts.Length != 3) return false;

            int hh = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int mm = int.Parse(parts[1], CultureInfo.InvariantCulture);

            var secParts = parts[2].Split('.');
            int ss = int.Parse(secParts[0], CultureInfo.InvariantCulture);
            int micro = 0;
            if (secParts.Length > 1)
            {
                var frac = secParts[1];
                if (frac.Length > 6) frac = frac.Substring(0, 6);
                while (frac.Length < 6) frac += "0";
                micro = int.Parse(frac, CultureInfo.InvariantCulture);
            }

            ts = new TimeSpan(0, hh, mm, ss) + TimeSpan.FromTicks(micro * 10);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
