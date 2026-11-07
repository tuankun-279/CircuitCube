using System.Collections.Generic;
using CircuitCube.Core;
using NUnit.Framework;

namespace CircuitCube.Tests
{
    public class CircuitCubeTests
    {
        static IEnumerable<object[]> Shapes()
        {
            for (int n = 2; n <= 5; n++) yield return new object[] { "cube" + n, ShapeLibrary.Cube(n) };
            foreach (int b in new[] { 3, 5, 6, 7 }) yield return new object[] { "pyramid" + b, ShapeLibrary.Pyramid(b) };
            foreach (float r in new[] { 1.5f, 2f, 2.5f, 3f }) yield return new object[] { "ball" + r, ShapeLibrary.Ball(r) };
            yield return new object[] { "cross11", ShapeLibrary.Cross(1, 1) };
            yield return new object[] { "cross21", ShapeLibrary.Cross(2, 1) };
            yield return new object[] { "cross31", ShapeLibrary.Cross(3, 1) };
            yield return new object[] { "cross22", ShapeLibrary.Cross(2, 2) };
            yield return new object[] { "tower", ShapeLibrary.Tower(2, 4) };
            yield return new object[] { "ell", ShapeLibrary.Ell(4, 2) };
            for (uint s = 1; s <= 20; s++) yield return new object[] { "mix" + s, ShapeLibrary.Megamix(s * 7919u, 50, 180) };
        }

        [TestCaseSource(nameof(Shapes))]
        public void EveryLinkPointsBack(string name, List<Vec3i> blocks)
        {
            var topo = new ShapeTopology(blocks);
            Assert.Greater(topo.Count, 0, name);
            for (int c = 0; c < topo.Count; c++)
                for (int k = 0; k < 4; k++)
                {
                    var link = topo.Links[c][k];
                    Assert.GreaterOrEqual(link.Back, 0, name + ": missing back direction at tile " + c + " dir " + k);
                    var back = topo.Links[link.Cell][link.Back];
                    Assert.AreEqual(c, back.Cell, name + ": tile " + c + " dir " + k);
                    Assert.AreEqual(k, back.Back, name + ": tile " + c + " dir " + k);
                    Assert.AreNotEqual(c, link.Cell, name);
                }
        }

        [Test]
        public void TetrahedronLinksPointBack([Values(2, 3, 4, 5, 6)] int n)
        {
            var topo = PolyShapes.Tetrahedron(n);
            Assert.AreEqual(4 * n * n, topo.Count);
            for (int c = 0; c < topo.Count; c++)
            {
                Assert.AreEqual(3, topo.Cells[c].Ports);
                for (int k = 0; k < 3; k++)
                {
                    var link = topo.Links[c][k];
                    Assert.GreaterOrEqual(link.Cell, 0, "open side at tile " + c);
                    var back = topo.Links[link.Cell][link.Back];
                    Assert.AreEqual(c, back.Cell);
                    Assert.AreEqual(k, back.Back);
                }
            }
        }

        [Test]
        public void PolyShapeLinksPointBack(
            [Values("octa-2", "octa-3", "octa-4", "dodeca-2", "dodeca-3", "geo-1", "geo-2", "geo-3", "geo-4")] string id)
        {
            var topo = ShapeResolver.Build(id, null);
            Assert.IsNotNull(topo, id);
            int expected;
            switch (id)
            {
                case "octa-2": expected = 32; break;
                case "octa-3": expected = 72; break;
                case "octa-4": expected = 128; break;
                case "dodeca-2": expected = 60; break;
                case "dodeca-3": expected = 132; break;
                case "geo-1": expected = 32; break;
                case "geo-2": expected = 42; break;
                case "geo-3": expected = 92; break;
                default: expected = 162; break;
            }
            Assert.AreEqual(expected, topo.Count, id);
            int pentagons = 0;
            for (int c = 0; c < topo.Count; c++)
            {
                var cell = topo.Cells[c];
                Assert.AreEqual(cell.Ports, cell.Corners.Length, id);
                if (cell.Ports == 5) pentagons++;
                for (int k = 0; k < cell.Ports; k++)
                {
                    var link = topo.Links[c][k];
                    Assert.GreaterOrEqual(link.Cell, 0, id + ": open side at tile " + c);
                    var back = topo.Links[link.Cell][link.Back];
                    Assert.AreEqual(c, back.Cell, id + ": tile " + c + " side " + k);
                    Assert.AreEqual(k, back.Back, id + ": tile " + c + " side " + k);
                    // neighbours share the side: its middle is the same point on both tiles
                    var a = (cell.Corners[k] + cell.Corners[(k + 1) % cell.Ports]) * 0.5f;
                    var o = topo.Cells[link.Cell];
                    var b = (o.Corners[link.Back] + o.Corners[(link.Back + 1) % o.Ports]) * 0.5f;
                    Assert.Less((a - b).Length, 1e-3f, id + ": tile " + c + " side " + k);
                }
            }
            if (id.StartsWith("geo") || id == "dodeca-3") Assert.AreEqual(12, pentagons, id);
        }

        [Test]
        public void HexagonTilesRoundTrip()
        {
            var lv = LevelPlan.Build(3, 55);
            string s = TileCodec.Encode(lv.Tiles);
            var back = TileCodec.Decode(s, lv.Topo);
            Assert.IsNotNull(back);
            for (int i = 0; i < lv.Tiles.Length; i++)
            {
                Assert.AreEqual(lv.Tiles[i].Sol, back[i].Sol);
                Assert.AreEqual(lv.Tiles[i].Rot, back[i].Rot);
            }
        }

        [Test]
        public void TriangleTurnIsAThird()
        {
            Assert.AreEqual(0b010, Circuit.RotCW(0b001, 3));
            Assert.AreEqual(0b001, Circuit.RotCW(0b100, 3));
            Assert.AreEqual(0b011, Circuit.RotN(0b011, 3, 3));
        }

        [Test]
        public void CubeHasSixFaces([Values(2, 3, 4, 5)] int n)
        {
            Assert.AreEqual(6 * n * n, new ShapeTopology(ShapeLibrary.Cube(n)).Count);
        }

        [Test]
        public void EveryGroupLevelIsSolvableAndScrambled([Values(0, 1, 2, 3, 4, 5)] int group)
        {
            for (int l = 1; l <= LevelPlan.LevelsPerGroup; l++)
            {
                var lv = LevelPlan.Build(group, l);
                string at = LevelPlan.Groups[group].Name + " " + l;
                Assert.IsTrue(lv.IsSolved(true), at + ": solution does not light every lamp");
                Assert.IsFalse(lv.IsSolved(false), at + ": starts already solved");
                int lamps = 0, sources = 0;
                foreach (var t in lv.Tiles)
                {
                    Assert.AreNotEqual(0, t.Sol, at + ": empty tile");
                    if (t.Kind == TileKind.Lamp) lamps++;
                    if (t.Kind == TileKind.Source) sources++;
                }
                Assert.Greater(lamps, 0, at);
                Assert.Greater(sources, 0, at);
                Assert.Greater(lv.Par, 0, at);
                Assert.LessOrEqual(lv.Tiles.Length, 200, at + ": board too big for a phone");
            }
        }

        [Test]
        public void SameLevelGivesSameBoard([Values(0, 1, 2, 3, 4, 5)] int group)
        {
            var a = LevelPlan.Build(group, 37);
            var topo = LevelPlan.TopologyFor(group, 37);
            var b = CubeGenerator.Generate(37, topo, LevelPlan.ColorsFor(group, 37), LevelPlan.SeedFor(group, 37),
                LevelPlan.MinParFor(37, topo.Count), LevelPlan.ScrambleFor(group, 37, topo.Count));
            Assert.AreEqual(a.Tiles.Length, b.Tiles.Length);
            for (int i = 0; i < a.Tiles.Length; i++)
            {
                Assert.AreEqual(a.Tiles[i].Sol, b.Tiles[i].Sol);
                Assert.AreEqual(a.Tiles[i].Rot, b.Tiles[i].Rot);
                Assert.AreEqual(a.Tiles[i].Kind, b.Tiles[i].Kind);
            }
        }

        [Test]
        public void MixedLampNeedsBothColours()
        {
            var lv = LevelPlan.Build(0, LevelPlan.MixIntroLevel);
            int red = 0, blue = 0, purple = 0;
            foreach (var t in lv.Tiles)
            {
                if (t.Kind == TileKind.Source && t.Color == Circuit.Red) red++;
                if (t.Kind == TileKind.Source && t.Color == Circuit.Blue) blue++;
                if (t.Kind == TileKind.Lamp && t.Color == 3) purple++;
            }
            Assert.AreEqual(1, red);
            Assert.AreEqual(1, blue);
            Assert.Greater(purple, 0);
        }

        [Test]
        public void ShapesDifferBetweenGroups()
        {
            var counts = new HashSet<int>();
            for (int g = 0; g < LevelPlan.GroupCount; g++) counts.Add(LevelPlan.TopologyFor(g, 20).Count);
            Assert.GreaterOrEqual(counts.Count, 4);
        }

        [Test]
        public void RotationIsAQuarterTurnTowardV()
        {
            Assert.AreEqual(2, Circuit.RotCW(1));
            Assert.AreEqual(1, Circuit.RotCW(8));
            Assert.AreEqual(0b0101, Circuit.RotN(0b1010, 1));
            Assert.AreEqual(0b1010, Circuit.RotN(0b1010, 2));
        }
    }
}
