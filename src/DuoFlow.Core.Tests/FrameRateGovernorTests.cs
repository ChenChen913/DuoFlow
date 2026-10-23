using DuoFlow.Vision;
using Xunit;

namespace DuoFlow.Core.Tests;

/// <summary>
/// M3.1: frame throttling. The failure mode worth guarding against is not
/// "drops too few" (that only costs battery) but "drops too many" - a 10 fps
/// target that silently becomes 5 fps because the timestamps jitter.
/// </summary>
public class FrameRateGovernorTests
{
    [Fact]
    public void ThirtyFpsStream_IsThrottledToTen()
    {
        var governor = new FrameRateGovernor(10.0);
        int accepted = 0;

        // 90 frames at exactly 1/30 s = 3 seconds of stream.
        for (int i = 0; i < 90; i++)
        {
            if (governor.Accept(i / 30.0))
            {
                accepted++;
            }
        }

        // 3 seconds at 10 fps = 30 frames, give or take the first-frame edge.
        Assert.InRange(accepted, 29, 31);
        Assert.InRange(governor.DropRatio, 0.60, 0.70);
    }

    [Fact]
    public void TenFpsStreamWithJitter_IsNotHalved()
    {
        // The whole reason for the 0.9 slack: real timestamps arrive a few ms
        // early or late. Without the slack this accepts every OTHER frame.
        var governor = new FrameRateGovernor(10.0);
        var random = new Random(7);
        int accepted = 0;

        for (int i = 0; i < 50; i++)
        {
            double jitter = (random.NextDouble() - 0.5) * 0.006;   // +/- 3 ms
            if (governor.Accept(i / 10.0 + jitter))
            {
                accepted++;
            }
        }

        Assert.Equal(50, accepted);
    }

    [Fact]
    public void ZeroTarget_AcceptsEverything()
    {
        var governor = new FrameRateGovernor(0.0);
        for (int i = 0; i < 100; i++)
        {
            Assert.True(governor.Accept(i / 60.0));
        }
        Assert.Equal(100, governor.Accepted);
        Assert.Equal(0, governor.Dropped);
    }

    [Fact]
    public void ChangingTheTarget_TakesEffectImmediately()
    {
        var governor = new FrameRateGovernor(10.0);
        Assert.True(governor.Accept(0.0));

        // At 10 fps the next frame is due at 0.1 s...
        governor.TargetFps = 1.0;
        Assert.False(governor.Accept(0.05));
        // ...but at 1 fps it is not due until 0.9 s. If the change had waited
        // for the OLD interval to expire, 0.05 s would have been accepted.
        Assert.False(governor.Accept(0.2));
        Assert.True(governor.Accept(0.95));
    }

    [Fact]
    public void ResetClock_MakesTheNextFrameDue()
    {
        var governor = new FrameRateGovernor(10.0);
        Assert.True(governor.Accept(0.0));
        Assert.False(governor.Accept(0.01));

        governor.ResetClock();
        Assert.True(governor.Accept(0.01));
    }

    [Fact]
    public void NonFiniteTimestamps_AreRejected_AndCountedAsDropped()
    {
        var governor = new FrameRateGovernor(10.0);
        Assert.False(governor.Accept(double.NaN));
        Assert.False(governor.Accept(double.PositiveInfinity));
        Assert.Equal(0, governor.Accepted);
    }

    [Fact]
    public void InvalidConfiguration_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRateGovernor(-1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRateGovernor(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRateGovernor(10.0, slack: 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRateGovernor(10.0, slack: 1.5));

        var governor = new FrameRateGovernor(10.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => governor.TargetFps = -5.0);
    }
}
