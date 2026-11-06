using System.Collections.Generic;

namespace CircuitCube.Core
{
    /// <summary>
    /// Builds a level from a known solution: grows one spanning tree per colour group over the shape's surface until
    /// every tile is used, turns the tree leaves into sources and lamps, then scrambles the rotations.
    /// Same algorithm as the web prototype that was play-tested.
    /// </summary>
    public static class CubeGenerator
    {
        /// <param name="scramble">Share of tiles turned away from the solution (0..1). Early levels scramble fewer tiles.</param>
        public static CubeLevel Generate(int number, BoardTopology topo, int[] groups, uint seed, int minPar, float scramble = 1f)
        {
            for (int tries = 0; tries < 6; tries++)
            {
                var level = TryGenerate(topo, groups, seed + (uint)(tries * 101), minPar, scramble);
                if (level != null)
                {
                    level.Number = number;
                    level.Colors = (int[])groups.Clone();
                    return level;
                }
            }
            // Last resort: a single red group always generates.
            var fallback = TryGenerate(topo, new[] { Circuit.Red }, seed ^ 0x5bd1e995u, 4, 1f);
            if (fallback == null) throw new System.InvalidOperationException("Level generation failed for level " + number);
            fallback.Number = number;
            fallback.Colors = new[] { Circuit.Red };
            return fallback;
        }

        static CubeLevel TryGenerate(BoardTopology topo, int[] groups, uint seed, int minPar, float scramble)
        {
            var R = new CubeRng(seed);
            int n = topo.Count;
            int maxLeaves = 6 + (n + 3 * groups.Length - 1) / (3 * groups.Length);
            var free = new List<int>(4);

            for (int attempt = 0; attempt < 4000; attempt++)
            {
                var owner = new int[n];
                var mask = new int[n];
                for (int i = 0; i < n; i++) owner[i] = -1;
                var members = new List<int>[groups.Length];
                var last = new int[groups.Length];
                bool ok = true;

                for (int g = 0; g < groups.Length; g++)
                {
                    members[g] = new List<int>();
                    int c, t = 0;
                    do { c = R.Range(n); t++; } while (owner[c] >= 0 && t < 200);
                    if (owner[c] >= 0) { ok = false; break; }
                    owner[c] = g;
                    members[g].Add(c);
                    last[g] = c;
                }
                if (!ok) continue;

                var active = new List<int>(groups.Length);
                var cand = new List<int>();
                while (true)
                {
                    active.Clear();
                    for (int g = 0; g < groups.Length; g++)
                        foreach (int c in members[g])
                            if (HasFree(topo, owner, c)) { active.Add(g); break; }
                    if (active.Count == 0) break;

                    int gi = active[R.Range(active.Count)];
                    int from = last[gi];
                    if (!(R.Next() < 0.6 && HasFree(topo, owner, from)))
                    {
                        cand.Clear();
                        foreach (int c in members[gi]) if (HasFree(topo, owner, c)) cand.Add(c);
                        from = cand[R.Range(cand.Count)];
                    }
                    free.Clear();
                    for (int k = 0; k < topo.Links[from].Length; k++) if (owner[topo.Links[from][k].Cell] < 0) free.Add(k);
                    int dir = free[R.Range(free.Count)];
                    var link = topo.Links[from][dir];
                    owner[link.Cell] = gi;
                    mask[from] |= 1 << dir;
                    mask[link.Cell] |= 1 << link.Back;
                    members[gi].Add(link.Cell);
                    last[gi] = link.Cell;
                }

                bool filled = true;
                for (int i = 0; i < n; i++) if (owner[i] < 0) { filled = false; break; }
                if (!filled) continue;

                var kind = new TileKind[n];
                var color = new int[n];
                for (int g = 0; g < groups.Length && ok; g++)
                {
                    int set = groups[g];
                    var leaves = new List<int>();
                    foreach (int c in members[g]) if (Circuit.Bits(mask[c]) == 1) leaves.Add(c);
                    int need = Circuit.Pop3(set);
                    if (leaves.Count < need + 1 || leaves.Count > need + maxLeaves) { ok = false; break; }
                    R.Shuffle(leaves);
                    var bits = new List<int>(3);
                    foreach (int b in new[] { 1, 2, 4 }) if ((set & b) != 0) bits.Add(b);
                    for (int i = 0; i < leaves.Count; i++)
                    {
                        if (i < need) { kind[leaves[i]] = TileKind.Source; color[leaves[i]] = bits[i]; }
                        else { kind[leaves[i]] = TileKind.Lamp; color[leaves[i]] = set; }
                    }
                }
                if (!ok) continue;

                var level = new CubeLevel { Topo = topo, Tiles = new CubeTile[n] };
                for (int i = 0; i < n; i++)
                    level.Tiles[i] = new CubeTile { Sol = mask[i], Kind = kind[i], Color = color[i], Ports = topo.Cells[i].Ports };
                if (!level.IsSolved(true)) continue;

                for (int s = 0; s < 60; s++)
                {
                    int par = 0;
                    foreach (var t in level.Tiles)
                    {
                        t.Rot = R.Next() < scramble ? 1 + R.Range(t.Ports - 1) : 0;
                        par += Circuit.TapsToSolve(t);
                    }
                    if (par >= minPar && !level.IsSolved(false))
                    {
                        level.Par = par;
                        return level;
                    }
                }
            }
            return null;
        }

        static bool HasFree(BoardTopology topo, int[] owner, int c)
        {
            var links = topo.Links[c];
            for (int k = 0; k < links.Length; k++) if (owner[links[k].Cell] < 0) return true;
            return false;
        }
    }
}
