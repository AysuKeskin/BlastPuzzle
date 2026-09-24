using System;

namespace BlastPuzzle.Boards
{
    // A grid coordinate. This is a struct (a value type) because a position
    // simply *is* its two numbers: (2, 3) and (2, 3) are the same position, and
    // copying one is harmless. Later milestones put thousands of these in sets
    // during group detection, so avoiding a heap allocation each time is free here.
    //
    // Row 0 is the BOTTOM row and rows increase upwards. See Board for why.
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

        // Implementing IEquatable<BoardPosition> gives HashSet and Dictionary a
        // typed comparison to call, instead of the object-based one that would
        // box every position into a heap allocation.
        public bool Equals(BoardPosition other) => Row == other.Row && Column == other.Column;

        public override bool Equals(object obj) => obj is BoardPosition other && Equals(other);

        public override int GetHashCode() => (Row * RowHashMultiplier) ^ Column;

        public static bool operator ==(BoardPosition left, BoardPosition right) => left.Equals(right);

        public static bool operator !=(BoardPosition left, BoardPosition right) => !left.Equals(right);

        public override string ToString() => $"(r{Row}, c{Column})";
    }
}
