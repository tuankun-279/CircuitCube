using CircuitCube.Core;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Puts a level on screen: one Tile prefab instance per surface tile of the shape. Tile centres from the topology
    /// (2 units apart) are scaled by 0.5, so a tile is 1 world unit wide. Everything visible comes from the prefab.
    /// </summary>
    public sealed class CubeBoardView : MonoBehaviour
    {
        CubeLevel _level;
        BoardTile[] _tiles;
        Transform _content;
        Camera _cam;
        int[] _comp;
        Tweener _tw;
        Vector3[] _normals;

        // Tile brightness for faces turned away from the light and towards it.
        public static Color ShadeDark = new Color(0.62f, 0.64f, 0.72f, 1f);
        public static Color ShadeLight = Color.white;
        static readonly Vector3 LightDir = new Vector3(-0.45f, 0.75f, -0.5f).normalized;

        public int LitLamps { get; private set; }
        public int TotalLamps { get; private set; }
        /// <summary>Radius of the shape in world units.</summary>
        public float Radius => _level.Topo.Radius * 0.5f;

        static Vector3 W(V3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <param name="prefabs">Tile prefab per side count: index 3 triangle, 4 square, 5 pentagon.</param>
        public void Build(CubeLevel level, BoardTile[] prefabs, Camera cam, Tweener tw, bool interactive)
        {
            _level = level;
            _cam = cam;
            _tw = tw;
            _comp = new int[level.Tiles.Length];

            _content = new GameObject("Content").transform;
            _content.SetParent(transform, false);
            _content.localScale = Vector3.one * 0.5f;

            _tiles = new BoardTile[level.Tiles.Length];
            _normals = new Vector3[level.Tiles.Length];
            for (int i = 0; i < level.Tiles.Length; i++)
            {
                var cell = level.Topo.Cells[i];
                var tile = level.Tiles[i];
                _normals[i] = W(cell.Normal);
                var prefab = prefabs[cell.Ports];
                var t = Instantiate(prefab, _content);
                t.ApplySkin(Themes.Current);
                t.name = "Tile" + i;
                t.Index = i;
                t.Face = cell.Face;
                // local +X = port 0, local +Z = outward normal, ports go counter-clockwise
                Vector3 n = W(cell.Normal), ax = W(cell.Axis);
                t.transform.localPosition = W(cell.Center);
                t.transform.localRotation = Quaternion.LookRotation(n, Vector3.Cross(n, ax));
                int turns = t.SetWireShape(tile.Sol);
                t.Angle = t.Step * (turns + tile.Rot);
                t.WirePivot.localRotation = Quaternion.Euler(0, 0, t.Angle);
                t.SetKind(tile.Kind, tile.Color);
                var col = t.GetComponent<Collider>();
                if (col != null) col.enabled = interactive;
                _tiles[i] = t;
            }
            Refresh(false);
            LateUpdate();
        }

        /// <summary>Recolours wires and lamps from the current rotations. Returns true when every lamp is right.</summary>
        public bool Refresh(bool animate = true)
        {
            _level.Components(false, _comp, out int[] sets);
            int lit = 0, total = 0;
            for (int i = 0; i < _tiles.Length; i++)
            {
                var t = _level.Tiles[i];
                var tv = _tiles[i];
                int set = sets[_comp[i]];
                tv.SetWireColor(set != 0 ? CubeColors.Energy(set) : CubeColors.WireOffColor, set != 0);
                tv.ShowHint(t.Locked, animate);
                if (t.Kind == TileKind.Lamp)
                {
                    total++;
                    bool good = set == t.Color;
                    if (good) lit++;
                    tv.SetLit(good, animate);
                }
            }
            LitLamps = lit;
            TotalLamps = total;
            return lit == total;
        }

        /// <summary>The tile turn follows the move, so it is the one animation driven from code.</summary>
        public void AnimateRotation(int index, int steps, float duration = 0.18f)
        {
            var tv = _tiles[index];
            float from = tv.Angle;
            float to = from + tv.Step * steps;
            tv.Angle = to;
            var pivot = tv.WirePivot;
            _tw.Run(duration, p =>
            {
                // small overshoot at the end, like a click into place
                float q = p + Mathf.Sin(p * Mathf.PI) * 0.12f;
                pivot.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(from, to, q));
            }, null, tv);
        }

        public void Celebrate()
        {
            foreach (var t in _tiles) if (_level.Tiles[t.Index].Kind == TileKind.Lamp) t.PlayBurst();
        }

        /// <summary>
        /// The tile under a point the tap ray hit. Triangle tiles use box colliders that overlap their neighbours, so
        /// the tile whose centre is nearest the hit point wins (for triangle and square grids that is the tile the point is in).
        /// </summary>
        public int TileAt(Vector3 worldPoint, int hitIndex)
        {
            int best = hitIndex;
            float bestD = (_tiles[hitIndex].transform.position - worldPoint).sqrMagnitude;
            Vector3 n = transform.TransformDirection(_normals[hitIndex]);
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (!_tiles[i].Visible) continue;
                if (Vector3.Dot(transform.TransformDirection(_normals[i]), n) < 0.9f) continue;
                float d = (_tiles[i].transform.position - worldPoint).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>Re-skin the board in place after the theme changes.</summary>
        public void ApplyTheme()
        {
            if (_tiles == null) return;
            foreach (var t in _tiles)
            {
                t.ApplySkin(Themes.Current);
                var tile = _level.Tiles[t.Index];
                t.SetKind(tile.Kind, tile.Color);
            }
            Refresh(false);
        }

        public Vector3 NormalWorld(int index) => transform.TransformDirection(W(_level.Topo.Cells[index].Normal));

        void LateUpdate()
        {
            if (_level == null || _cam == null) return;
            Vector3 camPos = _cam.transform.position;
            for (int i = 0; i < _tiles.Length; i++)
            {
                var t = _tiles[i];
                Vector3 n = transform.TransformDirection(_normals[i]);
                bool visible = Vector3.Dot(n, camPos - t.transform.position) > 0f;
                t.SetVisible(visible);
                if (!visible) continue;
                float light = Mathf.Clamp01(Vector3.Dot(n, -LightDir) * 0.5f + 0.5f);
                var th = Themes.Current;
                t.SetBaseColor(th != null ? Color.Lerp(th.ShadeDark, th.ShadeLight, light) : Color.Lerp(ShadeDark, ShadeLight, light));
            }
        }
    }
}
