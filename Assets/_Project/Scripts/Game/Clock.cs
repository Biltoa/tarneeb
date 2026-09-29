using System.Diagnostics;

namespace Tarneeb.Game
{
    /// <summary>Wall-clock seconds that also advance outside Play mode (unit tests) and ignore Time.timeScale.</summary>
    public static class Clock
    {
        private static readonly double Frequency = Stopwatch.Frequency;

        public static double Seconds => Stopwatch.GetTimestamp() / Frequency;
    }
}
