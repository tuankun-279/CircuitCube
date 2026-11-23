using System;
using CircuitCube.Game;
using NUnit.Framework;
using UnityEngine;

namespace CircuitCube.Tests
{
    public class LevelAttemptTests
    {
        static LevelAttempt Started()
        {
            var a = new LevelAttempt();
            a.Begin("cube", 12, 34, 2, 7);
            return a;
        }

        [Test]
        public void QuitFiresOnceWithAllParams()
        {
            var a = Started();
            var q = a.Quit(5, 1, 2, 41.6f);
            Assert.IsNotNull(q);
            var p = q.Params();
            CollectionAssert.AreEqual(new[] { "shape", "level", "serial", "rev", "cv", "moves", "hints", "undos", "seconds" }, Array.ConvertAll(p, x => x.key));
            CollectionAssert.AreEqual(new object[] { "cube", 12, 34, 2, 7, 5, 1, 2, 42 }, Array.ConvertAll(p, x => x.value));
            Assert.IsNull(a.Quit(6, 1, 2, 50f), "a second quit for the same attempt");
        }

        [Test]
        public void NoQuitAfterCompletionOrRestart()
        {
            var a = Started();
            a.Close();
            Assert.IsNull(a.Quit(5, 0, 0, 10f));
            Assert.IsNull(a.Peek(5, 0, 0, 10f));
        }

        [Test]
        public void NoQuitWithoutMoves()
        {
            Assert.IsNull(Started().Quit(0, 0, 0, 10f));
            Assert.IsNull(new LevelAttempt().Quit(3, 0, 0, 10f), "never started");
        }

        [Test]
        public void PeekKeepsTheAttemptOpen()
        {
            var a = Started();
            Assert.IsNotNull(a.Peek(3, 0, 0, 5f));
            Assert.IsTrue(a.Open);
            Assert.IsNotNull(a.Quit(3, 0, 0, 5f));
        }

        [Test]
        public void PendingQuitSurvivesJson()
        {
            var q = Started().Peek(3, 1, 0, 9f);
            q.pausedAt = 123;
            var back = JsonUtility.FromJson<LevelQuit>(JsonUtility.ToJson(q));
            CollectionAssert.AreEqual(Array.ConvertAll(q.Params(), x => x.value), Array.ConvertAll(back.Params(), x => x.value));
            Assert.AreEqual(123, back.pausedAt);
        }

        [Test]
        public void OnlyALongPauseCountsAsQuit()
        {
            var now = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsFalse(LevelAttempt.PausedTooLong(now.AddSeconds(-30).Ticks, now));
            Assert.IsTrue(LevelAttempt.PausedTooLong(now.AddSeconds(-61).Ticks, now));
            Assert.IsFalse(LevelAttempt.PausedTooLong(0, now), "never paused");
        }
    }
}
