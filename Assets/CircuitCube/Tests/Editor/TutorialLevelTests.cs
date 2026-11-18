using CircuitCube.Core;
using NUnit.Framework;

namespace CircuitCube.Tests
{
    /// <summary>The hand-made tutorial levels: every tile is where the lessons point, every level is solvable and short.</summary>
    public class TutorialLevelTests
    {
        static readonly int[] Pars = { 1, 2, 2, 2, 1, 2 };

        [Test]
        public void EveryTileOfEveryLevelIsOnTheCube()
        {
            for (int n = 1; n <= TutorialLevels.Count; n++)
                foreach (var s in TutorialLevels.SpecOf(n))
                    Assert.GreaterOrEqual(TutorialLevels.Find(TutorialLevels.Topology, s.Face, s.Cell), 0, "level " + n + " " + s.Face + s.Cell);
        }

        [Test]
        public void LevelsAreSolvedOnlyWhenEveryTileIsTurnedBack()
        {
            for (int n = 1; n <= TutorialLevels.Count; n++)
            {
                var level = TutorialLevels.Build(n);
                Assert.AreEqual(54, level.Tiles.Length);
                Assert.IsTrue(level.IsSolved(true), "level " + n + " solved layout");
                Assert.IsFalse(level.IsSolved(false), "level " + n + " starts unsolved");
                Assert.AreEqual(Pars[n - 1], level.Par, "level " + n + " par");
                foreach (var t in level.Tiles) t.Rot += Circuit.TapsToSolve(t);
                Assert.IsTrue(level.IsSolved(false), "level " + n + " after the par moves");
            }
        }

        [Test]
        public void WiresCrossTheEdgeInLevelTwo()
        {
            var level = TutorialLevels.Build(2);
            var topo = level.Topo;
            int front = TutorialLevels.Find(topo, 'f', 5), side = TutorialLevels.Find(topo, 'r', 3);
            bool linked = false;
            foreach (var l in topo.Links[front]) linked |= l.Cell == side;
            Assert.IsTrue(linked);
        }
    }
}
