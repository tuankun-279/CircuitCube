using System;
using System.IO;
using CircuitCube.Core;
using CircuitCube.Game;
using NUnit.Framework;

namespace CircuitCube.Tests
{
    /// <summary>The play clock of a weekly level runs over all its attempts and never goes back.</summary>
    public class WeeklyClockTests
    {
        [Test]
        public void ClockGoesOnAcrossAttempts()
        {
            var clocks = new WeeklyClocks();
            var first = clocks.Get("2026-W48", "cube", 1);
            Assert.AreEqual(0f, first.seconds);
            Assert.IsTrue(WeeklyClocks.Raise(first, 12.5f));
            var again = clocks.Get("2026-W48", "cube", 1);
            Assert.AreSame(first, again);
            Assert.AreEqual(12.5f, again.seconds);
        }

        [Test]
        public void ClockNeverGoesBack()
        {
            var e = new WeeklyClocks().Get("w", "cube", 1);
            WeeklyClocks.Raise(e, 30f);
            Assert.IsFalse(WeeklyClocks.Raise(e, 10f));
            Assert.IsFalse(WeeklyClocks.Raise(e, 30f));
            Assert.IsFalse(WeeklyClocks.Raise(e, float.NaN));
            Assert.IsFalse(WeeklyClocks.Raise(null, 99f));
            Assert.AreEqual(30f, e.seconds);
        }

        [Test]
        public void EveryLevelHasItsOwnClock()
        {
            var clocks = new WeeklyClocks();
            WeeklyClocks.Raise(clocks.Get("w", "cube", 1), 5f);
            Assert.AreEqual(0f, clocks.Get("w", "cube", 2).seconds);
            Assert.AreEqual(0f, clocks.Get("w", "tetra", 1).seconds);
            Assert.AreEqual(0f, clocks.Get("w2", "cube", 1).seconds);
            Assert.AreEqual(5f, clocks.Get("w", "cube", 1).seconds);
        }

        [Test]
        public void ANewWeekDropsTheOldClocks()
        {
            var clocks = new WeeklyClocks();
            WeeklyClocks.Raise(clocks.Get("2026-W48", "cube", 1), 5f);
            Assert.IsFalse(clocks.KeepOnly("2026-W48"));
            Assert.IsTrue(clocks.KeepOnly("2026-W49"));
            Assert.AreEqual(0, clocks.entries.Count);
        }

        [Test]
        public void ServerCountsTheHighestTimeSeen()
        {
            Assert.AreEqual(1, WeeklyClocks.Counted(0, 1));
            Assert.AreEqual(300, WeeklyClocks.Counted(300, 299));
            Assert.AreEqual(300, WeeklyClocks.Counted(300, 300));
            Assert.AreEqual(301, WeeklyClocks.Counted(300, 301));
            Assert.AreEqual(RankScore.MaxTenths, WeeklyClocks.Counted(300, int.MaxValue));
        }

        [Test]
        public void AnEarlierTimeStillWinsWithFewerMovesOnly()
        {
            double best = RankScore.Weekly(20, 300);
            Assert.AreEqual(best, RankScore.Weekly(20, WeeklyClocks.Counted(300, 50)));
            Assert.Less(RankScore.Weekly(19, WeeklyClocks.Counted(300, 50)), best);
        }

        [Test]
        public void ALaterRunOnlyWinsWithFewerMoves()
        {
            Assert.Greater(RankScore.Weekly(20, 500), RankScore.Weekly(20, 300));
            Assert.Less(RankScore.Weekly(19, 500), RankScore.Weekly(20, 300));
        }

        [Test]
        public void ClocksSurviveARestartOfTheGame()
        {
            string old = CubeSave.Root, dir = Path.Combine(Path.GetTempPath(), "cc-weekly-" + Guid.NewGuid().ToString("N"));
            CubeSave.Root = dir;
            CubeSave.Unload();
            try
            {
                WeeklyClocks.Raise(CubeSave.WeeklyClocks.Get("2026-W48", "cube", 2), 61.25f);
                CubeSave.SaveWeeklyClocks();
                CubeSave.Unload();
                Assert.AreEqual(61.25f, CubeSave.WeeklyClocks.Get("2026-W48", "cube", 2).seconds);
            }
            finally
            {
                CubeSave.Root = old;
                CubeSave.Unload();
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
