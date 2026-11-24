using System;

namespace TactileWars.Army
{
    /// <summary>Axial hex coordinate (q along the row, r = row index) for the pointy-top deployment grid.</summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int q, r;

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        public bool Equals(HexCoord other) => q == other.q && r == other.r;
        public override bool Equals(object obj) => obj is HexCoord o && Equals(o);
        public override int GetHashCode() => (q * 73856093) ^ (r * 19349663);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
        public override string ToString() => $"Hex({q}, {r})";
    }
}
