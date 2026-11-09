using System;
using System.Collections.Generic;
using System.Text;

namespace CircuitCube.Core
{
    /// <summary>
    /// The board of a level as one string, 3 characters per tile, in the shape's tile order:
    /// [wire mask in base64: 0-9 a-z A-Z - _][kind + colour][start rotation]. Kind + colour: '.' plain wire, 'r'/'b'/'y' a red/blue/yellow
    /// source, '1'..'7' a lamp that needs that colour set. Same format as Level Lab and the data config tool.
    /// </summary>
    public static class TileCodec
    {
        // 6 bits for hexagon tiles; the first 32 characters are the old base32, so older data reads the same
        const string B64 = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-_";

        public static string Encode(CubeTile[] tiles)
        {
            var sb = new StringBuilder(tiles.Length * 3);
            foreach (var t in tiles)
            {
                sb.Append(B64[t.Sol & 63]);
                if (t.Kind == TileKind.Source) sb.Append(t.Color == Circuit.Red ? 'r' : t.Color == Circuit.Blue ? 'b' : 'y');
                else if (t.Kind == TileKind.Lamp) sb.Append((char)('0' + t.Color));
                else sb.Append('.');
                int p = Math.Max(1, t.Ports);
                sb.Append((char)('0' + ((t.Rot % p) + p) % p));
            }
            return sb.ToString();
        }

        /// <summary>Tiles for <paramref name="topo"/>; null if the string does not fit the shape.</summary>
        public static CubeTile[] Decode(string tiles, BoardTopology topo)
        {
            if (string.IsNullOrEmpty(tiles) || tiles.Length != topo.Count * 3) return null;
            var o = new CubeTile[topo.Count];
            for (int i = 0; i < o.Length; i++)
            {
                int sol = B64.IndexOf(tiles[3 * i]);
                char k = tiles[3 * i + 1];
                int rot = tiles[3 * i + 2] - '0';
                if (sol < 0 || rot < 0 || rot > 9 || sol >= 1 << topo.Cells[i].Ports) return null;
                var t = new CubeTile { Sol = sol, Rot = rot, Ports = topo.Cells[i].Ports };
                if (k == 'r' || k == 'b' || k == 'y') { t.Kind = TileKind.Source; t.Color = k == 'r' ? Circuit.Red : k == 'b' ? Circuit.Blue : Circuit.Yellow; }
                else if (k >= '1' && k <= '7') { t.Kind = TileKind.Lamp; t.Color = k - '0'; }
                else if (k == '.') t.Kind = TileKind.Wire;
                else return null;
                o[i] = t;
            }
            return o;
        }

        /// <summary>Lamp colour sets in order of first appearance (for the colour legend).</summary>
        public static int[] LampColors(CubeTile[] tiles)
        {
            var list = new List<int>();
            foreach (var t in tiles) if (t.Kind == TileKind.Lamp && !list.Contains(t.Color)) list.Add(t.Color);
            return list.ToArray();
        }

        /// <summary>FNV-1a over wire + kind characters (rotation left out). Matches the web tools.</summary>
        public static string Check(string tiles)
        {
            uint h = 2166136261;
            for (int i = 0; i + 1 < tiles.Length; i += 3)
            {
                h = (h ^ tiles[i]) * 16777619;
                h = (h ^ tiles[i + 1]) * 16777619;
            }
            return h.ToString("x8");
        }

        public static int Par(CubeTile[] tiles)
        {
            int p = 0;
            foreach (var t in tiles) p += Circuit.TapsToSolve(t);
            return p;
        }

        public static float Difficulty(int par, int tileCount, int colourGroups)
        {
            if (tileCount <= 0) return 0f;
            double d = Math.Min(1.0, 0.75 * par / (tileCount * 1.2) + 0.08 * Math.Max(0, colourGroups - 1));
            return (float)Math.Round(d, 2);
        }
    }

    /// <summary>Score of a won level: 6 at par, at most 5 over par, minus hints, never below the minimum.</summary>
    public static class LevelScore
    {
        /// <summary>
        /// Most moves that still score as par: the level's par plus a margin for a few slips, at least
        /// <see cref="GameRules.expertSlackMin"/> taps and <see cref="GameRules.expertSlackPercent"/>% of par
        /// (rounded up). This is the "par" the player sees.
        /// </summary>
        public static int ExpertLimit(int par, GameRules r) =>
            par + Math.Max(r.expertSlackMin, (int)Math.Ceiling(par * r.expertSlackPercent / 100.0));

        public static int Points(int moves, int par, int hintsUsed, GameRules r)
        {
            int baseScore = moves <= ExpertLimit(par, r) ? r.parScore : r.overParScore;
            return Math.Max(r.minScore, baseScore - hintsUsed * r.hintPenalty);
        }

        /// <summary>Whole stars shown until the half-star / Expert art exists: 6 → 3, 5/4 → 2, 3 → 1.</summary>
        public static int WholeStars(int points) => points <= 0 ? 0 : Math.Max(1, points / 2);
    }
}
