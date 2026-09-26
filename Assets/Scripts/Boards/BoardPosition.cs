using System;

namespace BlastPuzzle.Boards
{
    // A row/column coordinate.
    public readonly struct BoardPosition : IEquatable<BoardPosition>
    {
        // Spreads the row across the hash so that (1, 2) and (2, 1) do not collide.
        private const int RowHashMultiplier = 397;

        public BoardPosition(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public int Row { get; }

        public int Column { get; }
        public bool Equals(BoardPosition other) => Row == other.Row && Column == other.Column;

        public override bool Equals(object obj) => obj is BoardPosition other && Equals(other);

        public override int GetHashCode() => (Row * RowHashMultiplier) ^ Column;

        public static bool operator ==(BoardPosition left, BoardPosition right) => left.Equals(right);

        public static bool operator !=(BoardPosition left, BoardPosition right) => !left.Equals(right);

        public override string ToString() => $"(r{Row}, c{Column})";
    }
}
