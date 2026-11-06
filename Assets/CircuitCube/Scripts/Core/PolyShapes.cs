using System;
using System.Collections.Generic;

namespace CircuitCube.Core
{
    /// <summary>
    /// Shapes made of flat polygon tiles. Ids in the level data (see ShapeResolver):
    /// <code>
    /// tetra-n     tetrahedron, n x n triangles per face            36, 64, 100, 144 tiles (n = 3..6)
    /// octa-n      octahedron, n x n triangles per face             32, 72, 128, 200 tiles (n = 2..5)
    /// dodeca-2    Kilominx: each pentagon cut into 5 kites          60 tiles
    /// dodeca-3    Megaminx: centre pentagon, 5 edges, 5 corners     132 tiles
    /// geo-1       soccer ball: 12 pentagons, 20 hexagons            32 tiles
    /// geo-f       Goldberg ball GP(f,0), f = 2..4                   42, 92, 162 tiles
    /// </code>
    /// Sizes are in board units (a square tile is 2 wide): triangles have the tetra's side, the other tiles are scaled
    /// so that a tile is about as wide as a square one. Corners go counter-clockwise seen from outside.
    /// </summary>
    public static class PolyShapes
    {
        /// <summary>Side of one small triangle, in board units (a square tile is 2 wide).</summary>
        public const float TriangleSide = 2.6f;
        /// <summary>Mean distance from a tile's centre to its sides for the dodecahedra and balls (a square has 1).</summary>
        public const double TileInradius = 1.08;
        /// <summary>Megaminx: where the centre pentagon's corners sit between the face centre and its corners.</summary>
        public const double MegaCentre = 0.42;

        // ------------------------------------------------------------------ small double vector

        readonly struct D3
        {
            public readonly double X, Y, Z;
            public D3(double x, double y, double z) { X = x; Y = y; Z = z; }
            public static D3 operator +(D3 a, D3 b) => new D3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static D3 operator -(D3 a, D3 b) => new D3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static D3 operator *(D3 a, double s) => new D3(a.X * s, a.Y * s, a.Z * s);
            public double Dot(D3 b) => X * b.X + Y * b.Y + Z * b.Z;
            public D3 Cross(D3 b) => new D3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
            public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
            public D3 WithLength(double l) => this * (l / Length);
            public static D3 Lerp(D3 a, D3 b, double t) => a + (b - a) * t;
            public V3 F => new V3((float)X, (float)Y, (float)Z);
            public long Key => ((long)Math.Round(X * 1e4) * 4000003L + (long)Math.Round(Y * 1e4)) * 4000037L + (long)Math.Round(Z * 1e4);
        }

        sealed class Builder
        {
            public readonly List<D3[]> Polys = new List<D3[]>();
            public readonly List<int> Faces = new List<int>();
            public readonly List<CellShape> Shapes = new List<CellShape>();

            public void Add(D3[] poly, int face, CellShape shape)
            {
                // counter-clockwise seen from outside (every shape here is convex around the origin)
                D3 c = Centre(poly), n = new D3(0, 0, 0);
                for (int i = 0; i < poly.Length; i++) n = n + poly[i].Cross(poly[(i + 1) % poly.Length]);
                if (n.Dot(c) < 0) Array.Reverse(poly);
                Polys.Add(poly); Faces.Add(face); Shapes.Add(shape);
            }

            /// <summary>Scale everything so a tile's centre is <paramref name="inradius"/> from its sides on average.</summary>
            public void ScaleTo(double inradius)
            {
                double sum = 0;
                foreach (var p in Polys) sum += Inradius(p);
                double s = inradius / (sum / Polys.Count);
                for (int i = 0; i < Polys.Count; i++)
                    for (int k = 0; k < Polys[i].Length; k++) Polys[i][k] = Polys[i][k] * s;
            }

            public PolyTopology Build()
            {
                var polys = new List<V3[]>(Polys.Count);
                foreach (var p in Polys)
                {
                    var f = new V3[p.Length];
                    for (int k = 0; k < p.Length; k++) f[k] = p[k].F;
                    polys.Add(f);
                }
                return new PolyTopology(polys, Faces, Shapes);
            }
        }

        static D3 Centre(D3[] poly)
        {
            var c = new D3(0, 0, 0);
            foreach (var v in poly) c = c + v;
            return c * (1.0 / poly.Length);
        }

        static double Inradius(D3[] poly)
        {
            D3 c = Centre(poly);
            double best = double.MaxValue;
            for (int k = 0; k < poly.Length; k++) best = Math.Min(best, ((poly[k] + poly[(k + 1) % poly.Length]) * 0.5 - c).Length);
            return best;
        }

        // ------------------------------------------------------------------ base solids

        static readonly double Phi = (1 + Math.Sqrt(5)) / 2;

        static void Icosahedron(out D3[] v, out List<int[]> faces)
        {
            var list = new List<D3>();
            foreach (int a in new[] { -1, 1 })
                foreach (int b in new[] { -1, 1 })
                {
                    list.Add(new D3(0, a, b * Phi));
                    list.Add(new D3(a, b * Phi, 0));
                    list.Add(new D3(b * Phi, 0, a));
                }
            v = list.ToArray();
            faces = new List<int[]>();
            bool Edge(int i, int j) => Math.Abs((list[i] - list[j]).Length - 2) < 1e-6;
            for (int i = 0; i < 12; i++)
                for (int j = i + 1; j < 12; j++)
                    for (int k = j + 1; k < 12; k++)
                        if (Edge(i, j) && Edge(j, k) && Edge(i, k)) faces.Add(new[] { i, j, k });
        }

        /// <summary>Points sorted counter-clockwise round an axis through the origin (seen from the axis tip).</summary>
        static D3[] Around(List<D3> pts, D3 axis)
        {
            D3 a = axis * (1 / axis.Length);
            D3 r = Math.Abs(a.Y) < 0.9 ? new D3(0, 1, 0) : new D3(1, 0, 0);
            D3 u = r.Cross(a); u = u * (1 / u.Length);
            D3 w = a.Cross(u);
            var arr = pts.ToArray();
            var ang = new double[arr.Length];
            for (int i = 0; i < arr.Length; i++) ang[i] = Math.Atan2(arr[i].Dot(w), arr[i].Dot(u));
            Array.Sort(ang, arr);
            return arr;
        }

        /// <summary>The 12 pentagons of a dodecahedron (dual of the icosahedron), counter-clockwise from outside.</summary>
        static List<D3[]> DodecaFaces()
        {
            Icosahedron(out var v, out var faces);
            var centres = new D3[faces.Count];
            for (int f = 0; f < faces.Count; f++) centres[f] = (v[faces[f][0]] + v[faces[f][1]] + v[faces[f][2]]) * (1.0 / 3);
            var o = new List<D3[]>();
            for (int i = 0; i < v.Length; i++)
            {
                var pts = new List<D3>();
                for (int f = 0; f < faces.Count; f++) if (Array.IndexOf(faces[f], i) >= 0) pts.Add(centres[f]);
                o.Add(Around(pts, v[i]));
            }
            return o;
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>A regular tetrahedron with every face cut into n x n small triangles (like a Pyraminx).</summary>
        public static PolyTopology Tetrahedron(int n)
        {
            double edge = n * TriangleSide;
            double s = edge / (2.0 * Math.Sqrt(2.0));
            var A = new D3(s, s, s); var B = new D3(s, -s, -s); var C = new D3(-s, s, -s); var D = new D3(-s, -s, s);
            return Triangles(new[] { new[] { A, B, C }, new[] { A, D, B }, new[] { A, C, D }, new[] { B, D, C } }, n);
        }

        /// <summary>A regular octahedron with every face cut into n x n small triangles (like a Skewb Diamond / FTO).</summary>
        public static PolyTopology Octahedron(int n)
        {
            double d = n * TriangleSide / Math.Sqrt(2.0);
            var faces = new List<D3[]>();
            foreach (int x in new[] { 1, -1 })
                foreach (int y in new[] { 1, -1 })
                    foreach (int z in new[] { 1, -1 })
                        faces.Add(new[] { new D3(x * d, 0, 0), new D3(0, y * d, 0), new D3(0, 0, z * d) });
            return Triangles(faces.ToArray(), n);
        }

        static PolyTopology Triangles(D3[][] faces, int n)
        {
            var b = new Builder();
            for (int f = 0; f < faces.Length; f++)
            {
                D3 p0 = faces[f][0], p1 = faces[f][1], p2 = faces[f][2];
                if ((p1 - p0).Cross(p2 - p0).Dot(p0 + p1 + p2) < 0) { var t = p1; p1 = p2; p2 = t; }
                D3 e1 = (p1 - p0) * (1.0 / n), e2 = (p2 - p0) * (1.0 / n);
                D3 G(int i, int j) => p0 + e1 * i + e2 * j;
                for (int i = 0; i < n; i++)
                    for (int j = 0; i + j < n; j++)
                    {
                        b.Add(new[] { G(i, j), G(i + 1, j), G(i, j + 1) }, f, CellShape.Triangle);
                        if (i + j < n - 1) b.Add(new[] { G(i + 1, j), G(i + 1, j + 1), G(i, j + 1) }, f, CellShape.Triangle);
                    }
            }
            return b.Build();
        }

        /// <summary>Kilominx: every pentagon of a dodecahedron cut into 5 kites (corner, two side middles, face centre).</summary>
        public static PolyTopology Kilominx()
        {
            var b = new Builder();
            var faces = DodecaFaces();
            for (int f = 0; f < faces.Count; f++)
            {
                var p = faces[f];
                D3 c = Centre(p);
                for (int k = 0; k < 5; k++)
                {
                    D3 cur = p[k], next = p[(k + 1) % 5], prev = p[(k + 4) % 5];
                    b.Add(new[] { cur, D3.Lerp(cur, next, 0.5), c, D3.Lerp(cur, prev, 0.5) }, f, CellShape.Kite);
                }
            }
            b.ScaleTo(TileInradius);
            return b.Build();
        }

        /// <summary>Megaminx: every pentagon of a dodecahedron cut into a centre pentagon, 5 edge pieces and 5 corners.</summary>
        public static PolyTopology Megaminx()
        {
            var b = new Builder();
            var faces = DodecaFaces();
            for (int f = 0; f < faces.Count; f++)
            {
                var p = faces[f];
                D3 c = Centre(p);
                var inner = new D3[5];
                for (int k = 0; k < 5; k++) inner[k] = D3.Lerp(c, p[k], MegaCentre);
                b.Add((D3[])inner.Clone(), f, CellShape.Pentagon);
                for (int k = 0; k < 5; k++)
                {
                    D3 cur = p[k], next = p[(k + 1) % 5], prev = p[(k + 4) % 5];
                    D3 a = D3.Lerp(cur, next, 1.0 / 3), bb = D3.Lerp(cur, next, 2.0 / 3), bPrev = D3.Lerp(prev, cur, 2.0 / 3);
                    b.Add(new[] { a, bb, inner[(k + 1) % 5], inner[k] }, f, CellShape.MegaEdge);
                    b.Add(new[] { cur, a, inner[k], bPrev }, f, CellShape.MegaCorner);
                }
            }
            b.ScaleTo(TileInradius);
            return b.Build();
        }

        /// <summary>Soccer ball (truncated icosahedron): 12 pentagons and 20 hexagons.</summary>
        public static PolyTopology Soccer()
        {
            Icosahedron(out var v, out var faces);
            var b = new Builder();
            int id = 0;
            for (int i = 0; i < v.Length; i++)
            {
                var pts = new List<D3>();
                for (int j = 0; j < v.Length; j++)
                    if (i != j && Math.Abs((v[i] - v[j]).Length - 2) < 1e-6) pts.Add(D3.Lerp(v[i], v[j], 1.0 / 3));
                b.Add(Around(pts, v[i]), id++, CellShape.Pentagon);
            }
            foreach (var f in faces)
            {
                D3 A = v[f[0]], B = v[f[1]], C = v[f[2]];
                var pts = new List<D3>
                {
                    D3.Lerp(A, B, 1.0 / 3), D3.Lerp(A, B, 2.0 / 3), D3.Lerp(B, C, 1.0 / 3),
                    D3.Lerp(B, C, 2.0 / 3), D3.Lerp(C, A, 1.0 / 3), D3.Lerp(C, A, 2.0 / 3),
                };
                b.Add(Around(pts, A + B + C), id++, CellShape.Hexagon);
            }
            b.ScaleTo(TileInradius);
            return b.Build();
        }

        /// <summary>
        /// Goldberg ball GP(f,0): the dual of an icosahedron whose faces are cut into f x f triangles and pushed onto a
        /// sphere. Always 12 pentagons; the rest are hexagons (10 f² + 2 tiles).
        /// </summary>
        public static PolyTopology Goldberg(int f)
        {
            if (f < 2) return Soccer();
            Icosahedron(out var v, out var faces);
            double R = v[0].Length;
            var corners = new Dictionary<long, (D3 p, List<D3> around)>();
            var order = new List<long>();
            foreach (var t in faces)
            {
                D3 A = v[t[0]], B = v[t[1]], C = v[t[2]];
                D3 P(int i, int j) => (A + (B - A) * ((double)i / f) + (C - A) * ((double)j / f)).WithLength(R);
                void Tri(D3 a, D3 b, D3 c)
                {
                    D3 centre = ((a + b + c) * (1.0 / 3)).WithLength(R);
                    foreach (var q in new[] { a, b, c })
                    {
                        long key = q.Key;
                        if (!corners.TryGetValue(key, out var e)) { e = (q, new List<D3>()); corners[key] = e; order.Add(key); }
                        e.around.Add(centre);
                    }
                }
                for (int i = 0; i < f; i++)
                    for (int j = 0; i + j < f; j++)
                    {
                        Tri(P(i, j), P(i + 1, j), P(i, j + 1));
                        if (i + j < f - 1) Tri(P(i + 1, j), P(i + 1, j + 1), P(i, j + 1));
                    }
            }
            var b = new Builder();
            int id = 0;
            foreach (long key in order)
            {
                var e = corners[key];
                b.Add(Around(e.around, e.p), id++, e.around.Count == 5 ? CellShape.Pentagon : CellShape.Hexagon);
            }
            b.ScaleTo(TileInradius);
            return b.Build();
        }

        // ------------------------------------------------------------------ outlines for the face images

        /// <summary>
        /// The outline every tile of a kind is drawn from, in the tile's own frame: centre (corner average) at the origin,
        /// side 0 straight ahead (+X), corners counter-clockwise, farthest corner at distance 1. A tile's face image shows
        /// this outline; the renderer maps it corner to corner onto each tile, so slightly uneven tiles (Goldberg
        /// hexagons) still fit their image exactly.
        /// </summary>
        public static float[] Outline(CellShape shape)
        {
            double[] xy;
            switch (shape)
            {
                case CellShape.Square: xy = Regular(4); break;
                case CellShape.Triangle: xy = Regular(3); break;
                case CellShape.Pentagon: xy = Regular(5); break;
                case CellShape.Hexagon: xy = Regular(6); break;
                default:
                {
                    // pieces of a regular pentagon (circumradius 1, corner k at 90 + 72k degrees)
                    var P = new double[10];
                    for (int k = 0; k < 5; k++) { double a = (90 + 72 * k) * Math.PI / 180; P[2 * k] = Math.Cos(a); P[2 * k + 1] = Math.Sin(a); }
                    double Px(int k) => P[2 * (k % 5)];
                    double Py(int k) => P[2 * (k % 5) + 1];
                    double Lx(int a, int b, double t) => Px(a) + (Px(b) - Px(a)) * t;
                    double Ly(int a, int b, double t) => Py(a) + (Py(b) - Py(a)) * t;
                    double s = MegaCentre;
                    if (shape == CellShape.Kite) xy = new[] { Px(0), Py(0), Lx(0, 1, 0.5), Ly(0, 1, 0.5), 0, 0, Lx(0, 4, 0.5), Ly(0, 4, 0.5) };
                    else if (shape == CellShape.MegaCorner)
                        xy = new[] { Px(0), Py(0), Lx(0, 1, 1.0 / 3), Ly(0, 1, 1.0 / 3), Px(0) * s, Py(0) * s, Lx(4, 0, 2.0 / 3), Ly(4, 0, 2.0 / 3) };
                    else
                        xy = new[] { Lx(0, 1, 1.0 / 3), Ly(0, 1, 1.0 / 3), Lx(0, 1, 2.0 / 3), Ly(0, 1, 2.0 / 3), Px(1) * s, Py(1) * s, Px(0) * s, Py(0) * s };
                    break;
                }
            }
            return Normalise(xy);
        }

        static double[] Regular(int n)
        {
            var o = new double[2 * n];
            for (int k = 0; k < n; k++) { double a = (-180.0 / n + k * 360.0 / n) * Math.PI / 180; o[2 * k] = Math.Cos(a); o[2 * k + 1] = Math.Sin(a); }
            return o;
        }

        /// <summary>Centre on the corner average, turn side 0 to +X, scale the farthest corner to 1.</summary>
        static float[] Normalise(double[] xy)
        {
            int n = xy.Length / 2;
            double cx = 0, cy = 0;
            for (int k = 0; k < n; k++) { cx += xy[2 * k]; cy += xy[2 * k + 1]; }
            cx /= n; cy /= n;
            double mx = (xy[0] + xy[2]) * 0.5 - cx, my = (xy[1] + xy[3]) * 0.5 - cy;
            double a = -Math.Atan2(my, mx), cos = Math.Cos(a), sin = Math.Sin(a), r = 0;
            var o = new double[xy.Length];
            for (int k = 0; k < n; k++)
            {
                double x = xy[2 * k] - cx, y = xy[2 * k + 1] - cy;
                o[2 * k] = x * cos - y * sin; o[2 * k + 1] = x * sin + y * cos;
                r = Math.Max(r, Math.Sqrt(o[2 * k] * o[2 * k] + o[2 * k + 1] * o[2 * k + 1]));
            }
            var f = new float[xy.Length];
            for (int i = 0; i < f.Length; i++) f[i] = (float)(o[i] / r);
            return f;
        }
    }
}
