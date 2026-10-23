using DuoFlow.Core;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M1.3 Animation Engine: smoothing, velocity, anti-jump. All tests drive a
/// ManualTimeSource so results are deterministic (no wall clock).
/// </summary>
public class AnimationEngineTests
{
    private static LidState Raw(double progress, LidStateSource? source = null, double confidence = 1.0)
        => new(Angle: 0, Progress: progress, Velocity: 0, Confidence: confidence,
               Source: source ?? LidStateSource.Manual);

    // ---- first observation -------------------------------------------------

    [Fact]
    public void FirstUpdate_SnapsToTarget_DoesNotAnimateFromZero()
    {
        // A fresh engine adopts the world as it is; anti-jump only applies to
        // changes it has actually observed.
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);

        engine.Update(Raw(0.8));

        Assert.Equal(0.8, engine.CurrentProgress, 9);
        Assert.Equal(0.0, engine.CurrentVelocity, 9);
    }

    // ---- monotonic input / convergence -------------------------------------

    [Fact]
    public void MonotonicInput_MovesMonotonically_AndConverges()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));

        engine.Update(Raw(1.0));   // keyboard End: discontinuous target

        double previous = -1;
        for (int i = 0; i < 400; i++)
        {
            clock.AdvanceMilliseconds(5);
            engine.Tick();
            double p = engine.CurrentProgress;

            Assert.InRange(p, 0.0, 1.0);
            Assert.True(p >= previous - 1e-9, $"progress must not move backwards at step {i}");
            previous = p;
        }

        Assert.True(Math.Abs(engine.CurrentProgress - 1.0) < 1e-3, "must converge to target");
        Assert.Equal(0.0, engine.CurrentVelocity, 9);   // settled: velocity zeroed
    }

    // ---- sudden jump: the anti-jump guarantee --------------------------------

    [Fact]
    public void SuddenJump_IsRateLimited_NeverInstant()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);           // defaults: maxV = 2.0/s
        engine.Update(Raw(0.0));
        engine.Update(Raw(1.0));                           // discontinuity

        // One 16.7 ms frame at the cap moves at most maxV * dt ≈ 0.0334:
        clock.AdvanceMilliseconds(16.7);
        engine.Tick();
        Assert.InRange(engine.CurrentProgress, 0.0, 0.06);
        Assert.True(engine.CurrentProgress < 0.5, "a jump must NOT teleport in one frame");

        // Full travel takes ≳ 1/maxV = 0.5 s: after only 0.25 s we are not there.
        clock.AdvanceMilliseconds(250);
        engine.Tick();
        Assert.True(engine.CurrentProgress < 0.999, "0.25 s < 0.5 s minimum travel time");
    }

    [Fact]
    public void SuddenJump_ReachesTarget_AtRateCapSpeed()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));
        engine.Update(Raw(1.0));

        // During the capped phase each ~16.7ms step advances ≈ maxV * dt.
        clock.AdvanceMilliseconds(16.7);
        engine.Tick();
        double expectedCapStep = 2.0 * 0.0167;
        Assert.True(Math.Abs(engine.CurrentProgress - expectedCapStep) < 0.01,
            $"expected ≈ cap-limited step, got {engine.CurrentProgress}");
    }

    // ---- round trips ---------------------------------------------------------

    [Fact]
    public void RoundTrip_ReversesCleanly_NoOvershoot_NoClampBreak()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));
        engine.Update(Raw(1.0));

        // Go halfway (0.3 s > 0.25 s at cap 2.0/s), then reverse to 0.
        clock.AdvanceMilliseconds(300);
        engine.Tick();
        double half = engine.CurrentProgress;
        Assert.InRange(half, 0.4, 0.75);

        engine.Update(Raw(0.0));
        bool sawDownward = false;
        double previous = half;
        for (int i = 0; i < 600; i++)
        {
            clock.AdvanceMilliseconds(5);
            engine.Tick();
            double p = engine.CurrentProgress;
            Assert.InRange(p, 0.0, 1.0);
            if (p < previous - 1e-9) sawDownward = true;
            previous = p;
        }

        Assert.True(sawDownward, "must move back down toward 0");
        Assert.True(Math.Abs(engine.CurrentProgress) < 1e-3, "must converge back to 0");
        Assert.Equal(0.0, engine.CurrentVelocity, 9);
    }

    // ---- M2.3: the two operations a user actually does by hand --------------
    //
    // "mid stop" and "mid reverse" are the two gestures M2.3 has to sign off
    // on, and they are the only two where the engine is asked to do something
    // other than run to an endpoint. Both are asserted here as PROPERTIES, not
    // as expected numbers: a rate-limited exponential approach that passes
    // these is smooth for ANY tuning of tau / cap, so the assertions survive
    // M2.3 changing the defaults.

    [Fact]
    public void MidStop_SettlesOntoTheHeldTarget_NoOvershootNoOscillation()
    {
        // Lid stops at half way: the target stops moving while the smoothed
        // value is still catching up. Expected: it closes the remaining gap,
        // touches the target, and then stays there - no overshoot (a springy
        // look), no ringing (a flicker at rest).
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));
        engine.Update(Raw(1.0));

        clock.AdvanceMilliseconds(150);
        engine.Tick();
        double mid = engine.CurrentProgress;
        Assert.InRange(mid, 0.20, 0.45);   // still well short of the stop point

        // The lid stops at 0.5.
        engine.Update(Raw(0.5));

        double previous = mid;
        for (int i = 0; i < 800; i++)
        {
            clock.AdvanceMilliseconds(5);
            engine.Tick();
            double p = engine.CurrentProgress;

            Assert.True(p >= previous - 1e-9,
                $"must not fall back while approaching the held target (step {i}: {previous} -> {p})");
            Assert.True(p <= 0.5 + 1e-9,
                $"must not overshoot the held target (step {i}: {p})");
            previous = p;
        }

        // Converged exactly onto the stop point, and dead still.
        Assert.Equal(0.5, engine.CurrentProgress, 6);
        Assert.Equal(0.0, engine.CurrentVelocity, 9);

        // ...and it stays still: the render loop keeps ticking with no input.
        for (int i = 0; i < 100; i++)
        {
            clock.AdvanceMilliseconds(33);
            engine.Tick();
            Assert.Equal(0.5, engine.CurrentProgress, 6);
        }
    }

    [Fact]
    public void MidReverse_TurnsExactlyOnce_AndNeverStepsOutsideTheRateCap()
    {
        // Lid closes, then the user opens it again mid-way. Expected: the
        // direction changes ONCE, and no single step moves faster than the
        // rate cap allows - i.e. the reversal is a turn, not a jump. A sign
        // flip twice would be a visible shudder.
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));
        engine.Update(Raw(1.0));

        clock.AdvanceMilliseconds(300);
        engine.Tick();
        double peak = engine.CurrentProgress;
        Assert.InRange(peak, 0.40, 0.75);

        engine.Update(Raw(0.0));   // the user changes their mind here

        const double dtSeconds = 0.005;
        const double cap = 2.0;    // AnimationEngineOptions.MaxProgressPerSecond
        double previous = peak;
        int signFlips = 0;
        // The rise that got us here was the FIRST direction; without seeding it
        // the post-reversal motion would look like "one direction, no turn".
        int lastSign = 1;

        for (int i = 0; i < 800; i++)
        {
            clock.AdvanceMilliseconds(5);
            engine.Tick();
            double p = engine.CurrentProgress;

            double delta = p - previous;
            Assert.True(Math.Abs(delta) <= cap * dtSeconds * 1.05 + 1e-9,
                $"step {i} moved {delta:0.0000} in {dtSeconds}s - faster than the rate cap");
            Assert.InRange(p, 0.0, peak + 1e-9);

            int sign = Math.Sign(Math.Round(delta, 9));
            if (sign != 0 && lastSign != 0 && sign != lastSign)
            {
                signFlips++;
            }
            if (sign != 0)
            {
                lastSign = sign;
            }

            previous = p;
        }

        Assert.Equal(1, signFlips);                       // one turn, no shudder
        Assert.True(previous < 1e-3, "must finish back at 0");
        Assert.Equal(0.0, engine.CurrentVelocity, 9);
    }

    [Fact]
    public void SlowRamp_IsTrackedMonotonically_WithBoundedLag()
    {
        // Slow lid movement: 0.1 progress per second, sampled at 100 Hz for
        // 5 s. The smoothed value must follow without ever reversing, and the
        // lag must stay bounded (a lag that grows means the engine is being
        // dragged, which reads as "the effect is behind my hand").
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));

        double previous = 0.0;
        double worstLag = 0.0;
        for (int i = 1; i <= 500; i++)
        {
            double target = 0.1 * (i * 0.01);   // 0.1 / s
            engine.Update(Raw(target));
            clock.AdvanceMilliseconds(10);
            LidState s = engine.Tick();

            Assert.True(s.Progress >= previous - 1e-9,
                $"step {i}: slow ramp must never reverse ({previous} -> {s.Progress})");
            previous = s.Progress;

            double lag = target - s.Progress;
            Assert.True(lag >= -1e-9, $"step {i}: must not run ahead of the hand (lag {lag})");
            worstLag = Math.Max(worstLag, lag);
        }

        // A 0.1/s ramp through a tau=0.10 s filter settles at lag = rate*tau
        // = 0.01; allow 3x for the discrete-time integration.
        Assert.True(worstLag < 0.03, $"steady-state lag {worstLag:0.0000} too large");
        Assert.InRange(engine.CurrentProgress, 0.47, 0.50);
    }

    // ---- extreme inputs -------------------------------------------------------

    [Fact]
    public void ExtremeInputs_AreClamped()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);

        engine.Update(Raw(1.5));
        Assert.Equal(1.0, engine.CurrentProgress, 9);   // first update snaps to the clamped target

        // Second update: target clamps to 0, but anti-jump means the engine
        // SLEWS there (dt=0 here moves nothing yet). Advance and converge.
        engine.Update(Raw(-0.5));
        Assert.InRange(engine.CurrentProgress, 0.0, 1.0);
        for (int i = 0; i < 600; i++)
        {
            clock.AdvanceMilliseconds(5);
            engine.Tick();
        }
        Assert.True(Math.Abs(engine.CurrentProgress) < 1e-3, "must converge to clamped target 0");
    }

    [Fact]
    public void NaNInput_IsIgnored_TargetUnchanged()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.4));

        engine.Update(Raw(double.NaN));

        Assert.Equal(0.4, engine.CurrentProgress, 9);
    }

    // ---- irregular time steps --------------------------------------------------

    [Fact]
    public void IrregularTimeSteps_StayStable_AndClampHugeGaps()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));
        engine.Update(Raw(1.0));

        double previous = 0;
        foreach (double dtMs in new[] { 1.0, 200.0, 5.0, 50.0, 0.0, 5000.0, 10.0, 10.0, 10.0 })
        {
            clock.AdvanceMilliseconds(dtMs);
            engine.Tick();
            double p = engine.CurrentProgress;

            Assert.False(double.IsNaN(p), "dt anomalies must not poison the state");
            Assert.InRange(p, 0.0, 1.0);
            Assert.True(p >= previous - 1e-9, "never backwards toward a forward target");
            previous = p;
        }

        // The 5000 ms gap was clamped to MaxDeltaTimeSeconds (0.25 s) → the
        // single worst-case jump is maxV * maxDt = 2.0 * 0.25 = 0.5, and the
        // subsequent steps keep converging normally.
        Assert.True(engine.CurrentProgress > previous - 1e-9);
    }

    [Fact]
    public void ZeroAndNegativeDeltaTime_DoNotMoveState()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));
        engine.Update(Raw(0.5));

        clock.AdvanceMilliseconds(0);
        engine.Tick();
        Assert.Equal(0.0, engine.CurrentProgress, 9);

        clock.AdvanceMilliseconds(-100);   // clock anomaly
        engine.Tick();
        Assert.Equal(0.0, engine.CurrentProgress, 9);
    }

    // ---- velocity (M1.1 kept it at 0; M1.3 makes it real) ------------------------

    [Fact]
    public void Velocity_IsRealWhileSlewing_AndZeroAfterArrival()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));
        engine.Update(Raw(1.0));

        clock.AdvanceMilliseconds(16.7);
        engine.Tick();
        Assert.Equal(2.0, engine.CurrentVelocity, 3);   // capped slew rate, positive = closing

        // Arrive: 1.0 travel at cap 2.0/s needs ≥ 0.5 s; step in ≤0.25 s
        // chunks (MaxDeltaTimeSeconds clamp) plus settle margin.
        for (int i = 0; i < 40; i++)
        {
            clock.AdvanceMilliseconds(40);
            engine.Tick();
        }
        Assert.True(Math.Abs(engine.CurrentProgress - 1.0) < 1e-3);
        Assert.Equal(0.0, engine.CurrentVelocity, 9);
    }

    [Fact]
    public void Velocity_SignFlips_WhenTargetReverses()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));
        engine.Update(Raw(1.0));
        clock.AdvanceMilliseconds(50);
        engine.Tick();
        Assert.True(engine.CurrentVelocity > 0);

        engine.Update(Raw(0.0));
        clock.AdvanceMilliseconds(16.7);
        engine.Tick();
        Assert.True(engine.CurrentVelocity < 0, "reverse target must flip velocity sign");
    }

    // ---- output state semantics ---------------------------------------------------

    [Fact]
    public void Angle_FollowsIllustrativeMapping_OfSmoothedProgress()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));
        engine.Update(Raw(1.0));

        clock.AdvanceMilliseconds(16.7);
        LidState s = engine.Tick();

        double expectedAngle = 180.0 - 150.0 * s.Progress;   // PROJECT_SPEC §6
        Assert.Equal(expectedAngle, s.Angle, 6);
    }

    [Fact]
    public void SourceAndConfidence_PassThrough_FromRawInput()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);

        engine.Update(Raw(0.5, LidStateSource.Camera, 0.42));
        clock.AdvanceMilliseconds(10);
        LidState s = engine.Tick();

        Assert.Equal(LidStateSource.Camera, s.Source);
        Assert.Equal(0.42, s.Confidence, 9);
    }

    [Fact]
    public void Output_AlwaysWithinUnitInterval_AfterChaoticSequence()
    {
        var clock = new ManualTimeSource();
        var engine = new AnimationEngine(clock);
        engine.Update(Raw(0.0));

        var random = new Random(1234);   // fixed seed: deterministic
        for (int i = 0; i < 500; i++)
        {
            double raw = random.NextDouble() * 2.0 - 0.5;   // -0.5 .. 1.5
            engine.Update(Raw(raw));
            clock.AdvanceMilliseconds(random.Next(0, 40));
            LidState s = engine.Tick();

            Assert.InRange(s.Progress, 0.0, 1.0);
            Assert.InRange(s.Velocity, -2.0, 2.0);
            Assert.InRange(s.Angle, 30.0, 180.0);
        }
    }
}
