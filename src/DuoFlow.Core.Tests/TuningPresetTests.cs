using System;
using DuoFlow.Render;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// DD-051: the render-side tuning snapshot is the SAME object the panel edits
/// and the file stores. These tests pin the three properties that make that
/// safe:
///
///  1. a snapshot round-trips through <see cref="TuningPreset.ToOptions"/> and
///     through JSON without losing or mutating a knob;
///  2. a corrupt / out-of-range / partial file NEVER yields a half-valid
///     preset - it yields null, and the caller falls back to the defaults;
///  3. an ABSENT property keeps the compiled-in default, so an old file stays
///     loadable after a knob is added (forward compatibility).
/// </summary>
public class TuningPresetTests
{
    private static TuningPreset Sample() => new()
    {
        MaxFoldDegrees = 62.0,
        CameraDistanceRatio = 3.75,
        TiltAwayFromViewer = false,
        HingeAtTop = true,
        MaxBlurPixels = 211.0,
        StartProgress = 0.12,
        FullProgress = 0.73,
        GradientEnabled = true,
        HueStartDegrees = 301.0,
        HueEndDegrees = 42.0,
        GradientStrength = 0.17,
        SweepEnabled = true,
        SweepPosition = 0.35,
        SweepWidth = 0.21,
        SweepStrength = 0.19,
        SweepDirection = SweepDirection.FarToHinge,
    };

    [Fact]
    public void Defaults_MatchTheOptionClasses()
    {
        TuningPreset p = TuningPreset.Default;
        var (warp, blur, gradient, sweep) = p.ToOptions();

        Assert.Equal(WarpOptions.DefaultMaxFoldDegrees, warp.MaxFoldDegrees, 12);
        Assert.Equal(WarpOptions.DefaultCameraDistanceRatio, warp.CameraDistanceRatio, 12);
        Assert.True(warp.TiltAwayFromViewer);
        Assert.False(warp.HingeAtTop);
        Assert.Equal(BlurOptions.DefaultMaxBlurPixels, blur.MaxBlurPixels, 12);
        Assert.Equal(BlurOptions.DefaultStartProgress, blur.StartProgress, 12);
        Assert.Equal(BlurOptions.DefaultFullProgress, blur.FullProgress, 12);
        Assert.False(gradient.Enabled);
        Assert.Equal(GradientOptions.DefaultStrength, gradient.Strength, 12);
        Assert.False(sweep.Enabled);
        Assert.Equal(LightSweepOptions.DefaultStrength, sweep.Strength, 12);
        Assert.Equal(SweepDirection.HingeToFar, sweep.Direction);
        p.Validate();
    }

    [Fact]
    public void FromOptions_ThenToOptions_IsIdentity()
    {
        var warp = new WarpOptions
        {
            MaxFoldDegrees = 55.0, CameraDistanceRatio = 4.5, TiltAwayFromViewer = false, HingeAtTop = true,
        };
        var blur = new BlurOptions { MaxBlurPixels = 200.0, StartProgress = 0.2, FullProgress = 0.8 };
        var gradient = new GradientOptions
        {
            Enabled = true, HueStartDegrees = 10.0, HueEndDegrees = 350.0, Strength = 0.15,
        };
        var sweep = new LightSweepOptions
        {
            Enabled = true, Position = 0.4, Width = 0.3, Strength = 0.12,
            Direction = SweepDirection.FarToHinge,
        };

        var (w2, b2, g2, s2) = TuningPreset.FromOptions(warp, blur, gradient, sweep).ToOptions();

        Assert.Equal(55.0, w2.MaxFoldDegrees, 12);
        Assert.Equal(4.5, w2.CameraDistanceRatio, 12);
        Assert.False(w2.TiltAwayFromViewer);
        Assert.True(w2.HingeAtTop);
        Assert.Equal(200.0, b2.MaxBlurPixels, 12);
        Assert.Equal(0.2, b2.StartProgress, 12);
        Assert.Equal(0.8, b2.FullProgress, 12);
        Assert.True(g2.Enabled);
        Assert.Equal(10.0, g2.HueStartDegrees, 12);
        Assert.Equal(350.0, g2.HueEndDegrees, 12);
        Assert.Equal(0.15, g2.Strength, 12);
        Assert.True(s2.Enabled);
        Assert.Equal(0.4, s2.Position, 12);
        Assert.Equal(0.3, s2.Width, 12);
        Assert.Equal(0.12, s2.Strength, 12);
        Assert.Equal(SweepDirection.FarToHinge, s2.Direction);
    }

    [Fact]
    public void Json_RoundTrip_PreservesEveryKnob()
    {
        TuningPreset original = Sample();
        TuningPreset? parsed = TuningPreset.TryParse(original.ToJson(), out string? error);

        Assert.True(parsed is not null, $"preset should parse: {error}");
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void Json_RoundTrip_IsStable_WhenSerializedTwice()
    {
        string once = Sample().ToJson();
        TuningPreset? again = TuningPreset.TryParse(once, out _);
        Assert.NotNull(again);
        Assert.Equal(once, again!.ToJson());
    }

    [Fact]
    public void Enum_IsWrittenAsAReadableName()
    {
        // A numeric enum silently accepts values the model never defined;
        // a name makes an unparsable file fail loudly instead.
        Assert.Contains("FarToHinge", Sample().ToJson());
    }

    [Fact]
    public void EmptyObject_KeepsEveryDefault()
    {
        TuningPreset? parsed = TuningPreset.TryParse("{}", out string? error);

        Assert.True(parsed is not null, $"empty object should still load: {error}");
        Assert.Equal(TuningPreset.Default, parsed);
    }

    [Fact]
    public void UnknownProperties_AreIgnored_ForForwardCompatibility()
    {
        string json = "{ \"MaxFoldDegrees\": 70, \"FutureKnob\": 123, \"Nested\": { \"a\": 1 } }";

        TuningPreset? parsed = TuningPreset.TryParse(json, out string? error);

        Assert.True(parsed is not null, $"unknown properties must not break loading: {error}");
        Assert.Equal(70.0, parsed!.MaxFoldDegrees, 12);
        Assert.Equal(BlurOptions.DefaultMaxBlurPixels, parsed.MaxBlurPixels, 12);
    }

    [Fact]
    public void PropertyNames_AreCaseInsensitive()
    {
        TuningPreset? parsed = TuningPreset.TryParse("{ \"maxfolddegrees\": 44 }", out string? error);

        Assert.True(parsed is not null, $"hand-edited files should load: {error}");
        Assert.Equal(44.0, parsed!.MaxFoldDegrees, 12);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MissingOrBlank_IsRejected_WithAReason(string? json)
    {
        Assert.Null(TuningPreset.TryParse(json, out string? error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{ \"MaxFoldDegrees\": \"seventy\" }")]
    [InlineData("{ \"SweepDirection\": \"Sideways\" }")]
    public void CorruptJson_IsRejected_AndNeverThrows(string json)
    {
        TuningPreset? parsed = TuningPreset.TryParse(json, out string? error);

        Assert.Null(parsed);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("{ \"MaxFoldDegrees\": 120 }")]          // > 90: past edge-on
    [InlineData("{ \"MaxFoldDegrees\": 0 }")]            // degenerate fold
    [InlineData("{ \"CameraDistanceRatio\": 1.0 }")]     // far edge on the camera plane
    [InlineData("{ \"MaxBlurPixels\": 0 }")]
    [InlineData("{ \"MaxBlurPixels\": 401 }")]
    [InlineData("{ \"StartProgress\": 0.9 }")]
    [InlineData("{ \"StartProgress\": 0.8, \"FullProgress\": 0.5 }")]  // inverted envelope
    [InlineData("{ \"GradientStrength\": 0.5 }")]        // past the tint cap
    [InlineData("{ \"SweepWidth\": 0.9 }")]              // past the band cap
    [InlineData("{ \"SweepPosition\": -0.1 }")]
    public void OutOfRangeValues_AreRejected_NotClampedSilently(string json)
    {
        TuningPreset? parsed = TuningPreset.TryParse(json, out string? error);

        Assert.Null(parsed);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Validate_AcceptsTheDefaults_AndRejectsAnInvertedEnvelope()
    {
        TuningPreset.Default.Validate();

        var inverted = TuningPreset.Default with { StartProgress = 0.6, FullProgress = 0.4 };
        Assert.Throws<ArgumentOutOfRangeException>(inverted.Validate);
    }

    [Fact]
    public void ToOptions_DoesNotValidate_SoValidateIsTheGate()
    {
        // Documenting the contract: ToOptions is a dumb adapter; a caller that
        // skips Validate gets exactly the garbage it asked for.
        var bad = TuningPreset.Default with { MaxFoldDegrees = 500.0 };
        var (warp, _, _, _) = bad.ToOptions();
        Assert.Equal(500.0, warp.MaxFoldDegrees, 12);
        Assert.Throws<ArgumentOutOfRangeException>(bad.Validate);
    }
}
