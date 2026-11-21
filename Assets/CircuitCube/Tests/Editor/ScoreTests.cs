using CircuitCube.Core;
using NUnit.Framework;

namespace CircuitCube.Tests
{
    /// <summary>Expert by wasted moves: the fewest taps are counted to the player's own finished board.</summary>
    public class ScoreTests
    {
        static CubeTile Tile(int sol, int rot, bool locked = false) => new CubeTile { Sol = sol, Ports = 4, Kind = TileKind.Wire, Rot = rot, Locked = locked };

        [Test]
        public void StraightWireMatchesAfterHalfATurn()
        {
            // a straight wire turned from 1 to 2 looks the same as at 0: one tap, not three
            var tiles = new[] { Tile(0b0101, 2) };
            Assert.AreEqual(1, LevelScore.MinTaps(tiles, new[] { 1 }));
            tiles[0].Rot = 4;
            Assert.AreEqual(1, LevelScore.MinTaps(tiles, new[] { 1 }));
        }

        [Test]
        public void CornerCountsTheTurnsToItsFinalLayout()
        {
            var tiles = new[] { Tile(0b0011, 4), Tile(0b0011, 2) };
            Assert.AreEqual(3 + 0, LevelScore.MinTaps(tiles, new[] { 1, 2 }));
        }

        [Test]
        public void TilesFixedByAHintCountNothing()
        {
            var tiles = new[] { Tile(0b0011, 0, true), Tile(0b0011, 3) };
            Assert.AreEqual(2, LevelScore.MinTaps(tiles, new[] { 1, 1 }));
        }

        [Test]
        public void ExpertWithinTheSlackOfTheFewestTaps()
        {
            var r = new GameRules();
            int min = 20, limit = LevelScore.ExpertLimit(min, r);
            Assert.AreEqual(LevelScore.Expert, LevelScore.Points(limit, min, 0, 0, r));
            Assert.AreEqual(r.parScore, LevelScore.Points(limit, min, 1, 0, r));
            Assert.Less(LevelScore.Points(limit + 1, min, 0, 0, r), r.parScore);
            Assert.AreEqual(3, LevelScore.Waste(23, min));
        }
    }
}
