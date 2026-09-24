using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;

namespace BlastPuzzle.Boards
{
    // The logical grid, and the single source of truth for board state.
    //
    // Deliberately plain C#: no MonoBehaviour, no GameObject, no Transform, no
    // UnityEngine reference at all. A Board can be built in memory, inspected and
    // (from Milestone 4) unit-tested without opening a scene or entering play mode.
    //
    // ORIENTATION: row 0 is the BOTTOM row and rows increase upwards. This is
    // chosen so it matches Unity's y-up world space: turning a BoardPosition into
    // a world position stays a plain multiply with no sign flip, and gravity means
    // "blocks move toward lower row numbers". Every later system depends on this,
    // so it is stated here rather than rediscovered per file.
    public sealed class Board
    {
        // The four orthogonal directions, in a fixed order so that neighbour
        // enumeration is deterministic. Diagonals are intentionally absent:
        // blast groups connect up, down, left and right only.
        private static readonly (int RowOffset, int ColumnOffset)[] OrthogonalOffsets =
        {
            (1, 0),   // up    (rows increase upwards)
            (-1, 0),  // down
            (0, 1),   // right
            (0, -1)   // left
        };

        private readonly Cell[,] cells;

        public Board(int rows, int columns)
        {
            if (rows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), rows, "A board needs at least one row.");
            }

            if (columns <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), columns, "A board needs at least one column.");
            }

            Rows = rows;
            Columns = columns;
            cells = new Cell[rows, columns];

            // Every coordinate gets a Cell up front, so GetCell never returns null.
            // The cells start empty; filling them with blocks is a later milestone.
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    cells[row, column] = new Cell(new BoardPosition(row, column));
                }
            }
        }

        public int Rows { get; }

        public int Columns { get; }

        public bool IsInside(int row, int column) =>
            row >= 0 && row < Rows && column >= 0 && column < Columns;

        public bool IsInside(BoardPosition position) => IsInside(position.Row, position.Column);

        // Throws rather than returning null for an off-grid coordinate. Asking for
        // a cell that cannot exist is a programming mistake, and failing loudly at
        // the point of the mistake beats a NullReferenceException three systems later.
        // Callers that legitimately might be off-grid should ask IsInside first.
        public Cell GetCell(int row, int column)
        {
            if (!IsInside(row, column))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(row),
                    $"(r{row}, c{column}) is outside a {Rows}x{Columns} board.");
            }

            return cells[row, column];
        }

        public Cell GetCell(BoardPosition position) => GetCell(position.Row, position.Column);

        // Mutation goes through the Board so that the coordinate is validated in one
        // place, and so a future milestone has a single choke point to hook if the
        // board ever needs to raise change notifications.
        public void SetBlock(BoardPosition position, Block block) => GetCell(position).SetBlock(block);

        // Returns what was removed (null if the cell was already empty) so callers
        // do not have to read the cell first and then clear it.
        public Block RemoveBlock(BoardPosition position)
        {
            Cell cell = GetCell(position);
            Block removed = cell.Block;
            cell.RemoveBlock();
            return removed;
        }

        // Up, down, left and right only -- never diagonals. Coordinates that fall
        // off the grid are skipped, so a corner cell simply yields two neighbours
        // and an edge cell three. Callers never have to special-case the border.
        public IEnumerable<Cell> GetOrthogonalNeighbours(BoardPosition position)
        {
            foreach ((int rowOffset, int columnOffset) in OrthogonalOffsets)
            {
                int neighbourRow = position.Row + rowOffset;
                int neighbourColumn = position.Column + columnOffset;

                if (IsInside(neighbourRow, neighbourColumn))
                {
                    yield return cells[neighbourRow, neighbourColumn];
                }
            }
        }

        public IEnumerable<Cell> GetOrthogonalNeighbours(Cell cell) => GetOrthogonalNeighbours(cell.Position);
    }
}
