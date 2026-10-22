using System;
using System.IO;
using DuoFlow.Render;

namespace DuoFlow.App;

/// <summary>
/// DD-051: the on-disk half of the parameter panel. The console is the editor,
/// this is the memory - a tuning session that evaporates on restart makes M2.3
/// (visual tuning) cost a re-dial every launch.
///
/// File: %LOCALAPPDATA%\DuoFlow\tuning.json. Written debounced by the panel,
/// read once at startup. Every failure path falls back to the compiled-in
/// defaults and leaves a trace line: a corrupt file must never stop the app
/// from starting, and must never silently change how the effect looks.
///
/// No secrets, no paths, no machine identifiers - just the 16 knobs.
/// </summary>
internal static class TuningStore
{
    private static string? _path;

    /// <summary>Full path of the preset file (exists only after the first save).</summary>
    public static string FilePath => _path ??= Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DuoFlow",
        "tuning.json");

    /// <summary>Load the saved preset, or the defaults. Never throws.</summary>
    public static TuningPreset Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Trace.Log($"tuning: no saved preset at {FilePath} - using defaults");
                return TuningPreset.Default;
            }

            TuningPreset? preset = TuningPreset.TryParse(File.ReadAllText(FilePath), out string? error);
            if (preset is null)
            {
                // Keep the bad file for inspection instead of overwriting it
                // with defaults on the next save - the user may want to see it.
                Trace.Log($"tuning: saved preset REJECTED ({error}) - using defaults");
                return TuningPreset.Default;
            }

            Trace.Log($"tuning: loaded preset from {FilePath}");
            return preset;
        }
        catch (Exception ex)
        {
            Trace.Log($"tuning: load FAILED ({ex.GetType().Name}: {ex.Message}) - using defaults");
            return TuningPreset.Default;
        }
    }

    /// <summary>Persist a preset. Never throws (a read-only profile is not fatal).</summary>
    public static void Save(TuningPreset preset)
    {
        try
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(FilePath, preset.ToJson());
        }
        catch (Exception ex)
        {
            Trace.Log($"tuning: save FAILED ({ex.GetType().Name}: {ex.Message})");
        }
    }
}
