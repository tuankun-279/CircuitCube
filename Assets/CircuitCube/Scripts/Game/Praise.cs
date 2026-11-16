using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Praise and encouragement: short lines on the result card (in place of a streak count), and the mascot's lines in the bottom corner during play. Lines
    /// are English keys for <see cref="Loc"/>; <see cref="Pick"/> avoids saying the same line twice in a row.
    /// </summary>
    public static class Praise
    {
        /// <summary>Result card: an Expert win (par, no hint, no undo).</summary>
        public static readonly string[] Expert = { "Brilliant!", "Flawless!", "Genius!" };

        /// <summary>Result card: Expert wins in a row (the comet effect).</summary>
        public static readonly string[] ExpertStreak = { "Unstoppable!", "Legendary!", "On fire!" };

        /// <summary>Result card: three-star wins in a row, Expert counted as three stars (the constellation effect).</summary>
        public static readonly string[] StarStreak = { "Shining!", "Amazing!", "Great run!" };

        /// <summary>Result card: three stars won with a hint or an undo.</summary>
        public static readonly string[] ThreeStars = { "Great job!", "Well played!", "Smooth!" };

        /// <summary>Result card: fewer than three stars.</summary>
        public static readonly string[] Encourage = { "Well done!", "Good job!", "Keep going!" };

        /// <summary>Mascot, during a hard level (many moves, a restart, a long time).</summary>
        public static readonly string[] Cheer = { "Take your time, the circuit will click.", "Try one colour at a time.", "Spin the shape for a fresh look.", "Stuck? A hint lights the way." };

        /// <summary>Mascot, now and then when a lamp lights.</summary>
        public static readonly string[] Lit = { "That's it!", "Nice!", "Lit!" };

        /// <summary>Mascot, when two lamps or fewer are left.</summary>
        public static readonly string[] NearlyDone = { "Almost there!", "Just a few more!" };

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
