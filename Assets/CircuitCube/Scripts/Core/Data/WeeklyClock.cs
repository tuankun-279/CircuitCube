using System;
using System.Collections.Generic;

namespace CircuitCube.Core
{
    /// <summary>Play time of one weekly challenge level, over all its attempts.</summary>
    [Serializable] public class WeeklyClockEntry
    {
        public string week = "", group = "";
        public int slot;
        public float seconds;
    }

    /// <summary>
    /// The play clocks of the weekly challenge levels. A level's clock never goes back: it keeps counting over restarts,
    /// "Try again" and leaving the game, so a player who finds the best solution on the first try ranks above one who
    /// practised it. Only a new week starts from zero.
    /// </summary>
    [Serializable] public class WeeklyClocks
    {
        public List<WeeklyClockEntry> entries = new List<WeeklyClockEntry>();

        /// <summary>The clock of a level; a new one starts at zero.</summary>
        public WeeklyClockEntry Get(string week, string group, int slot)
        {
            foreach (var e in entries)
                if (e.slot == slot && e.group == group && e.week == week) return e;
            var made = new WeeklyClockEntry { week = week, group = group, slot = slot };
            entries.Add(made);
            return made;
        }

        /// <summary>Forget every other week. Returns true when something was dropped.</summary>
        public bool KeepOnly(string week) => entries.RemoveAll(e => e.week != week) > 0;

        /// <summary>Moves a clock forward; an earlier time changes nothing.</summary>
        public static bool Raise(WeeklyClockEntry entry, float seconds)
        {
            if (entry == null || float.IsNaN(seconds) || seconds <= entry.seconds) return false;
            entry.seconds = seconds;
            return true;
        }

        /// <summary>
        /// The server's rule: the time that counts for a win is never below the highest one it has seen for that level,
        /// so clearing the app's data or playing on another phone does not turn the clock back.
        /// </summary>
        public static int Counted(int highestTenths, int tenths) => Math.Min(RankScore.MaxTenths, Math.Max(highestTenths, tenths));
    }
}
