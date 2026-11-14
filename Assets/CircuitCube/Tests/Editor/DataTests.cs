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
        public void GeneratorKeepsGoingPast100()
        {
            int[] sample = { 101, 105, 150, 199, 200, 201, 202, 203, 250, 299, 300 };
            for (int g = 0; g < LevelPlan.GroupCount; g++)
                foreach (int l in sample)
                {
                    var lv = LevelPlan.Build(g, l);
                    string where = LevelPlan.GroupIds[g] + " " + l;
                    Assert.IsTrue(lv.IsSolved(true), where + " solution");
                    Assert.IsFalse(lv.IsSolved(false), where + " start");
                    Assert.LessOrEqual(lv.Tiles.Length, 200, where + " board size");
                    Assert.GreaterOrEqual(lv.Par, 10, where + " par");
                }
            // the first hundred keep their boards: same shapes as the first release of the data
            Assert.AreEqual("cube-5", LevelPlan.ShapeIdFor(0, 100));
            Assert.AreEqual("cube-4", LevelPlan.ShapeIdFor(0, 99));
            Assert.AreEqual("cross-22", LevelPlan.ShapeIdFor(5, 90));
            Assert.AreEqual("tetra-6", LevelPlan.ShapeIdFor(1, 60));
            Assert.AreEqual("tetra-5", LevelPlan.ShapeIdFor(1, 61));
            Assert.AreEqual("tetra-6", LevelPlan.ShapeIdFor(1, 105));   // every 5th past 100 is big
            Assert.AreEqual("tetra-6", LevelPlan.ShapeIdFor(1, 204));   // every 3rd past 200
        }

        [Test]
        public void ScoreRule()
        {
            var r = new GameRules();
            // slack: 15% of par, rounded up
            Assert.AreEqual(10 + 2, LevelScore.ExpertLimit(10, r));
            Assert.AreEqual(70 + 11, LevelScore.ExpertLimit(70, r));
            Assert.AreEqual(100 + 15, LevelScore.ExpertLimit(100, r));
            Assert.AreEqual(6, LevelScore.Points(5, 5, 0, r));
            Assert.AreEqual(6, LevelScore.Points(6, 5, 0, r));      // within the slack
            Assert.AreEqual(5, LevelScore.Points(7, 5, 0, r));
            Assert.AreEqual(6, LevelScore.Points(81, 70, 0, r));
            Assert.AreEqual(5, LevelScore.Points(82, 70, 0, r));
            Assert.AreEqual(5, LevelScore.Points(4, 5, 1, r));
            Assert.AreEqual(3, LevelScore.Points(20, 5, 3, r));
            var sphere = new GroupData { expertSlackPercent = 20 };      // a group's own slack wins, its min falls back to the rule
            Assert.AreEqual(100 + 20, LevelScore.ExpertLimit(100, r, sphere));
            Assert.AreEqual(10 + 2, LevelScore.ExpertLimit(10, r, sphere));
            Assert.AreEqual(100 + 15, LevelScore.ExpertLimit(100, r, new GroupData()));
            r.expertSlackPercent = 0; r.expertSlackMin = 0;        // strict par still possible from the manifest
            Assert.AreEqual(5, LevelScore.Points(6, 5, 0, r));
            Assert.AreEqual(3, LevelScore.WholeStars(6));
            Assert.AreEqual(2, LevelScore.WholeStars(5));
            Assert.AreEqual(5, LevelScore.HalfStars(5));         // 2½ stars
            Assert.AreEqual(0, LevelScore.HalfStars(0));
            Assert.AreEqual("2½", LevelScore.StarText(5));
            Assert.AreEqual("3", LevelScore.StarText(6));
            Assert.AreEqual("0", LevelScore.StarText(0));
        }

        [Test]
        public void AdsOnlyForListedDevices()
        {
            string mine = AdsGate.CodeFrom("device-a"), other = AdsGate.CodeFrom("device-b");
            Assert.AreEqual(9, mine.Length);
            Assert.AreEqual('-', mine[4]);
            Assert.AreEqual(mine, AdsGate.CodeFrom("device-a"));   // the same device always gets the same code
            Assert.AreNotEqual(mine, other);
            Assert.IsFalse(AdsGate.Allowed(0, mine, mine));         // off for everyone, listed or not
            Assert.IsTrue(AdsGate.Allowed(1, "", mine));            // on for everyone
            Assert.IsFalse(AdsGate.Allowed(2, "", mine));           // testers only, nobody listed
            Assert.IsFalse(AdsGate.Allowed(2, other, mine));
            Assert.IsTrue(AdsGate.Allowed(2, other + ", " + mine, mine));
            Assert.IsTrue(AdsGate.Allowed(2, mine.Replace("-", "").ToLowerInvariant() + ";" + other, mine));   // typed by hand
            // a manifest published before the rule existed lists nobody
            var old = JsonUtility.FromJson<ManifestData>("{\"schema\":3,\"rules\":{\"adsOn\":2}}");
            Assert.AreEqual("", old.rules.adsTesters);
            var set = JsonUtility.FromJson<ManifestData>("{\"schema\":3,\"rules\":{\"adsOn\":2,\"adsTesters\":\"ABCD-EFGH\"}}");
            Assert.IsTrue(AdsGate.Allowed(set.rules.adsOn, set.rules.adsTesters, "ABCD-EFGH"));
        }

        [Test]
        public void UndoLimitRule()
        {
            Assert.AreEqual(5, new GameRules().undosPerLevel);
            Assert.AreEqual(1, new GameRules().adsOn);           // ads stay on unless a manifest says 0
            // a manifest published before the rule existed keeps the default
            var old = JsonUtility.FromJson<ManifestData>("{\"schema\":3,\"rules\":{\"hintsPerLevel\":3}}");
            Assert.AreEqual(5, old.rules.undosPerLevel);
            var set = JsonUtility.FromJson<ManifestData>("{\"schema\":3,\"rules\":{\"undosPerLevel\":2}}");
            Assert.AreEqual(2, set.rules.undosPerLevel);
            // a level left half way keeps the undos it used
            var ip = JsonUtility.FromJson<InProgress>(JsonUtility.ToJson(new InProgress { serial = 7, undosUsed = 3 }));
            Assert.AreEqual(3, ip.undosUsed);
        }

        [Test]
        public void UpdateFlowChoice()
        {
            Assert.AreEqual(AppUpdates.Mode.None, AppUpdates.Decide(3, 3, 0, true, true, false));         // up to date
            Assert.AreEqual(AppUpdates.Mode.Flexible, AppUpdates.Decide(3, 4, 0, true, true, false));     // offered
            Assert.AreEqual(AppUpdates.Mode.None, AppUpdates.Decide(3, 4, 0, true, true, true));          // once a day
            Assert.AreEqual(AppUpdates.Mode.Immediate, AppUpdates.Decide(3, 5, 4, true, true, true));     // forced below 4
            Assert.AreEqual(AppUpdates.Mode.Flexible, AppUpdates.Decide(4, 5, 4, true, true, false));     // 4 is not below 4
            Assert.AreEqual(AppUpdates.Mode.Flexible, AppUpdates.Decide(3, 5, 4, false, true, false));    // Play allows only flexible
            Assert.AreEqual(0, new GameRules().updateBelow);
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
