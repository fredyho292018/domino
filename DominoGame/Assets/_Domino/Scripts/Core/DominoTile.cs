using System;
using System.Collections.Generic;

namespace Domino.Core
{
    [Serializable]
    public readonly struct DominoTile : IEquatable<DominoTile>
    {
        public int SideA { get; }
        public int SideB { get; }
        public int Id => Math.Min(SideA, SideB) * 10 + Math.Max(SideA, SideB);
        public DominoTile(int sideA, int sideB)
        {
            if (sideA < 0 || sideA > 9 || sideB < 0 || sideB > 9)
                throw new ArgumentOutOfRangeException(nameof(sideA), "Double Nine values must be 0–9.");
            SideA = sideA;
            SideB = sideB;
        }
        public bool Equals(DominoTile other) => Id == other.Id;
        public override bool Equals(object obj) => obj is DominoTile other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => $"{SideA}|{SideB}";
        public static List<DominoTile> CreateSet()
        {
            var tiles = new List<DominoTile>(55);
            for (int a = 0; a <= 9; a++)
                for (int b = a; b <= 9; b++) tiles.Add(new DominoTile(a, b));
            return tiles;
        }
    }
}
