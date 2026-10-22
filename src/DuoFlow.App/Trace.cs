using System;
using System.IO;

namespace DuoFlow.App;

/// <summary>
/// Minimal file tracer used on headless CI runs: appends a timestamped line
/// to %USERPROFILE%\duoflow-trace.txt so the CI log can reveal exactly how
/// far the launch sequence got. Never throws.
/// </summary>
public static class Trace
{
    private static readonly object Gate = new();
    private static string? _path;

    /// <summary>Cap for the trace file. It is appended once a second for as
    /// long as the console is open, so an unbounded file would grow silently
    /// in the user's profile forever (DD-051). Checked ONCE per process - the
    /// cost must not land on every line.</summary>
    private const long MaxBytes = 4L * 1024 * 1024;

    private static bool _sizeChecked;

    private static string FilePath
        => _path ??= System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "duoflow-trace.txt");

    public static void Log(string message)
    {
        try
        {
            lock (Gate)
            {
                TrimOnce();
                File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss.fff}  {message}\r\n");
            }
        }
        catch
        {
            // Never let tracing break the app.
        }
    }

    private static void TrimOnce()
    {
        if (_sizeChecked)
        {
            return;
        }
        _sizeChecked = true;
        try
        {
            var info = new FileInfo(FilePath);
            if (info.Exists && info.Length > MaxBytes)
            {
                File.WriteAllText(FilePath, "");
            }
        }
        catch
        {
            // Best effort - a read-only profile just keeps the old file.
        }
    }
}
