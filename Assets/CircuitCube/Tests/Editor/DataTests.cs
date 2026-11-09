using System.IO;
using CircuitCube.Core;
using CircuitCube.Game;
using NUnit.Framework;
using UnityEngine;

namespace CircuitCube.Tests
{
    public class DataTests
    {
        [Test]
        public void TilesRoundTrip()
        {
            for (int g = 0; g < LevelPlan.GroupCount; g++)
                for (int l = 1; l <= 12; l += 5)
                {
                    var lv = LevelPlan.Build(g, l);
                    string s = TileCodec.Encode(lv.Tiles);
                    Assert.AreEqual(lv.Tiles.Length * 3, s.Length);
                    var back = TileCodec.Decode(s, lv.Topo);
                    Assert.IsNotNull(back, "group " + g + " level " + l);
                    for (int i = 0; i < back.Length; i++)
                    {
                        Assert.AreEqual(lv.Tiles[i].Sol, back[i].Sol);
                        Assert.AreEqual(lv.Tiles[i].Kind, back[i].Kind);
                        Assert.AreEqual(lv.Tiles[i].Color, back[i].Color);
                        Assert.AreEqual(lv.Tiles[i].Rot, back[i].Rot);
                    }
                    Assert.AreEqual(TileCodec.Encode(back), s);
                }
        }

        [Test]
        public void ExportedLevelsPlayLikeTheGenerator()
        {
            var files = DataExport.FromGenerator(10);
            for (int g = 0; g < LevelPlan.GroupCount; g++)
            {
                string id = LevelPlan.GroupIds[g];
                for (int l = 1; l <= 10; l++)
                {
                    var d = JsonUtility.FromJson<LevelData>(files[GameData.LevelPath(id, l)]);
                    var lv = GameData.FromData(d, v => JsonUtility.FromJson<VoxelShapeFile>(files["shapes/voxel/" + v + ".json"]));
                    var want = LevelPlan.Build(g, l);
                    Assert.IsNotNull(lv, id + " " + l);
                    Assert.AreEqual(want.Topo.Count, lv.Topo.Count, id + " " + l + " tile count");
                    Assert.AreEqual(TileCodec.Encode(want.Tiles), TileCodec.Encode(lv.Tiles), id + " " + l);
                    Assert.AreEqual(want.Par, lv.Par);
                    Assert.AreEqual(TileCodec.Check(d.tiles), d.check);
                    Assert.IsTrue(lv.IsSolved(true), id + " " + l + " solution");
                    Assert.IsFalse(lv.IsSolved(false), id + " " + l + " start");
                }
            }
        }

        [Test]
        public void ScoreRule()
        {
            var r = new GameRules();
            // slack: at least 5 taps, 15% of par for big boards
            Assert.AreEqual(10 + 5, LevelScore.ExpertLimit(10, r));
            Assert.AreEqual(70 + 11, LevelScore.ExpertLimit(70, r));
            Assert.AreEqual(100 + 15, LevelScore.ExpertLimit(100, r));
            Assert.AreEqual(6, LevelScore.Points(5, 5, 0, r));
            Assert.AreEqual(6, LevelScore.Points(10, 5, 0, r));     // within the slack
            Assert.AreEqual(5, LevelScore.Points(11, 5, 0, r));
            Assert.AreEqual(6, LevelScore.Points(81, 70, 0, r));
            Assert.AreEqual(5, LevelScore.Points(82, 70, 0, r));
            Assert.AreEqual(5, LevelScore.Points(4, 5, 1, r));
            Assert.AreEqual(3, LevelScore.Points(20, 5, 3, r));
            r.expertSlackPercent = 0; r.expertSlackMin = 0;        // strict par still possible from the manifest
            Assert.AreEqual(5, LevelScore.Points(6, 5, 0, r));
            Assert.AreEqual(3, LevelScore.WholeStars(6));
            Assert.AreEqual(2, LevelScore.WholeStars(5));
        }

        [Test]
        public void SaveRoundTrip()
        {
            string old = CubeSave.Root;
            string dir = Path.Combine(Path.GetTempPath(), "cc-save-test-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                CubeSave.Root = dir;
                CubeSave.Unload();
                CubeSave.Complete(0, 1, 6);
                CubeSave.Complete(0, 2, 4);
                CubeSave.SetInProgress(1, new InProgress { serial = 3, position = 3, moves = 2, hintsUsed = 1 });
                CubeSave.Unload();                       // force a read from disk
                Assert.AreEqual(2, CubeSave.Beaten(0));
                Assert.AreEqual(6, CubeSave.Points(0, 1));
                Assert.AreEqual(4, CubeSave.Points(0, 2));
                Assert.AreEqual(0, CubeSave.Points(0, 3));
                var ip = CubeSave.GetInProgress(1);
                Assert.IsNotNull(ip);
                Assert.AreEqual(3, ip.position);
                Assert.AreEqual(1, ip.hintsUsed);
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
