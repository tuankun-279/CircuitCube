using System.Collections.Generic;
using CircuitCube.Core;
using CircuitCube.Game;
using NUnit.Framework;

namespace CircuitCube.Tests
{
    /// <summary>Leaderboard scores and the replay the server runs before it posts one.</summary>
    public class RankTests
    {
        /// <summary>The fewest taps that solve the level: each tile turned back to its solved wire.</summary>
        static List<int> SolvingTaps(CubeLevel lv)
        {
            var taps = new List<int>();
            for (int i = 0; i < lv.Tiles.Length; i++)
                for (int k = Circuit.TapsToSolve(lv.Tiles[i]); k > 0; k--) taps.Add(i);
            return taps;
        }

        [Test]
        public void WeeklyScoreRanksFewerMovesFirstThenTime()
        {
            Assert.Less(RankScore.Weekly(20, RankScore.MaxTenths), RankScore.Weekly(21, 0));
            Assert.Less(RankScore.Weekly(20, 150), RankScore.Weekly(20, 151));
            double s = RankScore.Weekly(37, 1234);
            Assert.AreEqual(37, RankScore.WeeklyMoves(s));
            Assert.AreEqual(1234, RankScore.WeeklyTenths(s));
            Assert.AreEqual(RankScore.Weekly(5, RankScore.MaxTenths), RankScore.Weekly(5, RankScore.MaxTenths + 500));
        }

        [Test]
        public void ShapeScoreRanksMoreExpertsFirstThenShorterTotal()
        {
            Assert.Greater(RankScore.Shape(11, RankScore.MaxShapeTenths), RankScore.Shape(10, 0));
            Assert.Greater(RankScore.Shape(10, 500), RankScore.Shape(10, 501));
            foreach (var (e, t) in new[] { (1, 0L), (1, 9L), (42, 123456L), (600, RankScore.MaxShapeTenths) })
            {
                double s = RankScore.Shape(e, t);
                Assert.AreEqual(e, RankScore.ShapeExperts(s), "experts of " + e + "/" + t);
                Assert.AreEqual(t, RankScore.ShapeTenths(s), "tenths of " + e + "/" + t);
            }
            Assert.AreEqual(0, RankScore.ShapeExperts(0));
        }

        [Test]
        public void TimeTextReadsMinutesSecondsTenths()
        {
            Assert.AreEqual("0:07.5", RankScore.TimeText(75));
            Assert.AreEqual("12:03.0", RankScore.TimeText(7230));
        }

        [Test]
        public void FewestTapsReplayIsAValidExpertWin()
        {
            var lv = LevelPlan.Build(0, 12);
            var taps = SolvingTaps(lv);
            Assume.That(taps.Count, Is.GreaterThan(0));
            var r = RunCheck.Replay(lv, taps, taps.Count * 10, new GameRules());
            Assert.IsTrue(r.Valid, r.Error);
            Assert.IsTrue(r.Expert);
            Assert.AreEqual(taps.Count, r.Moves);
            Assert.AreEqual(taps.Count, r.MinTaps);
        }

        [Test]
        public void ReplayDoesNotTouchTheLevel()
        {
            var lv = LevelPlan.Build(0, 12);
            var before = TileCodec.Encode(lv.Tiles);
            RunCheck.Replay(lv, SolvingTaps(lv), 9999, new GameRules());
            Assert.AreEqual(before, TileCodec.Encode(lv.Tiles));
        }

        [Test]
        public void WastedMovesPastTheAllowanceAreNotExpert()
        {
            var lv = LevelPlan.Build(0, 12);
            var taps = SolvingTaps(lv);
            var rules = new GameRules { wasteAllowed = 3 };
            int t0 = 0, ports = lv.Tiles[t0].Ports;
            var waste = new List<int>(taps);
            for (int k = 0; k < ports; k++) waste.Add(t0);   // a full turn of one tile changes nothing but costs moves
            var r = RunCheck.Replay(lv, waste, 9999, rules);
            Assert.IsTrue(r.Valid, r.Error);
            Assert.AreEqual(ports <= 3, r.Expert);
        }

        [Test]
        public void UnsolvedBoardIsRefused()
        {
            var lv = LevelPlan.Build(0, 12);
            var taps = SolvingTaps(lv);
            taps.RemoveAt(taps.Count - 1);
            if (taps.Count == 0) taps.Add(0);
            var r = RunCheck.Replay(lv, taps, 9999, new GameRules());
            Assert.IsFalse(r.Valid);
        }

        [Test]
        public void TapOnALockedOrMissingTileIsRefused()
        {
            var lv = LevelPlan.Build(0, 12);
            var taps = SolvingTaps(lv);
            var r = RunCheck.Replay(lv, new List<int>(taps) { lv.Tiles.Length }, 9999, new GameRules());
            Assert.AreEqual("tap", r.Error);
            var locked = lv.Clone();
            locked.Tiles[taps[0]].Locked = true;
            Assert.AreEqual("tap", RunCheck.Replay(locked, taps, 9999, new GameRules()).Error);
        }

        [Test]
        public void ImpossiblyFastPlayIsRefused()
        {
            var lv = LevelPlan.Build(0, 12);
            var taps = SolvingTaps(lv);
            Assume.That(taps.Count, Is.GreaterThan(2));
            Assert.AreEqual("fast", RunCheck.Replay(lv, taps, 0, new GameRules()).Error);
        }

        [Test]
        public void LevelFileBuildsTheSameBoardAsTheGame()
        {
            var lv = LevelPlan.Build(0, 12);
            var d = new LevelData { shape = "cube-3", tiles = TileCodec.Encode(lv.Tiles), par = lv.Par };
            var built = RunCheck.Build(d, _ => null);
            Assume.That(built, Is.Not.Null, "cube-3 shape id");
            Assert.AreEqual(d.tiles, TileCodec.Encode(built.Tiles));
        }

        [Test]
        public void PlayerNamesAreCleaned()
        {
            Assert.AreEqual("Tuan_Pro", NameFilter.Clean("  Tuan Pro!! "));
            Assert.AreEqual("Player", NameFilter.Clean("   "));
            Assert.AreEqual("Player", NameFilter.Clean("xfuckx"));
            Assert.AreEqual("Player", NameFilter.Clean("Đm"));
            Assert.AreEqual("Colonel", NameFilter.Clean("Colonel"));   // "lon" inside a word is fine
            Assert.AreEqual(20, NameFilter.Clean(new string('a', 40)).Length);
        }
    }
}
