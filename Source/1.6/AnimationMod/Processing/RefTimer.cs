using System.Diagnostics;

namespace AM.Processing;

public readonly ref struct RefTimer
{
    private static readonly double tickFrequency = 1000.0 / Stopwatch.Frequency;

    public static long GetTickNow() => Stopwatch.GetTimestamp();
    public static double ToMilliseconds(long startTick, long endTick) => (endTick - startTick) * tickFrequency;

    private readonly long startTime;

    public RefTimer()
    {
        startTime = GetTickNow();
    }

    public double GetElapsedMilliseconds() => (Stopwatch.GetTimestamp() - startTime) * tickFrequency;

    public void GetElapsedMilliseconds(out double milliseconds) => milliseconds = GetElapsedMilliseconds();

    public void GetElapsedMilliseconds(out float milliseconds) => milliseconds = (float)GetElapsedMilliseconds();

    public void GetElapsedMilliseconds(out long milliseconds) => milliseconds = (long)GetElapsedMilliseconds();
}