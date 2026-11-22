using System;
using System.Globalization;

namespace CircuitCube.Core
{
    /// <summary>Numbers and names as the leaderboard screens print them.</summary>
    public static class RankText
    {
        /// <summary>Time left: "2d 14h", "5h 12m", "12m" (never negative).</summary>
        public static string Countdown(TimeSpan left)
        {
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            if (left.Days > 0) return left.Days + "d " + left.Hours + "h";
            if (left.Hours > 0) return left.Hours + "h " + left.Minutes + "m";
            return Math.Max(0, left.Minutes) + "m";
        }

        /// <summary>A shape board total time from whole seconds: "44:40" under an hour, "1h05" from an hour.</summary>
        public static string ShapeTime(long seconds)
        {
            long s = Math.Max(0L, seconds), h = s / 3600, m = s / 60 % 60;
            return h > 0 ? h + "h" + m.ToString("D2") : m + ":" + (s % 60).ToString("D2");
        }

        /// <summary>A count with thousands separators: "1,412".</summary>
        public static string Count(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>"Tuan_Pro#4821" into "Tuan_Pro" and "#4821" (no tag: the whole name and "").</summary>
        public static void SplitTag(string full, out string name, out string tag)
        {
            full = full ?? "";
            int i = full.LastIndexOf('#');
            if (i <= 0) { name = full; tag = ""; return; }
            name = full.Substring(0, i);
            tag = full.Substring(i);
        }
    }
}
