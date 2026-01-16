using System;
using UnityEngine;

namespace HexCastle.Map
{
    [Serializable]
    public struct HexAxialCoord : IEquatable<HexAxialCoord>
    {
        public int q;
        public int r;

        public HexAxialCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        public static HexAxialCoord operator +(HexAxialCoord a, HexAxialCoord b) => new HexAxialCoord(a.q + b.q, a.r + b.r);

        public bool Equals(HexAxialCoord other) => q == other.q && r == other.r;
        public override bool Equals(object obj) => obj is HexAxialCoord other && Equals(other);
        public override int GetHashCode() => (q * 397) ^ r;

        public static int Distance(HexAxialCoord a, HexAxialCoord b)
        {
            // axial -> cube: x=q, z=r, y=-x-z
            int ax = a.q;
            int az = a.r;
            int ay = -ax - az;

            int bx = b.q;
            int bz = b.r;
            int by = -bx - bz;

            return (Mathf.Abs(ax - bx) + Mathf.Abs(ay - by) + Mathf.Abs(az - bz)) / 2;
        }
    }
}
