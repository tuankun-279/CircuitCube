using CircuitCube.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CircuitCube.EditorTools
{
    /// <summary>
    /// Turns the Tile / TriTile prefabs and their animation clips into the BoardLook asset the instanced board
    /// renderer draws from. The prefabs stay the place to design a tile: move or resize a layer, retime a clip, then
    /// run Tools/Circuit Cube/Board/Build Board Look.
    /// </summary>
    public static class BoardLookBuilder
    {
        const string LookPath = "Assets/CircuitCube/Themes/BoardLook.asset";
        const string ShaderPath = "Assets/CircuitCube/Shaders/BoardSprite.shader";
        const string Prefabs = "Assets/CircuitCube/Prefabs/Board/";
        const string Clips = "Assets/CircuitCube/Animations/";

        [MenuItem("Tools/Circuit Cube/Board/Build Board Look")]
        public static BoardLook Build()
        {
            var look = AssetDatabase.LoadAssetAtPath<BoardLook>(LookPath);
            if (look == null)
            {
                look = ScriptableObject.CreateInstance<BoardLook>();
                AssetDatabase.CreateAsset(look, LookPath);
            }
            look.Shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

            var square = Shape("Tile", look);
            var tri = Shape("TriTile", look);
            look.Shapes = new[] { square, tri };

            // curves come from the square tile's clips, divided by the layer's size at rest
            look.LampOnCore = Curve("LampOn", "BulbOn", square.CoreOn.Size);
            look.LampOnRing = Curve("LampOn", "Socket", square.Ring.Size);
            look.LampOnHalo = Curve("LampOn", "Glow", square.Halo.Size);
            look.LampLitHalo = Curve("LampLit", "Glow", square.Halo.Size);
            look.LampOffCore = Curve("LampOff", "BulbOff", square.CoreOff.Size);
            look.SourceBreath = Curve("SourceIdle", "Disc", square.SourceOutline.Size);
            look.SourceSpin = Spin("SourceIdle", "Ring");
            look.HintPop = Curve("HintPop", "", square.HintPin.Size);
            look.Burst = Curve("Burst", "", square.Ring.Size);

            EditorUtility.SetDirty(look);
            AssetDatabase.SaveAssets();
            WireScene(look);
            Debug.Log("[CircuitCube] Board look built from the tile prefabs.");
            return look;
        }

        static BoardLook.TileShape Shape(string prefab, BoardLook look)
        {
            var root = PrefabUtility.LoadPrefabContents(Prefabs + prefab + ".prefab");
            try
            {
                var bt = root.GetComponent<BoardTile>();
                var shape = new BoardLook.TileShape { Ports = bt.Ports, Canon = (int[])bt.Canon.Clone() };
                var r = root.transform;
                shape.Base = L(r, bt.Base.transform);
                shape.Wire = L(r, bt.Wires[0].transform);
                shape.Core = L(r, bt.WireCores[0].transform);
                shape.Halo = L(r, bt.LampGlow.transform);
                shape.Ring = L(r, bt.LampSocket.transform);
                shape.CoreOff = L(r, bt.LampBulbOff.transform);
                shape.CoreOn = L(r, bt.LampBulbOn.transform);
                shape.SourceHalo = L(r, bt.SourceRing.transform);
                shape.SourceOutline = L(r, bt.SourceDisc.transform);
                shape.SourceCenter = L(r, bt.SourceMark.transform);
                shape.HintPin = L(r, bt.HintPin.transform);
                // the burst is the lamp ring growing past the tile, drawn above everything
                var burst = L(r, bt.Burst.transform);
                burst.Size = shape.Ring.Size;
                shape.Burst = burst;
                // a tap counts within the tile's corner radius; overlaps are settled by the nearest centre
                shape.PickRadius = shape.Base.Size * (bt.Ports == 4 ? 0.7071f : 0.5f);
                if (bt.Ports == 4)
                {
                    look.Quad = bt.Base.GetComponent<MeshFilter>().sharedMesh;
                    look.HintTexture = bt.HintPin.GetComponent<Renderer>().sharedMaterial.mainTexture;
                }
                return shape;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Where a layer sits on the tile, measured through every parent between it and the tile root.</summary>
        static BoardLook.Layer L(Transform root, Transform t)
        {
            Matrix4x4 m = root.worldToLocalMatrix * t.localToWorldMatrix;
            Vector3 x = m.GetColumn(0), y = m.GetColumn(1);
            float size = new Vector2(y.x, y.y).magnitude;
            bool mirror = x.x * y.y - x.y * y.x < 0f;
            float angle = Mathf.Atan2(y.y, y.x) * Mathf.Rad2Deg - 90f;
            return new BoardLook.Layer { Offset = m.GetColumn(3), Size = size, Angle = Mathf.Abs(angle) < 0.01f ? 0f : angle, Mirror = mirror };
        }

        static AnimationCurve Curve(string clipName, string path, float rest)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + clipName + ".anim");
            var c = clip != null ? AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.y")) : null;
            if (c == null || rest <= 0f) return AnimationCurve.Constant(0f, 0.01f, 1f);
            var keys = c.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value /= rest;
                keys[i].inTangent /= rest;
                keys[i].outTangent /= rest;
            }
            return new AnimationCurve(keys);
        }

        /// <summary>Degrees per second of a layer the clip turns round its axis.</summary>
        static float Spin(string clipName, string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + clipName + ".anim");
            if (clip == null) return 0f;
            var z = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"));
            var w = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"));
            if (z == null || w == null || clip.length <= 0f) return 0f;
            // a quaternion covers half the angle, so the clip's half turn of (z, w) is a full turn of the layer
            float half = Mathf.Atan2(z.Evaluate(clip.length * 0.5f), w.Evaluate(clip.length * 0.5f)) * Mathf.Rad2Deg;
            return half * 2f * 2f / clip.length;
        }

        static void WireScene(BoardLook look)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var app = Object.FindFirstObjectByType<CircuitCubeApp>();
            if (app == null) return;
            app.Look = look;
            EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
