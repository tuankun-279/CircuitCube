using System;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>The level_quit event for one attempt: who left which level, and how far they got.</summary>
    [Serializable]
    public sealed class LevelQuit
    {
        public string shape;
        public int level, serial, rev, cv, moves, hints, undos, seconds;
        /// <summary>When the app went to the background (UTC ticks), 0 when it was not paused.</summary>
        public long pausedAt;

        public (string key, object value)[] Params() => new (string, object)[]
        {
            ("shape", shape), ("level", level), ("serial", serial), ("rev", rev), ("cv", cv),
            ("moves", moves), ("hints", hints), ("undos", undos), ("seconds", seconds),
        };
    }

    /// <summary>
    /// Tracks one attempt at a level so level_quit is sent at most once: only for a started, unfinished attempt with
    /// at least one move. Completing or restarting the level closes the attempt without a quit.
    /// </summary>
    public sealed class LevelAttempt
    {
        /// <summary>Time in the background after which a paused attempt counts as quit.</summary>
        public const double PauseQuitSeconds = 60;

        string _shape;
        int _level, _serial, _rev, _cv;

        public bool Open { get; private set; }

        public void Begin(string shape, int level, int serial, int rev, int cv)
        {
            _shape = shape; _level = level; _serial = serial; _rev = rev; _cv = cv;
            Open = true;
        }

        /// <summary>The level was completed or restarted: no level_quit for this attempt.</summary>
        public void Close() => Open = false;

        /// <summary>The level_quit due if the player left now, without closing the attempt; null when none is due.</summary>
        public LevelQuit Peek(int moves, int hints, int undos, float seconds)
        {
            if (!Open || moves <= 0) return null;
            return new LevelQuit
            {
                shape = _shape, level = _level, serial = _serial, rev = _rev, cv = _cv,
                moves = moves, hints = hints, undos = undos, seconds = Mathf.RoundToInt(seconds),
            };
        }

        /// <summary>The player left the level: returns the level_quit to send (null when none is due) and closes the attempt.</summary>
        public LevelQuit Quit(int moves, int hints, int undos, float seconds)
        {
            var q = Peek(moves, hints, undos, seconds);
            Open = false;
            return q;
        }

        /// <summary>True when a pause that started at <paramref name="pausedAt"/> (UTC ticks) lasted long enough to count as a quit.</summary>
        public static bool PausedTooLong(long pausedAt, DateTime nowUtc) =>
            pausedAt > 0 && (nowUtc - new DateTime(pausedAt, DateTimeKind.Utc)).TotalSeconds > PauseQuitSeconds;
    }
}
