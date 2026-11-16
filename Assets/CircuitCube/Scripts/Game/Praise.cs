using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Praise and encouragement shown on the result card (in place of a streak count) and during hard levels. Lines
    /// are English keys for <see cref="Loc"/>; <see cref="Pick"/> avoids saying the same line twice in a row.
    /// </summary>
    public static class Praise
    {
        /// <summary>An Expert win (par, no hint, no undo).</summary>
        public static readonly string[] Expert = { "Brilliant!", "Flawless!", "Pure genius!", "Not a single wasted move!", "Expert work!" };

        /// <summary>Two or more Expert wins in a row (the comet effect).</summary>
        public static readonly string[] ExpertStreak = { "Unstoppable!", "On fire!", "Comet streak!", "Legendary!", "Master of circuits!" };

        /// <summary>Three-star wins in a row, Expert counted as three stars (the constellation effect).</summary>
        public static readonly string[] StarStreak = { "Shining bright!", "Great run!", "Star after star!", "You're a natural!", "Keep it glowing!" };

        /// <summary>Three stars won with a hint or an undo.</summary>
        public static readonly string[] ThreeStars = { "Great job!", "Well played!", "Nicely done!", "All lit up!", "Smooth!" };

        /// <summary>Fewer than three stars: kind words and a nudge towards par.</summary>
        public static readonly string[] Encourage = { "Solved! Try it without help for Expert.", "Every lamp is lit. Well done!", "Good job! Par is within reach.", "Fewer moves next time?", "Step by step, you're getting sharper." };

        /// <summary>During a hard level (many moves, a restart, a long time): a calm tip.</summary>
        public static readonly string[] Cheer = { "Take your time, the circuit will click.", "Tricky one! You've got this.", "Try one colour at a time.", "Spin the shape for a fresh look.", "Stuck? A hint lights the way." };

        static string _last;

        /// <summary>A line from the set in the current language, never the one shown last time.</summary>
        public static string Pick(string[] set)
        {
            if (set == null || set.Length == 0) return "";
            string s = set[Random.Range(0, set.Length)];
            if (s == _last && set.Length > 1) s = set[(System.Array.IndexOf(set, s) + 1) % set.Length];
            _last = s;
            return Loc.T(s);
        }
    }
}
