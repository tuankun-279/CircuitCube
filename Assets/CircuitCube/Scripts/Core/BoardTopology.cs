using System.Collections.Generic;

namespace CircuitCube.Core
{
    /// <summary>Small float vector for the rules layer (kept free of UnityEngine so the rules can be unit tested).</summary>
    public readonly struct V3
    {
        public readonly float X, Y, Z;
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator -(V3 a) => new V3(-a.X, -a.Y, -a.Z);
        public static V3 operator *(V3 a, float s) => new V3(a.X * s, a.Y * s, a.Z * s);
        public float Dot(V3 b) => X * b.X + Y * b.Y + Z * b.Z;
        public V3 Cross(V3 b) => new V3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
        public float Length => (float)System.Math.Sqrt(X * X + Y * Y + Z * Z);
        public V3 Normalized { get { float l = Length; return l > 1e-6f ? this * (1f / l) : this; } }
        public static V3 From(Vec3i v) => new V3(v.X, v.Y, v.Z);
        public override string ToString() => "(" + X + "," + Y + "," + Z + ")";
    }

    /// <summary>
    /// Tiles on the surface of a shape and how they link. Every tile is a polygon with Ports sides (4 = square,
    /// 3 = triangle, 5 = pentagon). Port k of a tile points from its centre to the middle of its side k; ports go
    /// counter-clockwise when the tile is seen from outside. Coordinates use the board's units: a square tile is 2 wide.
    /// </summary>
    public abstract class BoardTopology
    {
        public sealed class Cell
        {
            public int Ports;
            public V3 Center, Normal;
            /// <summary>Unit direction of port 0 (in the tile plane).</summary>
            public V3 Axis;
            /// <summary>Which flat side of the shape the tile is on (for hints and shading).</summary>
            public int Face;
        }

        public struct Link
        {
            public int Cell;  // neighbouring tile index
            public int Back;  // port on the neighbour that points back here
        }

        public readonly List<Cell> Cells = new List<Cell>();
        public Link[][] Links;
        /// <summary>Distance from the centre to the farthest point of the shape (for camera framing).</summary>
        public float Radius { get; protected set; }
        public int Count => Cells.Count;
    }

    /// <summary>
    /// A shape given as flat polygons (vertices counter-clockwise seen from outside). Two tiles are neighbours when they
    /// share a side. Used for the tetrahedron now, and for octahedra and dodecahedra later.
    /// </summary>
    public sealed class PolyTopology : BoardTopology
    {
        public PolyTopology(List<V3[]> polygons, List<int> faces)
        {
            var edgeOwner = new Dictionary<long, (int cell, int port)>();
            float r = 0f;
            Links = new Link[polygons.Count][];
            for (int i = 0; i < polygons.Count; i++)
            {
                var poly = polygons[i];
                int k = poly.Length;
                V3 c = new V3(0, 0, 0);
                foreach (var v in poly) { c = c + v; if (v.Length > r) r = v.Length; }
                c = c * (1f / k);
                V3 n = (poly[1] - poly[0]).Cross(poly[2] - poly[0]).Normalized;
                V3 mid0 = (poly[0] + poly[1]) * 0.5f;
                Cells.Add(new Cell { Ports = k, Center = c, Normal = n, Axis = (mid0 - c).Normalized, Face = faces[i] });
                Links[i] = new Link[k];
                for (int p = 0; p < k; p++) Links[i][p] = new Link { Cell = -1, Back = -1 };
            }
            Radius = r;
            for (int i = 0; i < polygons.Count; i++)
            {
                var poly = polygons[i];
                for (int p = 0; p < poly.Length; p++)
                {
                    long key = EdgeKey(poly[p], poly[(p + 1) % poly.Length]);
                    if (edgeOwner.TryGetValue(key, out var other))
                    {
                        Links[i][p] = new Link { Cell = other.cell, Back = other.port };
                        Links[other.cell][other.port] = new Link { Cell = i, Back = p };
                    }
                    else edgeOwner[key] = (i, p);
                }
            }
        }

        static long Q(float v) => (long)System.Math.Round(v * 64f);

        static long EdgeKey(V3 a, V3 b)
        {
            // the midpoint identifies a shared side; both tiles walk it in opposite directions
            V3 m = (a + b) * 0.5f;
            return ((Q(m.X) + 100000) * 200001 + (Q(m.Y) + 100000)) * 200001 + (Q(m.Z) + 100000);
        }
    }

    /// <summary>Polygon shapes.</summary>
    public static class PolyShapes
    {
        /// <summary>Side of one small triangle, in board units (a square tile is 2 wide).</summary>
        public const float TriangleSide = 2.6f;

        /// <summary>A regular tetrahedron with every face cut into n x n small triangles (like a Pyraminx).</summary>
        public static PolyTopology Tetrahedron(int n)
        {
            float edge = n * TriangleSide;
            // regular tetrahedron on alternate cube corners, scaled so the edge length is `edge`
            float s = edge / (2f * (float)System.Math.Sqrt(2.0));
            var A = new V3(s, s, s); var B = new V3(s, -s, -s); var C = new V3(-s, s, -s); var D = new V3(-s, -s, s);
            var faces = new[] { new[] { A, B, C }, new[] { A, D, B }, new[] { A, C, D }, new[] { B, D, C } };
            var polys = new List<V3[]>();
            var ids = new List<int>();
            for (int f = 0; f < faces.Length; f++)
            {
                V3 p0 = faces[f][0], p1 = faces[f][1], p2 = faces[f][2];
                // make sure the face winds counter-clockwise seen from outside
                if ((p1 - p0).Cross(p2 - p0).Dot(p0 + p1 + p2) < 0f) { var t = p1; p1 = p2; p2 = t; }
                V3 e1 = (p1 - p0) * (1f / n), e2 = (p2 - p0) * (1f / n);
                V3 G(int i, int j) => p0 + e1 * i + e2 * j;
                for (int i = 0; i < n; i++)
                    for (int j = 0; i + j < n; j++)
                    {
                        polys.Add(new[] { G(i, j), G(i + 1, j), G(i, j + 1) }); ids.Add(f);
                        if (i + j < n - 1) { polys.Add(new[] { G(i + 1, j), G(i + 1, j + 1), G(i, j + 1) }); ids.Add(f); }
                    }
            }
            return new PolyTopology(polys, ids);
        }
    }
}
