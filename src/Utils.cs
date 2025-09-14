using Spectre.Console;
using System.Globalization;

public static class Utils
{
    public static long ToBytes(string size)
    {
        if (string.IsNullOrWhiteSpace(size))
            throw new ArgumentException("Size string is empty");

        var parts = size.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new FormatException("Invalid size format");

        var number = double.Parse(parts[0], CultureInfo.InvariantCulture);
        var unit = parts[1].ToUpper();

        return unit switch
        {
            "B" => (long)number,
            "KB" => (long)(number * 1024),
            "MB" => (long)(number * 1_048_576),
            "GB" => (long)(number * 1_073_741_824),
            _ => throw new FormatException($"Unknown unit: {unit}")
        };
    }

    public static string ToStringWithUnit(long bytes)
    {
        double size = bytes;
        string suffix;

        if (size >= 1_073_741_824) // GB
        {
            size /= 1_073_741_824;
            suffix = "GB";
        }
        else // MB
        {
            size /= 1_048_576;
            suffix = "MB";
        }

        return $"{size:F2} {suffix}";
    }

    public static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";

        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";

        return $"{ts.Seconds}s";
    }

    public static bool IsClipboardAvailable()
    {
        if (!OperatingSystem.IsLinux())
            return true;

        // Linux requires xsel be installed for clipboard to work
#if LINUX
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "xsel",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(500);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
#endif

        return false;
    } 

    public static async Task PressAnyKeyOrWaitAsync(int delayInMs)
    {
        await AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(new NullSpinner())
                .StartAsync("#", async ctx =>
                {
                    var delayMs = delayInMs;
                    while (delayMs >= 0)
                    {
                        if (Console.KeyAvailable)
                        {
                            Console.ReadKey(true);
                            break;
                        }
                        ctx.Status($"[grey]Press any key to exit or wait [silver]{delayMs / 1000.0:0.0}s[/].[/]");
                        delayMs -= 100;
                        await Task.Delay(100);
                    }
                });
    }

    private class NullSpinner : Spinner
    {
        public override TimeSpan Interval => TimeSpan.FromMinutes(1);
        public override bool IsUnicode => false;
        public override IReadOnlyList<string> Frames => new[] { "" };
    }
}