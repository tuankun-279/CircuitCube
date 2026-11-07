using System.Collections.Generic;
using CircuitCube.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace CircuitCube.Game
{
    /// <summary>
    /// Draws a level without a GameObject, renderer or collider per tile:
    /// - the tile faces of the whole board are one mesh (one draw call), each tile a fan of triangles showing the face
    ///   image of its outline (square, triangle, pentagon, hexagon, kite, Megaminx pieces) out of the theme's atlas;
    /// - wires are drawn as arms: one stretched strip from the tile centre to the middle of each side it leaves by,
    ///   plus a round hub in the centre. Turning a tile slides every arm on to the next side, so tiles that are not
    ///   regular (kites, Goldberg hexagons) turn as cleanly as squares;
    /// - lamps, sources, hint pins and win bursts are instanced quads, one draw call per layer.
    /// Sizes, offsets and animation curves come from the BoardLook asset (built from the Tile prefabs), images from the
    /// theme. Nothing is allocated per frame.
    /// </summary>
    public sealed class CubeBoardView : MonoBehaviour
    {
        enum Lyr { Wire, Core, Halo, Ring, CoreOff, CoreOn, SourceHalo, SourceOutline, SourceCenter, Hint, Burst, Count }
        const int LayerCount = (int)Lyr.Count;
        // draw order on top of the faces, in the order the prefab stacked the layers
        static readonly int[] Queue = { 3000, 3001, 3002, 3003, 3004, 3004, 3002, 3003, 3004, 3005, 3006 };

        sealed class Group
        {
            public Material Mat;
            public Matrix4x4[] M;
            public Vector4[] C;
            public Vector4[] UV;
            public int N;
            public MaterialPropertyBlock Props;
        }

        struct TileState
        {
            public Matrix4x4 Local;          // tile frame inside the board content (content units)
            public Vector3 Center, Normal;   // content space
            public BoardLook.TileShape Shape;
            public float Scale;              // lamp and source size against the shape's own tile
            public Vector3 Pin;              // where the hint pin sits on the tile
            public TileKind Kind;
            public int Ports, Sol, Side;     // Side: first entry of this tile in the side and corner tables
            public float Rot;                // drawn rotation in steps (fractional while turning)
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
        float[] _sideAngle, _sideLength;    // per tile side: direction (radians, tile frame) and distance of its middle
        Vector3[] _corners;                 // per tile side: corner k (content space), for picking
        Group[,] _groupOf;                  // [tile, layer]
        Vector4[,] _uvOf;                   // [tile, layer]: where the layer's image sits in the atlas
        Vector4 _armUv, _hubUv, _armCoreUv, _hubCoreUv;
        Group _wire, _core;
        float _wireWidth = 1f, _hubSize = 1f;
        readonly List<Group> _groups = new List<Group>();
        readonly Dictionary<(int queue, int tex), Group> _groupByKey = new Dictionary<(int, int), Group>();
        int[] _comp, _sets, _stack;
        bool _interactive;
        Mesh _faces;
        Material _faceMat;

        static readonly Dictionary<(int queue, int tex), Material> Materials = new Dictionary<(int, int), Material>();
        static readonly Dictionary<int, Material> FaceMaterials = new Dictionary<int, Material>();
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int UvRectId = Shader.PropertyToID("_UVRect");
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        static readonly int ShadeDarkId = Shader.PropertyToID("_ShadeDark");
        static readonly int ShadeLightId = Shader.PropertyToID("_ShadeLight");
        static readonly int LightDirId = Shader.PropertyToID("_LightDir");

        // Tile brightness for faces turned away from the light and towards it (when no theme is set).
        public static Color ShadeDark = new Color(0.62f, 0.64f, 0.72f, 1f);
        public static Color ShadeLight = Color.white;
        static readonly Vector3 LightDir = new Vector3(-0.45f, 0.75f, -0.5f).normalized;
        const float ContentScale = 0.5f;     // topology tiles are 2 units apart; a tile is 1 world unit wide
        /// <summary>Corner of a face image's outline on the image, as a share of the half width (PolyShapes.Outline).</summary>
        public const float OutlineFill = 0.97f;
        /// <summary>How far an arm runs past the middle of its side (board units), covering the fold between tiles.</summary>
        const float ArmOverlap = 0.05f;

        public int LitLamps { get; private set; }
        public int TotalLamps { get; private set; }
        /// <summary>Radius of the shape in world units.</summary>
        public float Radius => _level.Topo.Radius * ContentScale;
        public int TileCount => _t != null ? _t.Length : 0;
        /// <summary>Draw calls the board issued last frame (the face mesh plus one per layer in use).</summary>
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
            var topo = level.Topo;
            int n = level.Tiles.Length, sides = 0;
            foreach (var c in topo.Cells) sides += c.Ports;
            _comp = new int[n];
            _sets = new int[n];
            _stack = new int[n];
            _t = new TileState[n];
            _sideAngle = new float[sides];
            _sideLength = new float[sides];
            _corners = new Vector3[sides];
            _groupOf = new Group[n, LayerCount];
            _uvOf = new Vector4[n, LayerCount];
            var square = look.ShapeFor(4);
            int side = 0;
            for (int i = 0; i < n; i++)
            {
                var cell = topo.Cells[i];
                var tile = level.Tiles[i];
                Vector3 nrm = W(cell.Normal), ax = W(cell.Axis), ctr = W(cell.Center);
                // local +X = port 0, local +Z = outward normal, ports go counter-clockwise
                var rot = Quaternion.LookRotation(nrm, Vector3.Cross(nrm, ax));
                ref var s = ref _t[i];
                s.Local = Matrix4x4.TRS(ctr, rot, Vector3.one);
                s.Center = ctr;
                s.Normal = nrm;
                s.Kind = tile.Kind;
                s.Ports = cell.Ports;
                s.Sol = tile.Sol;
                s.Side = side;
                s.Rot = s.TurnTo = tile.Rot;
                s.TurnTime = 0f;
                s.BurstStart = -1f;

                Vector3 ay = Vector3.Cross(nrm, ax);
                float inradius = float.MaxValue, far = 0f;
                Vector2 farCorner = Vector2.zero;
                for (int k = 0; k < cell.Ports; k++)
                {
                    Vector3 a = W(cell.Corners[k]), b = W(cell.Corners[(k + 1) % cell.Ports]);
                    Vector3 m = (a + b) * 0.5f - ctr;
                    float mx = Vector3.Dot(m, ax), my = Vector3.Dot(m, ay);
                    _sideAngle[side + k] = Mathf.Atan2(my, mx);
                    _sideLength[side + k] = Mathf.Sqrt(mx * mx + my * my);
                    inradius = Mathf.Min(inradius, _sideLength[side + k]);
                    _corners[side + k] = a;
                    Vector2 ca = new Vector2(Vector3.Dot(a - ctr, ax), Vector3.Dot(a - ctr, ay));
                    if (ca.magnitude > far) { far = ca.magnitude; farCorner = ca; }
                }
                side += cell.Ports;

                // squares and triangles keep the layers designed in their prefabs; other outlines borrow the square's,
                // sized to the tile
                var own = cell.Shape == CellShape.Triangle || cell.Shape == CellShape.Square ? look.ShapeFor(cell.Ports) : null;
                s.Shape = own ?? square;
                s.Scale = own != null ? 1f : Mathf.Clamp(inradius, 0.6f, 1.15f);
                // (Add scales the offset by the tile's size again)
                s.Pin = own != null ? s.Shape.HintPin.Offset : new Vector3(farCorner.x * 0.55f / s.Scale, farCorner.y * 0.55f / s.Scale, square.HintPin.Offset.z);
            }
            BuildFaces();
            ApplyTheme();
        }

        void OnDestroy()
        {
            if (_faces != null) Destroy(_faces);
        }

        /// <summary>
        /// The board's tile faces as one mesh: a fan per tile. Unity's front face is the one whose (b - a) x (c - a)
        /// points at the viewer, so the fan follows the corners as they are (outward normal).
        /// </summary>
        void BuildFaces()
        {
            var topo = _level.Topo;
            int verts = 0, tris = 0;
            foreach (var c in topo.Cells) { verts += c.Ports + 1; tris += c.Ports * 3; }
            var pos = new Vector3[verts];
            var nrm = new Vector3[verts];
            var idx = new int[tris];
            int v = 0, t = 0;
            foreach (var c in topo.Cells)
            {
                Vector3 n = W(c.Normal);
                int centre = v;
                pos[v] = W(c.Center); nrm[v++] = n;
                for (int k = 0; k < c.Ports; k++) { pos[v] = W(c.Corners[k]); nrm[v++] = n; }
                for (int k = 0; k < c.Ports; k++)
                {
                    idx[t++] = centre;
                    idx[t++] = centre + 1 + k;
                    idx[t++] = centre + 1 + (k + 1) % c.Ports;
                }
            }
            if (_faces == null) _faces = new Mesh { name = "BoardFaces" };
            _faces.Clear();
            _faces.indexFormat = verts > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _faces.vertices = pos;
            _faces.normals = nrm;
            _faces.uv = new Vector2[verts];
            _faces.triangles = idx;
            _faces.RecalculateBounds();
        }

        /// <summary>Point the face mesh UVs at the current theme's face images.</summary>
        void FaceUvs(GameTheme theme)
        {
            var topo = _level.Topo;
            var uv = new Vector2[_faces.vertexCount];
            int v = 0;
            for (int i = 0; i < topo.Count; i++)
            {
                var c = topo.Cells[i];
                theme.AtlasUv(theme.FaceImage(c.Shape), out var rect);
                Vector2 Atlas(Vector2 p) => new Vector2(p.x * rect.x + rect.z, p.y * rect.y + rect.w);
                uv[v++] = Atlas(ImageUv(i, -1));
                for (int k = 0; k < c.Ports; k++) uv[v++] = Atlas(ImageUv(i, k));
            }
            _faces.uv = uv;
        }

        /// <summary>
        /// Where corner k (or the centre, k = -1) of a tile lands on its face image. Squares and triangles use the face
        /// layer of their prefab, exactly as the image was designed; other outlines use their shared outline.
        /// </summary>
        Vector2 ImageUv(int tile, int k)
        {
            var cell = _level.Topo.Cells[tile];
            if (k < 0) return new Vector2(0.5f, 0.5f);
            if (cell.Shape == CellShape.Square || cell.Shape == CellShape.Triangle)
            {
                ref var s = ref _t[tile];
                Vector3 local = s.Local.inverse.MultiplyPoint3x4(W(cell.Corners[k]));
                var b = s.Shape.Base;
                // inverse of the face layer's quad placement: offset, turn, size, mirror
                Vector2 p = new Vector2(local.x - b.Offset.x, local.y - b.Offset.y);
                float a = -b.Angle * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                p = new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs) / b.Size;
                if (b.Mirror) p.x = -p.x;
                return p + new Vector2(0.5f, 0.5f);
            }
            var o = PolyShapes.Outline(cell.Shape);
            return new Vector2(0.5f + o[2 * k] * 0.5f * OutlineFill, 0.5f + o[2 * k + 1] * 0.5f * OutlineFill);
        }

        /// <summary>Re-skin the board in place (also the first skin after Build).</summary>
        public void ApplyTheme()
        {
            if (_t == null) return;
            var theme = Themes.Current;
            _groups.Clear();
            _groupByKey.Clear();
            _wire = _core = null;
            for (int i = 0; i < _t.Length; i++)
                for (int l = 0; l < LayerCount; l++) _groupOf[i, l] = null;
            if (theme == null || theme.BoardAtlas == null) { _faceMat = null; return; }

            _faceMat = FaceMaterial(theme);
            FaceUvs(theme);
            var w = theme.Wires;
            _wireWidth = w.Width;
            _hubSize = w.HubSize;
            int arms = 0;
            foreach (var s in _t) arms += Circuit.Bits(s.Sol) + 1;
            _wire = Image(w.Arm, Lyr.Wire, arms, out _armUv) ? _groupByKey[(Queue[(int)Lyr.Wire], theme.BoardAtlas.GetInstanceID())] : null;
            Image(w.Hub, Lyr.Wire, arms, out _hubUv);
            _core = Image(w.ArmCore, Lyr.Core, arms, out _armCoreUv) ? _groupByKey[(Queue[(int)Lyr.Core], theme.BoardAtlas.GetInstanceID())] : null;
            Image(w.HubCore, Lyr.Core, arms, out _hubCoreUv);

            float halo = theme.HaloAlpha;
            bool tintCore = theme.TintLampCore;
            for (int i = 0; i < _t.Length; i++)
            {
                ref var s = ref _t[i];
                var skin = theme.SkinFor(s.Shape.Ports);
                var tile = _level.Tiles[i];
                Color c = CubeColors.Energy(tile.Color);
                if (s.Kind == TileKind.Lamp)
                {
                    Bind(i, Lyr.Halo, skin.LampHalo);
                    Bind(i, Lyr.Ring, skin.LampRing);
                    Bind(i, Lyr.CoreOff, skin.LampOff);
                    Bind(i, Lyr.CoreOn, skin.LampOn);
                    Bind(i, Lyr.Burst, skin.LampRing);   // the light-up shockwave is the lamp ring, expanding
                    s.RingColor = Lin(c);
                    s.CoreOnColor = Lin(tintCore ? c : Color.white);
                    s.HaloColor = Lin(new Color(c.r, c.g, c.b, halo));
                    s.BurstColor = Lin(new Color(c.r, c.g, c.b, 0.75f));
                }
                else if (s.Kind == TileKind.Source)
                {
                    Bind(i, Lyr.SourceHalo, skin.SourceHalo);
                    Bind(i, Lyr.SourceOutline, skin.SourceOutline);
                    Bind(i, Lyr.SourceCenter, skin.SourceCenter);
                    s.OutlineColor = Lin(c);
                    s.SourceHaloColor = Lin(new Color(c.r, c.g, c.b, halo));
                }
                if (_look.HintTexture != null) Bind(i, Lyr.Hint, _look.HintTexture.name);
            }
            Refresh(false);
            Draw();
        }

        Material FaceMaterial(GameTheme theme)
        {
            int key = theme.BoardAtlas.GetInstanceID();
            if (!FaceMaterials.TryGetValue(key, out var m) || m == null)
            {
                m = new Material(_look.FaceShader) { name = "BoardFaces_" + theme.BoardAtlas.name, mainTexture = theme.BoardAtlas, renderQueue = 2000 };
                FaceMaterials[key] = m;
            }
            m.SetColor(ShadeDarkId, theme.ShadeDark);
            m.SetColor(ShadeLightId, theme.ShadeLight);
            m.SetVector(LightDirId, LightDir);
            return m;
        }

        /// <summary>A wire image shared by every tile: its UV and the layer's draw group (sized for all arms and hubs).</summary>
        bool Image(string image, Lyr layer, int capacity, out Vector4 uv)
        {
            var theme = Themes.Current;
            if (!theme.AtlasUv(image, out uv))
            {
                Debug.LogWarning("[CircuitCube] Board image not in the atlas: " + image + " (Tools/Circuit Cube/Build Atlases)");
                return false;
            }
            GroupFor(layer, capacity);
            return true;
        }

        /// <summary>Put a tile layer in the draw group of its layer (one group per layer: every image is in the atlas).</summary>
        void Bind(int tile, Lyr layer, string image)
        {
            var theme = Themes.Current;
            if (theme == null || !theme.AtlasUv(image, out var uv))
            {
                if (!string.IsNullOrEmpty(image)) Debug.LogWarning("[CircuitCube] Board image not in the atlas: " + image + " (Tools/Circuit Cube/Build Atlases)");
                return;
            }
            _uvOf[tile, (int)layer] = uv;
            _groupOf[tile, (int)layer] = GroupFor(layer, _t.Length);
        }

        Group GroupFor(Lyr layer, int capacity)
        {
            Texture tex = Themes.Current.BoardAtlas;
            int q = Queue[(int)layer];
            var key = (q, tex.GetInstanceID());
            if (_groupByKey.TryGetValue(key, out var g))
            {
                if (g.M.Length < capacity) { g.M = new Matrix4x4[capacity]; g.C = new Vector4[capacity]; g.UV = new Vector4[capacity]; }
                return g;
            }
            g = new Group { Mat = MaterialFor(tex, q), M = new Matrix4x4[capacity], C = new Vector4[capacity], UV = new Vector4[capacity], Props = new MaterialPropertyBlock() };
            _groupByKey.Add(key, g);
            _groups.Add(g);
            return g;
        }

        Material MaterialFor(Texture tex, int queue)
        {
            var key = (queue, tex.GetInstanceID());
            if (Materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(_look.Shader) { name = "Board_" + tex.name + "_" + queue, mainTexture = tex, renderQueue = queue, enableInstancing = true };
            m.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            m.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat(ZWriteId, 0f);
            m.SetFloat(CutoffId, 0f);
            m.SetOverrideTag("RenderType", "Transparent");
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
            s.TurnFrom = s.Rot;
            s.TurnTo = s.TurnTo + steps;
            s.TurnStart = Time.unscaledTime;
            s.TurnTime = duration >= 0f ? duration : _look.TurnTime;
            if (s.TurnTime <= 0f) s.Rot = s.TurnTo;
        }

        public void Celebrate()
        {
            float now = Time.time;
            for (int i = 0; i < _t.Length; i++) if (_t[i].Kind == TileKind.Lamp) _t[i].BurstStart = now;
        }

        public Vector3 NormalWorld(int index) => transform.TransformDirection(_t[index].Normal);

        /// <summary>The visible tile a screen ray hits first, or -1. No colliders: ray against each tile's polygon.</summary>
        public int Pick(Ray ray)
        {
            if (_t == null || !_interactive) return -1;
            var toContent = (transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * ContentScale)).inverse;
            Vector3 o = toContent.MultiplyPoint3x4(ray.origin), d = toContent.MultiplyVector(ray.direction);
            int best = -1;
            float bestT = float.MaxValue;
            for (int i = 0; i < _t.Length; i++)
            {
                ref var s = ref _t[i];
                if (!s.Visible) continue;
                float den = Vector3.Dot(d, s.Normal);
                if (den >= -1e-5f) continue;                       // parallel or seen from behind
                float t = Vector3.Dot(s.Center - o, s.Normal) / den;
                if (t <= 0f || t >= bestT) continue;
                Vector3 p = o + d * t;
                bool inside = true;
                for (int k = 0; k < s.Ports && inside; k++)
                {
                    Vector3 a = _corners[s.Side + k], b = _corners[s.Side + (k + 1) % s.Ports];
                    // a little slack so a tap right on a seam still counts
                    inside = Vector3.Dot(Vector3.Cross(b - a, p - a), s.Normal) >= -0.05f * (b - a).magnitude;
                }
                if (inside) { best = i; bestT = t; }
            }
            return best;
        }

        void LateUpdate() => Draw();

        /// <summary>Advance the tile animations and draw every visible tile. Allocates nothing.</summary>
        public void Draw()
        {
            if (_level == null || _cam == null || _t == null) return;
            foreach (var g in _groups) g.N = 0;

            Matrix4x4 board = transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * ContentScale);
            Vector3 camPos = _cam.transform.position;
            float now = Time.time, unscaled = Time.unscaledTime;
            var look = _look;
            float onLen = BoardLook.Length(look.LampOnHalo), litLen = Mathf.Max(0.01f, BoardLook.Length(look.LampLitHalo));
            float breathLen = Mathf.Max(0.01f, BoardLook.Length(look.SourceBreath));
            float breath = look.SourceBreath.Evaluate(now % breathLen);
            float spin = (now * look.SourceSpin) % 360f;
            Vector4 white = new Vector4(1, 1, 1, 1);
            var square = look.ShapeFor(4);
            float wireZ = square.Wire.Offset.z, coreZ = square.Core.Offset.z;

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
                    s.Rot = Mathf.LerpUnclamped(s.TurnFrom, s.TurnTo, q);
                    if (p >= 1f) { s.Rot = s.TurnTo; s.TurnTime = 0f; }
                }

                if (s.Sol != 0) Arms(ref s, tw, wireZ, coreZ, white);

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
                    var pin = shape.HintPin;
                    pin.Offset = s.Pin;
                    Add(i, Lyr.Hint, tw, pin, 0f, anim ? look.HintPop.Evaluate(th) : 1f, white);
                }
            }

            var bounds = new Bounds(transform.position, Vector3.one * (Radius * 2f + 2f));
            int layer = gameObject.layer, calls = 0;
            if (_faceMat != null)
            {
                var fp = new RenderParams(_faceMat) { worldBounds = bounds, layer = layer, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                Graphics.RenderMesh(fp, _faces, 0, board);
                calls++;
            }
            var mesh = look.Quad;
            foreach (var g in _groups)
            {
                if (g.N == 0) continue;
                calls++;
                g.Props.SetVectorArray(ColorId, g.C);
                g.Props.SetVectorArray(UvRectId, g.UV);
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

        /// <summary>
        /// The tile's wire: an arm from the centre to the middle of every side the wire leaves by, and a hub. While the
        /// tile turns, each arm slides from one side towards the next (angle and length both eased).
        /// </summary>
        void Arms(ref TileState s, in Matrix4x4 tw, float wireZ, float coreZ, Vector4 white)
        {
            if (_wire == null) return;
            // the hub goes first so the arms lie over it: it only fills the outer corner of a bend, and a straight wire
            // shows no bead where its two arms meet
            Hub(_wire, tw, wireZ, _hubSize, _hubUv, s.WireColor);
            if (s.Powered && _core != null) Hub(_core, tw, coreZ, _hubSize, _hubCoreUv, white);
            int n = s.Ports;
            float fl = Mathf.Floor(s.Rot), f = s.Rot - fl;
            int baseRot = ((int)fl % n + n) % n;
            for (int j = 0; j < n; j++)
            {
                if (((s.Sol >> j) & 1) == 0) continue;
                int k0 = (j + baseRot) % n, k1 = (k0 + 1) % n;
                float a0 = _sideAngle[s.Side + k0], a1 = _sideAngle[s.Side + k1];
                float da = Mathf.Repeat(a1 - a0 + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
                float a = a0 + da * f;
                // a little past the side, so the neighbour's arm overlaps it where the tiles fold and no seam shows
                float len = Mathf.Lerp(_sideLength[s.Side + k0], _sideLength[s.Side + k1], f) + ArmOverlap;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                Arm(_wire, tw, cs, sn, len, wireZ, _wireWidth, _armUv, s.WireColor);
                if (s.Powered && _core != null) Arm(_core, tw, cs, sn, len, coreZ, _wireWidth, _armCoreUv, white);
            }
        }

        /// <summary>A strip from the tile centre along (cs, sn) for <paramref name="len"/>, <paramref name="width"/> wide.</summary>
        static void Arm(Group g, in Matrix4x4 tw, float cs, float sn, float len, float z, float width, Vector4 uv, Vector4 color)
        {
            // columns: x along the arm scaled by its length, y across scaled by the width, centred half way out
            var m = new Matrix4x4(
                new Vector4(cs * len, sn * len, 0f, 0f),
                new Vector4(-sn * width, cs * width, 0f, 0f),
                new Vector4(0f, 0f, 1f, 0f),
                new Vector4(cs * len * 0.5f, sn * len * 0.5f, z, 1f));
            g.M[g.N] = tw * m;
            g.C[g.N] = color;
            g.UV[g.N] = uv;
            g.N++;
        }

        static void Hub(Group g, in Matrix4x4 tw, float z, float size, Vector4 uv, Vector4 color)
        {
            var m = Matrix4x4.identity;
            m.m00 = size; m.m11 = size; m.m23 = z;
            g.M[g.N] = tw * m;
            g.C[g.N] = color;
            g.UV[g.N] = uv;
            g.N++;
        }

        void Add(int tile, Lyr layer, in Matrix4x4 tileWorld, in BoardLook.Layer l, float angle, float scale, Vector4 color)
        {
            var g = _groupOf[tile, (int)layer];
            if (g == null || scale <= 0f) return;
            float k = _t[tile].Scale, size = l.Size * scale * k;
            var offset = new Vector3(l.Offset.x * k, l.Offset.y * k, l.Offset.z);
            g.M[g.N] = tileWorld * Matrix4x4.TRS(offset, Quaternion.Euler(0f, 0f, angle + l.Angle), new Vector3(l.Mirror ? -size : size, size, 1f));
            g.C[g.N] = color;
            g.UV[g.N] = _uvOf[tile, (int)layer];
            g.N++;
        }
    }
}
