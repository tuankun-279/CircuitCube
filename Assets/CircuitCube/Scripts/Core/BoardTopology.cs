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
        public static V3 Lerp(V3 a, V3 b, float t) => a + (b - a) * t;
        public override string ToString() => "(" + X + "," + Y + "," + Z + ")";
    }

    /// <summary>
    /// The outline a tile has, which picks its face image. Regular ones turn into themselves; the others are drawn
    /// from a fixed outline that every tile of the kind shares (see <see cref="PolyShapes.Outline"/>).
    /// </summary>
    public enum CellShape { Square, Triangle, Pentagon, Hexagon, Kite, MegaCorner, MegaEdge }

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
            public CellShape Shape;
            /// <summary>The tile's corners, counter-clockwise seen from outside; side k runs from corner k to k+1.</summary>
            public V3[] Corners;
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
    /// share a side. Used for the tetrahedron, octahedron, dodecahedra and the Goldberg balls (see <see cref="PolyShapes"/>).
    /// </summary>
    public sealed class PolyTopology : BoardTopology
    {
        public PolyTopology(List<V3[]> polygons, List<int> faces, List<CellShape> shapes)
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
                // Newell's normal: right for corners that are very slightly off a plane (Goldberg tiles)
                V3 n = new V3(0, 0, 0);
                for (int p = 0; p < k; p++) n = n + poly[p].Cross(poly[(p + 1) % k]);
                n = n.Normalized;
                V3 mid0 = (poly[0] + poly[1]) * 0.5f - c;
                V3 axis = (mid0 - n * mid0.Dot(n)).Normalized;
                Cells.Add(new Cell { Ports = k, Center = c, Normal = n, Axis = axis, Face = faces[i], Shape = shapes[i], Corners = poly });
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
}
