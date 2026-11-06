using System.Collections.Generic;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// One look of the game (dark or light): UI colours by role, energy colours and the board materials.
    /// Assets: Assets/CircuitCube/Themes/Theme_Dark.asset and Theme_Light.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Circuit Cube/Theme", fileName = "Theme")]
    public sealed class GameTheme : ScriptableObject
    {
        public bool Dark = true;

        [Header("UI")]
        public List<RoleColor> Ui = new List<RoleColor>();

        [Header("Board")]
        public Color CameraBackground = Color.black;
        [Tooltip("Full-screen picture behind the board (Art/UI/Backgrounds). Empty = plain Background colour.")]
        public Sprite Backdrop;
        public Color ShadeDark = new Color(0.72f, 0.75f, 0.85f), ShadeLight = Color.white;
        [Tooltip("Energy colours: red, blue, yellow, purple (red+blue), orange (red+yellow), green (blue+yellow), white.")]
        public Color Red, Blue, Yellow, Purple, Orange, Green, White;
        public Color WireOff;
        [Tooltip("Light mode fills a lit lamp with its colour; dark mode keeps the core white.")]
        public bool TintLampCore;
        [Range(0f, 1f)] public float HaloAlpha = 0.45f;
        public BoardSkin Square = new BoardSkin(), Triangle = new BoardSkin();

        [Header("Board atlas (Tools/Circuit Cube/Build Atlases)")]
        [Tooltip("Every board image of this theme packed into one texture, so a layer needs one draw call whatever image it shows.")]
        public Texture2D BoardAtlas;
        public AtlasRect[] BoardAtlasRects = new AtlasRect[0];

        [System.NonSerialized] Dictionary<string, Vector4> _atlasUv;

        /// <summary>Where a board image (file name) sits in <see cref="BoardAtlas"/>: xy scale, zw offset.</summary>
        public bool AtlasUv(string image, out Vector4 uv)
        {
            uv = new Vector4(1f, 1f, 0f, 0f);
            if (BoardAtlas == null || string.IsNullOrEmpty(image)) return false;
            if (_atlasUv == null)
            {
                _atlasUv = new Dictionary<string, Vector4>();
                foreach (var r in BoardAtlasRects) if (!string.IsNullOrEmpty(r.Image)) _atlasUv[r.Image] = r.Uv;
            }
            return _atlasUv.TryGetValue(image, out uv);
        }

        void OnValidate() => _atlasUv = null;

        public Color Get(UiRole role)
        {
            if (role == UiRole.Transparent) return new Color(1, 1, 1, 0);
            foreach (var r in Ui) if (r.Role == role) return r.Color;
            return Color.white;
        }

        public Color Energy(int set)
        {
            switch (set)
            {
                case 1: return Red;
                case 2: return Blue;
                case 4: return Yellow;
                case 3: return Purple;
                case 5: return Orange;
                case 6: return Green;
                case 7: return White;
                default: return WireOff;
            }
        }

        public BoardSkin SkinFor(int ports) => ports == 3 ? Triangle : Square;
    }
}
