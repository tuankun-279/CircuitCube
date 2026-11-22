using System;
using System.Collections.Generic;

namespace CircuitCube.Core
{
    /// <summary>
    /// Leaderboard scores as single numbers, shared by the game and the Cloud Code module on the server.
    /// <para>Weekly challenge boards sort ascending: fewest moves first, then the shorter time.
    /// Score = moves × <see cref="WeeklyStep"/> + time in tenths of a second.</para>
    /// <para>Shape boards sort descending: most Expert levels in the shape first, then the fewest total moves of their
    /// best runs, then the shorter total time. Score = Expert count × <see cref="ShapeStep"/> − total moves ×
    /// <see cref="ShapeMoveStep"/> − total whole seconds. All parts fit in a double exactly (below 2^53).</para>
    /// </summary>
    public static class RankScore
    {
        /// <summary>Longest time a weekly level can record, in tenths of a second (about 2.7 hours).</summary>
        public const int MaxTenths = 99999;
        public const double WeeklyStep = 100000d;
        /// <summary>Most Expert levels one shape board can count.</summary>
        public const int MaxShapeExperts = 9000;
        /// <summary>Largest total of best moves a shape board can record.</summary>
        public const long MaxShapeMoves = 999999L;
        /// <summary>Longest total a shape board can record, in whole seconds (about 11.5 days of play).</summary>
        public const long MaxShapeSeconds = 999999L;
        public const double ShapeStep = 1000000000000d;
        public const double ShapeMoveStep = 1000000d;

        public static int Tenths(float seconds) => (int)Math.Max(0, Math.Min(MaxTenths, Math.Round(seconds * 10.0)));

        public static double Weekly(int moves, int tenths) => Math.Max(0, moves) * WeeklyStep + Math.Max(0, Math.Min(MaxTenths, tenths));
        public static int WeeklyMoves(double score) => (int)Math.Floor(score / WeeklyStep);
        public static int WeeklyTenths(double score) => (int)Math.Round(score - WeeklyMoves(score) * WeeklyStep);

        public static double Shape(int experts, long totalMoves, long totalTenths)
        {
            long e = Math.Max(0, Math.Min(MaxShapeExperts, experts));
            long m = Math.Max(0L, Math.Min(MaxShapeMoves, totalMoves));
            long sec = Math.Max(0L, Math.Min(MaxShapeSeconds, totalTenths / 10));
            return e * ShapeStep - m * ShapeMoveStep - sec;
        }

        public static int ShapeExperts(double score) => score <= 0 ? 0 : (int)Math.Ceiling(score / ShapeStep);
        static long ShapeRest(double score) => score <= 0 ? 0 : (long)Math.Round(ShapeExperts(score) * ShapeStep - score);
        public static long ShapeMoves(double score) => ShapeRest(score) / (long)ShapeMoveStep;
        /// <summary>Total time of a shape score, in whole seconds.</summary>
        public static long ShapeSeconds(double score) => ShapeRest(score) % (long)ShapeMoveStep;

        /// <summary>"1:23.4" from tenths of a second.</summary>
        public static string TimeText(long tenths)
        {
            long t = Math.Max(0, tenths), s = t / 10;
            return (s / 60).ToString() + ":" + (s % 60).ToString("D2") + "." + (t % 10).ToString();
        }

        /// <summary>Leaderboard id of weekly challenge level <paramref name="slot"/> (1..5).</summary>
        public static string WeeklyBoard(int slot) => "weekly_" + slot;
        public const string WeeklyTotalBoard = "weekly_total";
        /// <summary>Leaderboard id of a shape (group id, e.g. "cube").</summary>
        public static string ShapeBoard(string group) => "shape_" + group;
    }

    /// <summary>What the replay of a won level found.</summary>
    public struct RunResult
    {
        public bool Valid;
        public string Error;
        public int Moves, MinTaps;
        public bool Expert;
    }

    /// <summary>
    /// Checks a win by playing its taps again from the level's starting board: every tap turns one unlocked tile one
    /// step, and the board must be solved at the end. Runs in the game (tests) and on the server, so a score is only
    /// posted for a board that really was solved in that many moves. Only wins without hints and undos are sent, so the
    /// taps are the whole game.
    /// </summary>
    public static class RunCheck
    {
        /// <summary>Fastest believable play, in tenths of a second per tap (0.15 s).</summary>
        public const int MinTenthsPer2Taps = 3;

        /// <summary>The board of a level file; null if its shape or tiles do not fit.</summary>
        public static CubeLevel Build(LevelData d, Func<string, VoxelShapeFile> voxels)
        {
            if (d == null) return null;
            var topo = ShapeResolver.Build(d.shape, voxels);
            if (topo == null) return null;
            var tiles = TileCodec.Decode(d.tiles, topo);
            if (tiles == null) return null;
            if (d.locked != null) foreach (int i in d.locked) if (i >= 0 && i < tiles.Length) tiles[i].Locked = true;
            return new CubeLevel { Topo = topo, Tiles = tiles, Par = d.par, Colors = TileCodec.LampColors(tiles) };
        }

        public static RunResult Replay(CubeLevel level, IList<int> taps, int tenths, GameRules rules, GroupData group = null)
        {
            var r = new RunResult();
            if (level == null) { r.Error = "level"; return r; }
            if (taps == null || taps.Count == 0) { r.Error = "taps"; return r; }
            var lv = level.Clone();
            var start = new int[lv.Tiles.Length];
            for (int i = 0; i < start.Length; i++) start[i] = lv.Tiles[i].Rot;
            foreach (int i in taps)
            {
                if (i < 0 || i >= lv.Tiles.Length || lv.Tiles[i].Locked) { r.Error = "tap"; return r; }
                lv.Tiles[i].Rot++;
            }
            if (!lv.IsSolved(false)) { r.Error = "unsolved"; return r; }
            r.Moves = taps.Count;
            if (tenths < (r.Moves * MinTenthsPer2Taps) / 2) { r.Error = "fast"; return r; }
            r.MinTaps = LevelScore.MinTaps(lv.Tiles, start);
            r.Expert = r.Moves <= LevelScore.ExpertLimit(r.MinTaps, rules ?? new GameRules(), group);
            r.Valid = true;
            return r;
        }
    }
}
