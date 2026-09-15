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
                File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss.fff}  {message}\r\n");
            }
        }
        catch
        {
            // Never let tracing break the app.
        }
    }
}
