using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DuoFlow.Render;

/// <summary>
/// One validated snapshot of every render-side knob (DD-051): the console's
/// parameter panel and the on-disk preset carry the SAME object, so "what the
/// panel shows", "what the renderer applies" and "what survives a restart"
/// cannot drift apart.
///
/// It is a plain record of primitives on purpose - nothing here knows about
/// WinUI, D3D or file IO. That keeps the whole persistence contract unit
/// testable (DuoFlow.Core.Tests references this project), and it keeps the
/// JSON stable: adding a knob here adds it to the file, and an OLD file simply
/// leaves the new property at its default.
///
/// Why a flat record instead of four nested option objects: the four option
/// classes are <c>init</c>-only by design (DD-048) and validate themselves;
/// serialising them directly would couple the file format to their internal
/// shape. This record is the wire format, <see cref="ToOptions"/> is the
/// adapter.
/// </summary>
public sealed record TuningPreset
{
    // --- WarpOptions (DD-039) ---
    public double MaxFoldDegrees { get; init; } = WarpOptions.DefaultMaxFoldDegrees;
    public double CameraDistanceRatio { get; init; } = WarpOptions.DefaultCameraDistanceRatio;
    public bool TiltAwayFromViewer { get; init; } = true;
    public bool HingeAtTop { get; init; }

    // --- BlurOptions (DD-043 / DD-046) ---
    public double MaxBlurPixels { get; init; } = BlurOptions.DefaultMaxBlurPixels;
    public double StartProgress { get; init; } = BlurOptions.DefaultStartProgress;
    public double FullProgress { get; init; } = BlurOptions.DefaultFullProgress;

    // --- GradientOptions (DD-049) ---
    public bool GradientEnabled { get; init; }
    public double HueStartDegrees { get; init; } = GradientOptions.DefaultHueStartDegrees;
    public double HueEndDegrees { get; init; } = GradientOptions.DefaultHueEndDegrees;
    public double GradientStrength { get; init; } = GradientOptions.DefaultStrength;

    // --- LightSweepOptions (DD-050) ---
    public bool SweepEnabled { get; init; }
    public double SweepPosition { get; init; } = LightSweepOptions.DefaultPosition;
    public double SweepWidth { get; init; } = LightSweepOptions.DefaultWidth;
    public double SweepStrength { get; init; } = LightSweepOptions.DefaultStrength;
    public SweepDirection SweepDirection { get; init; } = SweepDirection.HingeToFar;

    /// <summary>The compiled-in defaults, read from the option classes (never
    /// duplicated as literals - "restore defaults" must follow the model).</summary>
    public static TuningPreset Default => new();

    /// <summary>Snapshot the four live option objects into one preset.</summary>
    public static TuningPreset FromOptions(
        WarpOptions warp, BlurOptions blur, GradientOptions gradient, LightSweepOptions sweep)
    {
        ArgumentNullException.ThrowIfNull(warp);
        ArgumentNullException.ThrowIfNull(blur);
        ArgumentNullException.ThrowIfNull(gradient);
        ArgumentNullException.ThrowIfNull(sweep);

        return new TuningPreset
        {
            MaxFoldDegrees = warp.MaxFoldDegrees,
            CameraDistanceRatio = warp.CameraDistanceRatio,
            TiltAwayFromViewer = warp.TiltAwayFromViewer,
            HingeAtTop = warp.HingeAtTop,
            MaxBlurPixels = blur.MaxBlurPixels,
            StartProgress = blur.StartProgress,
            FullProgress = blur.FullProgress,
            GradientEnabled = gradient.Enabled,
            HueStartDegrees = gradient.HueStartDegrees,
            HueEndDegrees = gradient.HueEndDegrees,
            GradientStrength = gradient.Strength,
            SweepEnabled = sweep.Enabled,
            SweepPosition = sweep.Position,
            SweepWidth = sweep.Width,
            SweepStrength = sweep.Strength,
            SweepDirection = sweep.Direction,
        };
    }

    /// <summary>Rebuild the four option objects from this preset. Does NOT
    /// validate - call <see cref="Validate"/> (or <see cref="TryParse"/>, which
    /// validates) before handing the result to the renderer.</summary>
    public (WarpOptions Warp, BlurOptions Blur, GradientOptions Gradient, LightSweepOptions Sweep) ToOptions()
        => (
            new WarpOptions
            {
                MaxFoldDegrees = MaxFoldDegrees,
                CameraDistanceRatio = CameraDistanceRatio,
                TiltAwayFromViewer = TiltAwayFromViewer,
                HingeAtTop = HingeAtTop,
            },
            new BlurOptions
            {
                MaxBlurPixels = MaxBlurPixels,
                StartProgress = StartProgress,
                FullProgress = FullProgress,
            },
            new GradientOptions
            {
                Enabled = GradientEnabled,
                HueStartDegrees = HueStartDegrees,
                HueEndDegrees = HueEndDegrees,
                Strength = GradientStrength,
            },
            new LightSweepOptions
            {
                Enabled = SweepEnabled,
                Position = SweepPosition,
                Width = SweepWidth,
                Strength = SweepStrength,
                Direction = SweepDirection,
            });

    /// <summary>Throws when any knob would render garbage - same contract as
    /// the individual option classes, applied to the whole set at once.</summary>
    public void Validate()
    {
        var (warp, blur, gradient, sweep) = ToOptions();
        OptionSet.ValidateOrThrow(warp, blur, gradient, sweep);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        // Enum as a readable string: a numeric enum silently accepts values the
        // model never defined, a name does not.
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serialize to the on-disk form.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>
    /// Parse + validate. NEVER throws and never returns a half-valid preset:
    /// a corrupt or out-of-range file must fall back to the compiled-in
    /// defaults (the caller decides), not silently change how the effect looks.
    /// An ABSENT property keeps this record's initializer default, so an old
    /// file stays loadable after a knob is added.
    /// </summary>
    public static TuningPreset? TryParse(string? json, out string? error)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "empty preset";
            return null;
        }

        TuningPreset? preset;
        try
        {
            preset = JsonSerializer.Deserialize<TuningPreset>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return null;
        }

        if (preset is null)
        {
            error = "preset body is null";
            return null;
        }

        var (warp, blur, gradient, sweep) = preset.ToOptions();
        error = OptionSet.ValidateAll(warp, blur, gradient, sweep);
        return error is null ? preset : null;
    }
}

/// <summary>
/// The one place that decides whether a whole option set is renderable.
/// Extracted (DD-051) so the renderer's SetOptions path and the preset loader
/// enforce exactly the same rules instead of drifting into two copies.
/// </summary>
public static class OptionSet
{
    /// <summary>Throws on the first invalid member (the contract every option
    /// class already has). Both sweep ends are checked, so a combination that
    /// only breaks at progress 1 is caught too.</summary>
    public static void ValidateOrThrow(
        WarpOptions warp, BlurOptions blur, GradientOptions gradient, LightSweepOptions sweep)
    {
        ArgumentNullException.ThrowIfNull(warp);
        ArgumentNullException.ThrowIfNull(blur);
        ArgumentNullException.ThrowIfNull(gradient);
        ArgumentNullException.ThrowIfNull(sweep);

        WarpGeometry.Compute(0.0, warp);
        WarpGeometry.Compute(1.0, warp);
        blur.Validate();
        gradient.Validate();
        sweep.Validate();
    }

    /// <summary>null when the set is valid, otherwise a human-readable reason.
    /// The non-throwing form, used where a rejection must become a status line
    /// (the console panel and the preset loader) instead of an exception.</summary>
    public static string? ValidateAll(
        WarpOptions? warp, BlurOptions? blur, GradientOptions? gradient, LightSweepOptions? sweep)
    {
        if (warp is null) { return "warp options null"; }
        if (blur is null) { return "blur options null"; }
        if (gradient is null) { return "gradient options null"; }
        if (sweep is null) { return "sweep options null"; }

        try
        {
            ValidateOrThrow(warp, blur, gradient, sweep);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
