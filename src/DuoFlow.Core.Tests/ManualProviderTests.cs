using DuoFlow.Core;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>LidState data model (PROJECT_SPEC §5).</summary>
public class LidStateTests
{
    [Fact]
    public void LidState_HoldsValues_AndHasValueEquality()
    {
        var a = new LidState(Angle: 90, Progress: 0.6, Velocity: 0.1, Confidence: 0.72, Source: LidStateSource.Camera);
        var b = new LidState(Angle: 90, Progress: 0.6, Velocity: 0.1, Confidence: 0.72, Source: LidStateSource.Camera);
        var c = a with { Progress = 0.7 };

        Assert.Equal(a, b);                       // records: value equality
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
        Assert.Equal(90, a.Angle);
        Assert.Equal(0.6, a.Progress);
        Assert.Equal(0.1, a.Velocity);
        Assert.Equal(0.72, a.Confidence);
        Assert.Equal(LidStateSource.Camera, a.Source);
    }

    [Fact]
    public void LidStateSource_Enum_MatchesSpecExactly()
    {
        // PROJECT_SPEC §5 - members and order are normative; reordering would
        // silently change persisted/compared numeric values.
        string[] expected = ["Manual", "Camera", "Sensor", "Unknown"];
        Assert.Equal(expected, Enum.GetNames<LidStateSource>());
        Assert.Equal(0, (int)LidStateSource.Manual);
        Assert.Equal(3, (int)LidStateSource.Unknown);
    }
}

/// <summary>ILidStateProvider contract (PROJECT_SPEC §18) via ManualProvider.</summary>
public class ManualProviderTests
{
    private static (ManualProvider Provider, List<LidState> Received) CreateCapturing()
    {
        var provider = new ManualProvider();
        var received = new List<LidState>();
        provider.StateChanged += (_, s) => received.Add(s);
        return (provider, received);
    }

    [Fact]
    public void IsAvailable_IsAlwaysTrue()
    {
        // DD-004: manual input has no hardware dependency to detect.
        Assert.True(new ManualProvider().IsAvailable);
    }

    [Fact]
    public void SetProgress_RaisesStateChanged_Immediately_WithManualSemantics()
    {
        var (provider, received) = CreateCapturing();

        provider.SetProgress(0.42);

        var s = Assert.Single(received);
        Assert.Equal(0.42, s.Progress);
        Assert.Equal(LidStateSource.Manual, s.Source);
        Assert.Equal(1.0, s.Confidence);      // HC §4.3: Manual = 1.00
        Assert.Equal(0.0, s.Velocity);        // velocity is M1.3's job
    }

    [Fact]
    public void SetProgress_ClampsToUnitInterval()
    {
        var (provider, received) = CreateCapturing();

        provider.SetProgress(-0.5);
        provider.SetProgress(1.5);

        Assert.Equal(2, received.Count);
        Assert.Equal(0.0, received[0].Progress);   // 0 = fully open
        Assert.Equal(1.0, received[1].Progress);   // 1 = NEAR fully closed (§6)
        Assert.Equal(1.0, provider.CurrentState.Progress);
    }

    [Fact]
    public void SetProgress_NaN_Throws()
    {
        var provider = new ManualProvider();
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.SetProgress(double.NaN));
    }

    [Fact]
    public void SetProgress_RaisesAgain_EvenForSameValue_AndNotifiesAllSubscribers()
    {
        var provider = new ManualProvider();
        var a = new List<LidState>();
        var b = new List<LidState>();
        EventHandler<LidState> ha = (_, s) => a.Add(s);
        EventHandler<LidState> hb = (_, s) => b.Add(s);
        provider.StateChanged += ha;
        provider.StateChanged += hb;

        provider.SetProgress(0.5);
        provider.SetProgress(0.5);   // manual input is authoritative: re-raise by design

        Assert.Equal(2, a.Count);
        Assert.Equal(2, b.Count);
        Assert.All(a, s => Assert.Equal(0.5, s.Progress));
    }

    [Fact]
    public void Unsubscribe_StopsReceivingEvents()
    {
        var provider = new ManualProvider();
        var received = new List<LidState>();
        EventHandler<LidState> h = (_, s) => received.Add(s);
        provider.StateChanged += h;

        provider.SetProgress(0.1);
        provider.StateChanged -= h;
        provider.SetProgress(0.2);

        var s = Assert.Single(received);
        Assert.Equal(0.1, s.Progress);
    }

    [Fact]
    public async Task StartStop_ManageLifecycle_AndStartEmitsBaseline()
    {
        var (provider, received) = CreateCapturing();

        Assert.False(provider.IsRunning);
        await provider.StartAsync();
        Assert.True(provider.IsRunning);

        // Start emits the current state once (baseline: 0 = fully open).
        var baseline = Assert.Single(received);
        Assert.Equal(0.0, baseline.Progress);
        Assert.Equal(LidStateSource.Manual, baseline.Source);

        await provider.StopAsync();
        Assert.False(provider.IsRunning);
    }

    [Fact]
    public void Angle_DefaultMapping_FollowsSpecIllustration_Monotonically()
    {
        // PROJECT_SPEC §6 illustrative endpoints: 0.0 -> 180°, 1.0 -> 30°.
        // Placeholder default only - calibration will replace it (DD-035).
        var provider = new ManualProvider();

        Assert.Equal(180.0, provider.CurrentState.Angle, 10);
        provider.SetProgress(1.0);
        Assert.Equal(30.0, provider.CurrentState.Angle, 10);

        double previous = double.PositiveInfinity;
        for (double p = 0.0; p <= 1.0; p += 0.1)
        {
            provider.SetProgress(p);
            double angle = provider.CurrentState.Angle;
            Assert.True(angle <= previous, $"Angle must not increase with progress (at p={p})");
            Assert.InRange(angle, 30.0, 180.0);
            previous = angle;
        }
    }

    [Fact]
    public void CurrentState_MatchesLastSetProgress()
    {
        var provider = new ManualProvider();
        provider.SetProgress(0.75);

        Assert.Equal(0.75, provider.CurrentState.Progress);
        Assert.Equal(LidStateSource.Manual, provider.CurrentState.Source);
        Assert.Equal(1.0, provider.CurrentState.Confidence);
    }
}

/// <summary>
/// M1.2 UI-driving contracts: the slider (0~1, StepFrequency 0.01) and the
/// Home/End accelerators both funnel through SetProgress, and the UI display
/// pulls from CurrentState (single source of truth). These tests pin the
/// provider-side half of that chain - no WinUI involved.
/// </summary>
public class ManualProviderDrivingTests
{
    [Fact]
    public void SetProgress_HomeEndBoundaries_AreExact_OnSpecEndpoints()
    {
        // Keyboard shortcuts Home -> 0 and End -> 1 (M1.2) route through
        // SetProgress; both boundaries must survive untouched (no clamp
        // drift) so Angle lands exactly on the §6 endpoints 180° / 30°.
        var provider = new ManualProvider();

        provider.SetProgress(0.0);   // Home
        Assert.Equal(0.0, provider.CurrentState.Progress);
        Assert.Equal(ManualProvider.DefaultOpenAngleDegrees, provider.CurrentState.Angle, 9);

        provider.SetProgress(1.0);   // End
        Assert.Equal(1.0, provider.CurrentState.Progress);
        Assert.Equal(ManualProvider.DefaultNearClosedAngleDegrees, provider.CurrentState.Angle, 9);
    }

    [Fact]
    public void SetProgress_SliderStepValues_ArePreserved()
    {
        // The M1.2 slider moves in 0.01 steps; every step the user can
        // produce must come back out of CurrentState unchanged (display
        // equals provider state), within float tolerance of the slider's
        // own step arithmetic.
        var provider = new ManualProvider();
        for (int step = 0; step <= 100; step++)
        {
            double sliderValue = Math.Round(step * 0.01, 2);
            provider.SetProgress(sliderValue);
            Assert.InRange(provider.CurrentState.Progress, sliderValue - 1e-9, sliderValue + 1e-9);
        }
    }

    [Fact]
    public void SetProgress_StepZero_HomeTwice_ReraisesWithStableDisplay()
    {
        // Pressing Home twice: same value re-raises (DD-035), and the state
        // the display pulls stays stable (0.00 / 180°) - no drift, no NaN.
        var provider = new ManualProvider();
        var seen = new List<double>();
        provider.StateChanged += (_, s) => seen.Add(s.Progress);

        provider.SetProgress(0.35);
        provider.SetProgress(0.0);   // Home
        provider.SetProgress(0.0);   // Home again

        double[] expected = [0.35, 0.0, 0.0];
        Assert.Equal(expected, seen);
        Assert.Equal(0.0, provider.CurrentState.Progress);
        Assert.Equal(180.0, provider.CurrentState.Angle, 9);
    }
}
