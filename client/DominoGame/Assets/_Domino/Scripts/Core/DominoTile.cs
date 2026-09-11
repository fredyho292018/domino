using System;
using System.Collections.Generic;

namespace Domino.Core
{
    [Serializable]
    public readonly struct DominoTile : IEquatable<DominoTile>
    {
        public const int SupportedMaxPip = 9;
        public int SideA { get; }
        public int SideB { get; }
        public int Id { get { int high = Math.Max(SideA, SideB); return high * (high + 1) / 2 + Math.Min(SideA, SideB); } }
        public DominoTile(int sideA, int sideB)
        {
            if (sideA < 0 || sideA > SupportedMaxPip || sideB < 0 || sideB > SupportedMaxPip)
                throw new ArgumentOutOfRangeException(nameof(sideA), "Double Nine values must be 0–9.");
            SideA = sideA;
            SideB = sideB;
        }
        public bool Equals(DominoTile other) => Id == other.Id;
        public override bool Equals(object obj) => obj is DominoTile other && Equals(other);
        public override int GetHashCode() => Id;
        public override string ToString() => $"{SideA}|{SideB}";
        public static int TotalTilesFor(int maxPip)
        {
            if (maxPip < 1 || maxPip > SupportedMaxPip) throw new ArgumentOutOfRangeException(nameof(maxPip));
            return checked((maxPip + 1) * (maxPip + 2) / 2);
        }
        public static List<DominoTile> CreateSet(int maxPip)
        {
            var tiles = new List<DominoTile>(TotalTilesFor(maxPip));
            for (int a = 0; a <= maxPip; a++)
                for (int b = a; b <= maxPip; b++) tiles.Add(new DominoTile(a, b));
            return tiles;
        }
    }
}
