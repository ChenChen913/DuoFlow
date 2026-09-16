namespace DuoFlow.Core;

/// <summary>
/// Injectable monotonic time source for the M1.3 Animation Engine.
/// The engine NEVER reads DateTime.Now / Environment.TickCount directly -
/// tests inject a <see cref="ManualTimeSource"/> to stay deterministic.
/// </summary>
public interface ITimeSource
{
    /// <summary>Monotonic timestamp in TimeSpan ticks (100 ns units).</summary>
    long GetTimestampTicks();
}

/// <summary>Production time source backed by a high-resolution stopwatch.</summary>
public sealed class StopwatchTimeSource : ITimeSource
{
    private readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>Elapsed as TimeSpan ticks (100 ns) - Stopwatch.Elapsed.Ticks is
    /// already frequency-normalized, unlike ElapsedTicks.</summary>
    public long GetTimestampTicks() => _stopwatch.Elapsed.Ticks;
}

/// <summary>Test time source: the test owns the clock and advances it by hand.</summary>
public sealed class ManualTimeSource : ITimeSource
{
    public long NowTicks { get; set; }

    public long GetTimestampTicks() => NowTicks;

    public void Advance(TimeSpan time) => NowTicks += time.Ticks;

    public void AdvanceMilliseconds(double milliseconds)
        => NowTicks += (long)(milliseconds * TimeSpan.TicksPerMillisecond);
}
