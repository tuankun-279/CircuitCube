using System.Collections.Generic;
using CircuitCube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace CircuitCube.Game
{
    /// <summary>
    /// Draws a level with GPU instancing: no GameObject, renderer, Animation or collider per tile. Each layer of the
    /// tiles (faces, one wire image, cores, lamp parts, source parts, hint pins, bursts) is one instanced draw call,
    /// so a 200-tile shape costs about 20 draw calls instead of several hundred. Sizes, offsets and animation curves
    /// come from the BoardLook asset (built from the Tile prefabs), textures from the theme's board skin.
    /// Nothing is allocated per frame.
    /// </summary>
    public sealed class CubeBoardView : MonoBehaviour
    {
        enum Lyr { Base, Wire, Core, Halo, Ring, CoreOff, CoreOn, SourceHalo, SourceOutline, SourceCenter, Hint, Burst, Count }
        const int LayerCount = (int)Lyr.Count;
        // draw order: faces first (opaque), then everything on top in the order the prefab stacked them
        static readonly int[] Queue = { 2000, 3000, 3001, 3002, 3003, 3004, 3004, 3002, 3003, 3004, 3005, 3006 };

        sealed class Group
        {
            public Material Mat;
            public Matrix4x4[] M;
            public Vector4[] C;
            public int N;
            public MaterialPropertyBlock Props;
        }

        struct TileState
        {
            public Matrix4x4 Local;          // tile frame inside the board content (content units)
            public Vector3 Center, Normal;   // content space
            public BoardLook.TileShape Shape;
            public TileKind Kind;
            public int Pick;                 // wire image index
            public float Step, Angle;
            public float TurnFrom, TurnTo, TurnStart, TurnTime;
            public bool Powered, Visible;
            public Vector4 WireColor, RingColor, CoreOnColor, HaloColor, OutlineColor, SourceHaloColor, BurstColor;
            public bool Lit, LitAnimated, OffAnimated, Hint, HintAnimated;
            public float LitStart, OffStart, HintStart, BurstStart;
        }

        CubeLevel _level;
        BoardLook _look;
        Camera _cam;
        TileState[] _t;
        Group[,] _groupOf;                  // [tile, layer]
        readonly List<Group> _groups = new List<Group>();
        readonly Dictionary<long, Group> _groupByKey = new Dictionary<long, Group>();
        int[] _comp, _sets, _stack;
        bool _interactive;

        static readonly Dictionary<long, Material> Materials = new Dictionary<long, Material>();
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

        // Tile brightness for faces turned away from the light and towards it.
        public static Color ShadeDark = new Color(0.62f, 0.64f, 0.72f, 1f);
        public static Color ShadeLight = Color.white;
        static readonly Vector3 LightDir = new Vector3(-0.45f, 0.75f, -0.5f).normalized;
        const float ContentScale = 0.5f;     // topology tiles are 2 units apart; a tile is 1 world unit wide

        public int LitLamps { get; private set; }
        public int TotalLamps { get; private set; }
        /// <summary>Radius of the shape in world units.</summary>
        public float Radius => _level.Topo.Radius * ContentScale;
        public int TileCount => _t != null ? _t.Length : 0;
        /// <summary>Instanced draw calls the board issued last frame (one per layer image in use).</summary>
        public int LastDrawCalls { get; private set; }

        static Vector3 W(V3 v) => new Vector3(v.X, v.Y, v.Z);
        static bool Linear => QualitySettings.activeColorSpace == ColorSpace.Linear;
        static Vector4 Lin(Color c) => Linear ? new Vector4(c.linear.r, c.linear.g, c.linear.b, c.a) : (Vector4)c;

        public void Build(CubeLevel level, BoardLook look, Camera cam, bool interactive)
        {
            _level = level;
            _look = look;
            _cam = cam;
            _interactive = interactive;
            int n = level.Tiles.Length;
            _comp = new int[n];
            _sets = new int[n];
            _stack = new int[n];
            _t = new TileState[n];
            _groupOf = new Group[n, LayerCount];
            for (int i = 0; i < n; i++)
            {
                var cell = level.Topo.Cells[i];
                var tile = level.Tiles[i];
                var shape = look.ShapeFor(cell.Ports);
                Vector3 nrm = W(cell.Normal), ax = W(cell.Axis);
                // local +X = port 0, local +Z = outward normal, ports go counter-clockwise
                var rot = Quaternion.LookRotation(nrm, Vector3.Cross(nrm, ax));
                ref var s = ref _t[i];
                s.Local = Matrix4x4.TRS(W(cell.Center), rot, Vector3.one);
                s.Center = W(cell.Center);
                s.Normal = nrm;
                s.Shape = shape;
                s.Kind = tile.Kind;
                s.Step = 360f / cell.Ports;
                int turns = WireImage(shape, cell.Ports, tile.Sol, out s.Pick);
                s.Angle = s.TurnTo = s.Step * (turns + tile.Rot);
                s.TurnTime = 0f;
                s.BurstStart = -1f;
            }
            ApplyTheme();
        }

        /// <summary>Which wire image draws a solved mask; returns the extra turns that image needs.</summary>
        static int WireImage(BoardLook.TileShape shape, int ports, int mask, out int pick)
        {
            var canon = shape.Canon;
            for (int i = 0; i < canon.Length; i++)
                for (int k = 0; k < ports; k++)
                    if (Circuit.RotN(canon[i], k, ports) == mask) { pick = i; return k; }
            pick = 0;
            return 0;
        }

        /// <summary>Re-skin the board in place (also the first skin after Build).</summary>
        public void ApplyTheme()
        {
            if (_t == null) return;
            var theme = Themes.Current;
            _groups.Clear();
            _groupByKey.Clear();
            float halo = theme != null ? theme.HaloAlpha : 0.85f;
            bool tintCore = theme == null || theme.TintLampCore;
            for (int i = 0; i < _t.Length; i++)
            {
                ref var s = ref _t[i];
                var skin = theme != null ? theme.SkinFor(s.Shape.Ports) : null;
                var tile = _level.Tiles[i];
                for (int l = 0; l < LayerCount; l++) _groupOf[i, l] = null;
                if (skin == null) continue;
                Bind(i, Lyr.Base, Tex(skin.Base), Cutoff(skin.Base));
                if (s.Pick < skin.Wires.Length) Bind(i, Lyr.Wire, Tex(skin.Wires[s.Pick]));
                if (s.Pick < skin.WireCores.Length) Bind(i, Lyr.Core, Tex(skin.WireCores[s.Pick]));
                Color c = CubeColors.Energy(tile.Color);
                if (s.Kind == TileKind.Lamp)
                {
                    Bind(i, Lyr.Halo, Tex(skin.LampHalo));
                    Bind(i, Lyr.Ring, Tex(skin.LampRing));
                    Bind(i, Lyr.CoreOff, Tex(skin.LampOff));
                    Bind(i, Lyr.CoreOn, Tex(skin.LampOn));
                    Bind(i, Lyr.Burst, Tex(skin.LampRing));   // the light-up shockwave is the lamp ring, expanding
                    s.RingColor = Lin(c);
                    s.CoreOnColor = Lin(tintCore ? c : Color.white);
                    s.HaloColor = Lin(new Color(c.r, c.g, c.b, halo));
                    s.BurstColor = Lin(new Color(c.r, c.g, c.b, 0.75f));
                }
                else if (s.Kind == TileKind.Source)
                {
                    Bind(i, Lyr.SourceHalo, Tex(skin.SourceHalo));
                    Bind(i, Lyr.SourceOutline, Tex(skin.SourceOutline));
                    Bind(i, Lyr.SourceCenter, Tex(skin.SourceCenter));
                    s.OutlineColor = Lin(c);
                    s.SourceHaloColor = Lin(theme != null ? new Color(c.r, c.g, c.b, halo) : c);
                }
                Bind(i, Lyr.Hint, _look.HintTexture);
            }
            Refresh(false);
            Draw();
        }

        static Texture Tex(Material m) => m != null ? m.mainTexture : null;

        /// <summary>The alpha cut of an alpha-clipped skin material (the triangle face), or 0.</summary>
        static float Cutoff(Material m) =>
            m != null && m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f && m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0f;

        void Bind(int tile, Lyr layer, Texture tex, float cutoff = 0f)
        {
            if (tex == null) return;
            int q = Queue[(int)layer];
            long key = ((long)q << 32) ^ (uint)tex.GetInstanceID();
            if (!_groupByKey.TryGetValue(key, out var g))
            {
                int cap = _t.Length;
                g = new Group { Mat = MaterialFor(tex, q, cutoff), M = new Matrix4x4[cap], C = new Vector4[cap], Props = new MaterialPropertyBlock() };
                _groupByKey.Add(key, g);
                _groups.Add(g);
            }
            _groupOf[tile, (int)layer] = g;
        }

        Material MaterialFor(Texture tex, int queue, float cutoff)
        {
            long key = ((long)queue << 32) ^ (uint)tex.GetInstanceID();
            if (Materials.TryGetValue(key, out var m) && m != null) return m;
            bool opaque = queue < 2500;
            m = new Material(_look.Shader) { name = "Board_" + tex.name, mainTexture = tex, renderQueue = queue, enableInstancing = true };
            m.SetFloat(SrcBlendId, opaque ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
            m.SetFloat(DstBlendId, opaque ? (float)BlendMode.Zero : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat(ZWriteId, opaque ? 1f : 0f);
            m.SetFloat(CutoffId, cutoff);
            m.SetOverrideTag("RenderType", opaque ? "Opaque" : "Transparent");
            Materials[key] = m;
            return m;
        }

        /// <summary>Recolours wires and lamps from the current rotations. Returns true when every lamp is right.</summary>
        public bool Refresh(bool animate = true)
        {
            _level.Components(false, _comp, _sets, _stack);
            var sets = _sets;
            int lit = 0, total = 0;
            float now = Time.time;
            for (int i = 0; i < _t.Length; i++)
            {
                var tile = _level.Tiles[i];
                ref var s = ref _t[i];
                int set = sets[_comp[i]];
                s.Powered = set != 0;
                s.WireColor = Lin(set != 0 ? CubeColors.Energy(set) : CubeColors.WireOffColor);
                if (tile.Locked != s.Hint)
                {
                    s.Hint = tile.Locked;
                    s.HintStart = now;
                    s.HintAnimated = animate;
                }
                if (tile.Kind != TileKind.Lamp) continue;
                total++;
                bool good = set == tile.Color;
                if (good) lit++;
                if (good == s.Lit) continue;
                s.Lit = good;
                if (good)
                {
                    s.LitStart = now;
                    s.LitAnimated = animate;
                    if (animate) s.BurstStart = now;
                }
                else
                {
                    s.OffStart = now;
                    s.OffAnimated = true;
                }
            }
            LitLamps = lit;
            TotalLamps = total;
            return lit == total;
        }

        /// <summary>Turn a tile's wire by <paramref name="steps"/> (negative = back), with a small overshoot.</summary>
        public void AnimateRotation(int index, int steps, float duration = -1f)
        {
            ref var s = ref _t[index];
            s.TurnFrom = s.Angle;
            s.TurnTo = s.TurnTo + s.Step * steps;
            s.TurnStart = Time.unscaledTime;
            s.TurnTime = duration >= 0f ? duration : _look.TurnTime;
        }

        public void Celebrate()
        {
            float now = Time.time;
            for (int i = 0; i < _t.Length; i++) if (_t[i].Kind == TileKind.Lamp) _t[i].BurstStart = now;
        }

        public Vector3 NormalWorld(int index) => transform.TransformDirection(_t[index].Normal);

        /// <summary>The visible tile a screen ray hits first, or -1. No colliders: ray against each tile's plane.</summary>
        public int Pick(Ray ray)
        {
            if (_t == null || !_interactive) return -1;
            var toContent = (transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * ContentScale)).inverse;
            Vector3 o = toContent.MultiplyPoint3x4(ray.origin), d = toContent.MultiplyVector(ray.direction);
            int best = -1;
            float bestT = float.MaxValue, bestD = float.MaxValue;
            for (int i = 0; i < _t.Length; i++)
            {
                ref var s = ref _t[i];
                if (!s.Visible) continue;
                float den = Vector3.Dot(d, s.Normal);
                if (den >= -1e-5f) continue;                       // parallel or seen from behind
                float t = Vector3.Dot(s.Center - o, s.Normal) / den;
                if (t <= 0f) continue;
                float dist = ((o + d * t) - s.Center).magnitude;
                if (dist > s.Shape.PickRadius) continue;
                // front-most face first; on the same face (overlapping triangle bounds) the nearest centre wins
                bool sameDepth = Mathf.Abs(t - bestT) < 0.02f * Mathf.Abs(bestT);
                if (t < bestT && !sameDepth || sameDepth && dist < bestD) { best = i; bestT = t; bestD = dist; }
            }
            return best;
        }

        void LateUpdate() => Draw();

        /// <summary>Advance the tile animations and draw every visible tile. Allocates nothing.</summary>
        public void Draw()
        {
            if (_level == null || _cam == null || _t == null) return;
            foreach (var g in _groups) g.N = 0;

            var theme = Themes.Current;
            Color dark = theme != null ? theme.ShadeDark : ShadeDark, light = theme != null ? theme.ShadeLight : ShadeLight;
            Matrix4x4 board = transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * ContentScale);
            Vector3 camPos = _cam.transform.position;
            float now = Time.time, unscaled = Time.unscaledTime;
            var look = _look;
            float onLen = BoardLook.Length(look.LampOnHalo), litLen = Mathf.Max(0.01f, BoardLook.Length(look.LampLitHalo));
            float breathLen = Mathf.Max(0.01f, BoardLook.Length(look.SourceBreath));
            float breath = look.SourceBreath.Evaluate(now % breathLen);
            float spin = (now * look.SourceSpin) % 360f;
            Vector4 white = new Vector4(1, 1, 1, 1);

            for (int i = 0; i < _t.Length; i++)
            {
                ref var s = ref _t[i];
                Matrix4x4 tw = board * s.Local;
                Vector3 nW = board.MultiplyVector(s.Normal).normalized;
                Vector3 pW = tw.MultiplyPoint3x4(Vector3.zero);
                s.Visible = Vector3.Dot(nW, camPos - pW) > 0f;
                if (!s.Visible) continue;
                var shape = s.Shape;

                // the tile turn (eased, slight overshoot)
                if (s.TurnTime > 0f)
                {
                    float p = Mathf.Clamp01((unscaled - s.TurnStart) / s.TurnTime);
                    float q = p + Mathf.Sin(p * Mathf.PI) * look.TurnOvershoot;
                    s.Angle = Mathf.LerpUnclamped(s.TurnFrom, s.TurnTo, q);
                    if (p >= 1f) { s.Angle = s.TurnTo; s.TurnTime = 0f; }
                }

                float shadeT = Mathf.Clamp01(Vector3.Dot(nW, -LightDir) * 0.5f + 0.5f);
                Add(i, Lyr.Base, tw, shape.Base, 0f, 1f, Lin(Color.Lerp(dark, light, shadeT)));
                Add(i, Lyr.Wire, tw, shape.Wire, s.Angle, 1f, s.WireColor);
                if (s.Powered) Add(i, Lyr.Core, tw, shape.Core, s.Angle, 1f, white);

                if (s.Kind == TileKind.Lamp)
                {
                    float t = now - s.LitStart;
                    if (s.Lit)
                    {
                        bool intro = s.LitAnimated && t < onLen;
                        float halo = intro ? look.LampOnHalo.Evaluate(t) : look.LampLitHalo.Evaluate((s.LitAnimated ? t - onLen : t) % litLen);
                        Add(i, Lyr.Halo, tw, shape.Halo, 0f, halo, s.HaloColor);
                        Add(i, Lyr.Ring, tw, shape.Ring, 0f, intro ? look.LampOnRing.Evaluate(t) : 1f, s.RingColor);
                        Add(i, Lyr.CoreOn, tw, shape.CoreOn, 0f, intro ? look.LampOnCore.Evaluate(t) : 1f, s.CoreOnColor);
                    }
                    else
                    {
                        float to = now - s.OffStart;
                        bool anim = s.OffAnimated && to < BoardLook.Length(look.LampOffCore);
                        Add(i, Lyr.Ring, tw, shape.Ring, 0f, 1f, s.RingColor);
                        Add(i, Lyr.CoreOff, tw, shape.CoreOff, 0f, anim ? look.LampOffCore.Evaluate(to) : 1f, white);
                    }
                    if (s.BurstStart >= 0f)
                    {
                        float tb = now - s.BurstStart;
                        if (tb < BoardLook.Length(look.Burst)) Add(i, Lyr.Burst, tw, shape.Burst, 0f, look.Burst.Evaluate(tb), s.BurstColor);
                        else s.BurstStart = -1f;
                    }
                }
                else if (s.Kind == TileKind.Source)
                {
                    Add(i, Lyr.SourceHalo, tw, shape.SourceHalo, spin, 1f, s.SourceHaloColor);
                    Add(i, Lyr.SourceOutline, tw, shape.SourceOutline, 0f, breath, s.OutlineColor);
                    Add(i, Lyr.SourceCenter, tw, shape.SourceCenter, 0f, breath, white);
                }

                if (s.Hint)
                {
                    float th = now - s.HintStart;
                    bool anim = s.HintAnimated && th < BoardLook.Length(look.HintPop);
                    Add(i, Lyr.Hint, tw, shape.HintPin, 0f, anim ? look.HintPop.Evaluate(th) : 1f, white);
                }
            }

            var bounds = new Bounds(transform.position, Vector3.one * (Radius * 2f + 2f));
            var mesh = look.Quad;
            int layer = gameObject.layer, calls = 0;
            foreach (var g in _groups)
            {
                if (g.N == 0) continue;
                calls++;
                g.Props.SetVectorArray(ColorId, g.C);
                var rp = new RenderParams(g.Mat)
                {
                    worldBounds = bounds,
                    matProps = g.Props,
                    layer = layer,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                };
                Graphics.RenderMeshInstanced(rp, mesh, 0, g.M, g.N);
            }
            LastDrawCalls = calls;
        }

        void Add(int tile, Lyr layer, in Matrix4x4 tileWorld, in BoardLook.Layer l, float angle, float scale, Vector4 color)
        {
            var g = _groupOf[tile, (int)layer];
            if (g == null || scale <= 0f) return;
            float size = l.Size * scale;
            g.M[g.N] = tileWorld * Matrix4x4.TRS(l.Offset, Quaternion.Euler(0f, 0f, angle + l.Angle), new Vector3(l.Mirror ? -size : size, size, 1f));
            g.C[g.N] = color;
            g.N++;
        }
    }
}
