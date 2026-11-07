using CircuitCube.Core;
using CircuitCube.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace CircuitCube.Tests
{
    /// <summary>
    /// Guards the performance goals of the instanced board: with the theme's board atlas a big shape draws in at most
    /// 12 calls (one per layer), and drawing, refreshing and turning tiles allocate no garbage.
    /// </summary>
    public class BoardPerfTests
    {
        const int MaxDrawCalls = 12;
        GameObject _camGo, _boardGo;
        GameTheme _oldTheme;

        [SetUp]
        public void SetUp()
        {
            _oldTheme = Themes.Current;
            Themes.Set(AssetDatabase.LoadAssetAtPath<GameTheme>("Assets/CircuitCube/Themes/Theme_Dark.asset"));
            _camGo = new GameObject("TestCamera", typeof(Camera));
            _camGo.transform.position = new Vector3(0, 0, -20);
        }

        [TearDown]
        public void TearDown()
        {
            if (_boardGo != null) Object.DestroyImmediate(_boardGo);
            Object.DestroyImmediate(_camGo);
            if (_oldTheme != null) Themes.Set(_oldTheme);
        }

        CubeBoardView Board(int group, int level)
        {
            var look = AssetDatabase.LoadAssetAtPath<BoardLook>("Assets/CircuitCube/Themes/BoardLook.asset");
            Assert.IsNotNull(look, "Run Tools/Circuit Cube/Board/Build Board Look first.");
            var topo = LevelPlan.TopologyFor(group, level);
            var lv = CubeGenerator.Generate(0, topo, new[] { 1, 2, 4 }, 99u, 0);
            if (_boardGo != null) Object.DestroyImmediate(_boardGo);
            _boardGo = new GameObject("Board");
            var board = _boardGo.AddComponent<CubeBoardView>();
            board.Build(lv, look, _camGo.GetComponent<Camera>(), true);
            board.transform.rotation = Quaternion.Euler(-28f, 38f, 0f);
            return board;
        }

        // level 100 is the biggest shape of most generator groups: cube 150 tiles, tetra 144, octa 128, sphere 162,
        // dodeca 132 (Megaminx); Nebula level 30 is a station shape, so its size 180 boards come from level 40
        [TestCase(0, 100)]
        [TestCase(1, 100)]
        [TestCase(2, 100)]
        [TestCase(3, 100)]
        [TestCase(4, 100)]
        [TestCase(5, 30)]
        public void BigShapeDrawsInFewCalls(int group, int level)
        {
            var board = Board(group, level);
            board.Draw();
            Assert.Greater(board.TileCount, 0);
            Assert.LessOrEqual(board.LastDrawCalls, MaxDrawCalls, board.TileCount + " tiles");
        }

        [Test]
        public void DrawingAllocatesNothing()
        {
            var board = Board(2, 100);
            board.Draw();
            Assert.That(() => board.Draw(), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void HundredTurnsAllocateNothing()
        {
            var board = Board(0, 100);
            var level = GetLevel(board);
            board.Refresh(false);
            board.Draw();
            Assert.That(() =>
            {
                for (int n = 0; n < 100; n++)
                {
                    int i = n * 7 % level.Tiles.Length;
                    level.Tiles[i].Rot++;
                    board.AnimateRotation(i, 1);
                    board.Refresh(true);
                    board.Draw();
                }
            }, Is.Not.AllocatingGCMemory());
        }

        static CubeLevel GetLevel(CubeBoardView board) =>
            (CubeLevel)typeof(CubeBoardView).GetField("_level", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(board);
    }
}
