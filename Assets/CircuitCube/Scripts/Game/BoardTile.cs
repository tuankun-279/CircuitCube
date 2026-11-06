using CircuitCube.Core;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// The design source of a tile (Assets/CircuitCube/Prefabs/Board/Tile.prefab and TriTile.prefab): every layer is
    /// an image set up in the editor and every animation is a clip on the prefab. The game no longer instantiates
    /// it; Tools/Circuit Cube/Board/Build Board Look reads the layers and clips into BoardLook, which the instanced
    /// board renderer (CubeBoardView) draws from. The methods below still work for previewing the prefab on its own.
    /// </summary>
    public sealed class BoardTile : MonoBehaviour
    {
        [Header("Shape")]
        [Tooltip("Sides of this tile: 4 square, 3 triangle, 5 pentagon. One tap turns 360/Ports degrees.")]
        public int Ports = 4;
        [Tooltip("Wire mask drawn by each image in Wires, in the same order (bit k = arm towards side k).")]
        public int[] Canon = { 0b0001, 0b0101, 0b0011, 0b0111, 0b1111 };

        [Header("Layers")]
        public Renderer Base;
        public Transform WirePivot;
        [Tooltip("One image per entry of Canon. Arm k points at side k; side 0 is local +X, then counter-clockwise.")]
        public Renderer[] Wires = new Renderer[5];
        [Tooltip("White core layer of each wire (child of the wire, not tinted). Shown only while the wire carries energy.")]
        public Renderer[] WireCores = new Renderer[5];
        public GameObject Lamp;
        public Renderer LampSocket, LampBulbOff, LampBulbOn, LampGlow;
        public GameObject Source;
        public Renderer SourceDisc, SourceRing, SourceMark;
        public GameObject HintPin;
        public Renderer Burst;

        [Header("Animations (clips on the prefab)")]
        public Animation LampAnim;      // clips: LampOn, LampLit (loop), LampOff
        public Animation SourceAnim;    // clip: SourceIdle (loop)
        public Animation HintAnim;      // clip: HintPop
        public Animation BurstAnim;     // clip: Burst

        [HideInInspector] public int Index;
        [HideInInspector] public float Angle;
        [HideInInspector] public int Face;

        Renderer _wire, _core;
        GameTheme _theme;
        bool _lit;
        static MaterialPropertyBlock _mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public float Step => 360f / Ports;

        public static void Tint(Renderer r, Color c)
        {
            if (r == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_mpb);
        }

        /// <summary>Choose the wire image for a solved mask; returns the extra quarter turns that image needs.</summary>
        public int SetWireShape(int mask)
        {
            int pick = 0, turns = 0;
            for (int i = 0; i < Canon.Length; i++)
                for (int k = 0; k < 4; k++)
                    if (k < Ports && Circuit.RotN(Canon[i], k, Ports) == mask) { pick = i; turns = k; goto found; }
            found:
            for (int i = 0; i < Wires.Length; i++) if (Wires[i] != null) Wires[i].gameObject.SetActive(i == pick);
            _wire = Wires[pick];
            _core = WireCores != null && pick < WireCores.Length ? WireCores[pick] : null;
            return turns;
        }

        public void SetKind(TileKind kind, int color)
        {
            if (Lamp != null) Lamp.SetActive(kind == TileKind.Lamp);
            if (Source != null) Source.SetActive(kind == TileKind.Source);
            if (HintPin != null) HintPin.SetActive(false);
            if (Burst != null) Burst.gameObject.SetActive(false);
            Color c = CubeColors.Energy(color);
            var theme = _theme != null ? _theme : Themes.Current;   // SetKind can run before ApplySkin
            float halo = theme != null ? theme.HaloAlpha : 0.85f;
            bool tintCore = theme == null || theme.TintLampCore;
            if (kind == TileKind.Lamp)
            {
                Tint(LampSocket, c);
                Tint(LampBulbOn, tintCore ? c : Color.white);
                Tint(LampGlow, new Color(c.r, c.g, c.b, halo));
                LampBulbOn.gameObject.SetActive(false);
                LampGlow.gameObject.SetActive(false);
                LampBulbOff.gameObject.SetActive(true);
                _lit = false;
            }
            else if (kind == TileKind.Source)
            {
                Tint(SourceDisc, c);
                Tint(SourceRing, theme != null ? new Color(c.r, c.g, c.b, halo) : c);
                if (SourceAnim != null) SourceAnim.Play();
            }
            if (Burst != null) Tint(Burst, new Color(c.r, c.g, c.b, 0.75f));
        }

        public void SetWireColor(Color c) => Tint(_wire, c);

        /// <summary>Wire colour, and the white core only while energy flows through it.</summary>
        public void SetWireColor(Color c, bool powered)
        {
            Tint(_wire, c);
            if (_core != null) _core.gameObject.SetActive(powered);
        }

        /// <summary>Swap every layer to the theme's materials (shared materials, so tiles still batch).</summary>
        public void ApplySkin(GameTheme theme)
        {
            if (theme == null) return;
            _theme = theme;
            var s = theme.SkinFor(Ports);
            Use(Base, s.Base);
            for (int i = 0; i < Wires.Length; i++)
            {
                if (i < s.Wires.Length) Use(Wires[i], s.Wires[i]);
                if (WireCores != null && i < WireCores.Length && i < s.WireCores.Length) Use(WireCores[i], s.WireCores[i]);
            }
            Use(LampGlow, s.LampHalo); Use(LampSocket, s.LampRing); Use(LampBulbOff, s.LampOff); Use(LampBulbOn, s.LampOn);
            Use(SourceRing, s.SourceHalo); Use(SourceDisc, s.SourceOutline); Use(SourceMark, s.SourceCenter);
            Use(Burst, s.LampRing);   // the light-up shockwave is the lamp ring, expanding
        }

        static void Use(Renderer r, Material m) { if (r != null && m != null) r.sharedMaterial = m; }

        public void SetBaseColor(Color c) => Tint(Base, c);

        /// <summary>Lamp on or off. Plays LampOn then the LampLit loop when it turns on.</summary>
        public void SetLit(bool lit, bool animate)
        {
            if (Lamp == null || !Lamp.activeSelf || lit == _lit) return;
            _lit = lit;
            LampBulbOn.gameObject.SetActive(lit);
            LampGlow.gameObject.SetActive(lit);
            LampBulbOff.gameObject.SetActive(!lit);
            if (LampAnim == null) return;
            if (lit)
            {
                if (animate)
                {
                    LampAnim.Play("LampOn");
                    LampAnim.PlayQueued("LampLit");
                    PlayBurst();
                }
                else LampAnim.Play("LampLit");
            }
            else LampAnim.Play("LampOff");
        }

        public void ShowHint(bool show, bool animate)
        {
            if (HintPin == null) return;
            bool was = HintPin.activeSelf;
            HintPin.SetActive(show);
            if (show && !was && animate && HintAnim != null) HintAnim.Play("HintPop");
        }

        public void PlayBurst()
        {
            if (Burst == null || BurstAnim == null) return;
            Burst.gameObject.SetActive(true);
            BurstAnim.Stop();
            BurstAnim.Play("Burst");
        }

        Renderer[] _all;
        Collider _col;
        bool _visible = true;
        public bool Visible => _visible;

        /// <summary>Turned-away tiles are skipped entirely (cheaper; the depth buffer handles everything else).</summary>
        public void SetVisible(bool v)
        {
            if (v == _visible) return;
            _visible = v;
            if (_all == null) { _all = GetComponentsInChildren<Renderer>(true); _col = GetComponent<Collider>(); }
            foreach (var r in _all) r.enabled = v;
            if (_col != null) _col.enabled = v;
        }
    }
}
