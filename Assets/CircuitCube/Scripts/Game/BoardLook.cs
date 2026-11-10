using System;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Everything the board renderer needs that is not a theme colour: the quad mesh, the shader, where each layer of a
    /// tile sits and how big it is, and the animation curves. Built in the editor from the Tile / TriTile prefabs and
    /// their animation clips (Tools/Circuit Cube/Board/Build Board Look), so the prefabs stay the place to design a
    /// tile; the game draws from this asset instead of instantiating a prefab per tile.
    /// </summary>
    [CreateAssetMenu(menuName = "Circuit Cube/Board Look", fileName = "BoardLook")]
    public sealed class BoardLook : ScriptableObject
    {
        [Serializable]
        public struct Layer
        {
            [Tooltip("Position on the tile (tile units, +Z is out of the face).")]
            public Vector3 Offset;
            [Tooltip("Width of the layer's square quad in tile units.")]
            public float Size;
            [Tooltip("Turn of the image on the tile, degrees (the wire turn is added on top).")]
            public float Angle;
            [Tooltip("Image flipped left to right, as the prefab's negative X scale.")]
            public bool Mirror;
        }

        [Serializable]
        public sealed class TileShape
        {
            [Tooltip("Sides of the tile: 3 triangle, 4 square, 5 pentagon.")]
            public int Ports = 4;
            [Tooltip("Wire mask drawn by each wire image, in the theme skin's order (bit k = arm towards side k).")]
            public int[] Canon = new int[0];
            [Tooltip("A tap on a point closer than this to the tile centre (tile units) can hit the tile.")]
            public float PickRadius = 1.42f;
            public Layer Base, Wire, Core, Halo, Ring, CoreOff, CoreOn, SourceHalo, SourceOutline, SourceCenter, HintPin, Burst;
        }

        public Mesh Quad;
        public Shader Shader;
        [Tooltip("Instanced material the board copies for its wires, lamps and glows. Being an asset with GPU instancing on, " +
                 "it also keeps the shader's instancing variant in player builds, which strip variants no asset uses.")]
        public Material SpriteMaterial;
        [Tooltip("Draws the tile faces of a board as one mesh (BoardFace.shader).")]
        public Shader FaceShader;
        public Texture HintTexture;
        public TileShape[] Shapes = new TileShape[0];

        [Header("Lamp (scale of the layer at rest = 1)")]
        public AnimationCurve LampOnCore = AnimationCurve.Linear(0, 1, 1, 1);
        public AnimationCurve LampOnRing = AnimationCurve.Linear(0, 1, 1, 1);
        public AnimationCurve LampOnHalo = AnimationCurve.Linear(0, 1, 1, 1);
        [Tooltip("Loops while the lamp is lit.")]
        public AnimationCurve LampLitHalo = AnimationCurve.Linear(0, 1, 1, 1);
        public AnimationCurve LampOffCore = AnimationCurve.Linear(0, 1, 1, 1);

        [Header("Source")]
        [Tooltip("Outline and centre, loops.")]
        public AnimationCurve SourceBreath = AnimationCurve.Linear(0, 1, 1, 1);
        public float SourceSpin = 60f;   // degrees per second of the source halo

        [Header("Hint pin and win burst (scale of the layer at rest = 1)")]
        public AnimationCurve HintPop = AnimationCurve.Linear(0, 1, 1, 1);
        public AnimationCurve Burst = AnimationCurve.Linear(0, 1, 1, 1);

        [Header("Tile turn")]
        public float TurnTime = 0.18f;
        [Tooltip("Overshoot at the end of a turn, like a click into place.")]
        public float TurnOvershoot = 0.12f;

        [Header("Win: the box breaks open")]
        [Tooltip("How long each piece flies (seconds).")]
        public float ShatterTime = 0.9f;
        [Tooltip("Pieces start one after another over this time, the ones facing the camera first (seconds).")]
        public float ShatterStagger = 0.14f;
        [Tooltip("How far a piece flies, in board radii.")]
        public float ShatterDistance = 1.6f;
        [Tooltip("Most a piece spins while it flies (degrees).")]
        public float ShatterSpin = 540f;
        [Tooltip("Size of the light left where the box was, in board radii (0 = none).")]
        public float ShatterCore = 1.3f;

        public TileShape ShapeFor(int ports)
        {
            foreach (var s in Shapes) if (s.Ports == ports) return s;
            return Shapes.Length > 0 ? Shapes[0] : null;
        }

        public static float Length(AnimationCurve c) => c != null && c.length > 0 ? c[c.length - 1].time : 0f;
    }
}
