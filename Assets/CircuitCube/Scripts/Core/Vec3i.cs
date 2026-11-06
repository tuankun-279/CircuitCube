using System;

namespace CircuitCube.Core
{
    /// <summary>Small integer vector used by the cube topology. Kept free of UnityEngine so the rules can be unit tested.</summary>
    public readonly struct Vec3i : IEquatable<Vec3i>
    {
        public readonly int X, Y, Z;

        public Vec3i(int x, int y, int z) { X = x; Y = y; Z = z; }

        public static Vec3i operator +(Vec3i a, Vec3i b) => new Vec3i(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3i operator -(Vec3i a, Vec3i b) => new Vec3i(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3i operator -(Vec3i a) => new Vec3i(-a.X, -a.Y, -a.Z);
        public static Vec3i operator *(Vec3i a, int s) => new Vec3i(a.X * s, a.Y * s, a.Z * s);
        public static bool operator ==(Vec3i a, Vec3i b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        public static bool operator !=(Vec3i a, Vec3i b) => !(a == b);

        public int Dot(Vec3i b) => X * b.X + Y * b.Y + Z * b.Z;
        public Vec3i Cross(Vec3i b) => new Vec3i(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);

        /// <summary>Unique key for coordinates in the range -512..511 on every axis.</summary>
        public int Key => ((X + 512) * 1024 + (Y + 512)) * 1024 + (Z + 512);

        public bool Equals(Vec3i other) => this == other;
        public override bool Equals(object obj) => obj is Vec3i v && this == v;
        public override int GetHashCode() => Key;
        public override string ToString() => "(" + X + "," + Y + "," + Z + ")";
    }
}
