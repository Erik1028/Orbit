using System.Diagnostics;

namespace Orbit;

// Seconds from a high-resolution counter. Environment.TickCount64 only advances every ~15.6 ms,
// which at 90 Hz made frame times alternate between 0 and 15.6 ms (jumpy smoothing/animation).
static class Clock
{
    static readonly double Inv = 1.0 / Stopwatch.Frequency;
    public static double Now => Stopwatch.GetTimestamp() * Inv;
}
