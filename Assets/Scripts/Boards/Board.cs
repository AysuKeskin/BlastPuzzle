using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Obstacles;

namespace BlastPuzzle.Boards
{
    // The grid. Row 0 is the bottom row, so gravity moves blocks toward lower rows.
    public sealed class Board
    {
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
        public void SetBlock(BoardPosition position, Block block)
        {
            Cell cell = GetCell(position);
            if (cell.HasObstacle)
            {
                throw new InvalidOperationException($"Cannot place a block at {position}: it holds {cell.Obstacle}.");
            }

            cell.SetBlock(block);
        }

        // Obstacles are placed once, at level start, and never move.
        public void PlaceObstacle(BoardPosition position, Obstacle obstacle)
        {
            if (obstacle == null)
            {
                throw new ArgumentNullException(nameof(obstacle));
            }

            Cell cell = GetCell(position);

            if (!cell.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Cannot place an obstacle at {position}: the cell already holds {(cell.HasBlock ? (object)cell.Block : cell.Obstacle)}.");
            }

            cell.SetObstacle(obstacle);
        }

        // Returns what was removed, or null if there was no obstacle there.
        public Obstacle RemoveObstacle(BoardPosition position)
        {
            Cell cell = GetCell(position);
            Obstacle removed = cell.Obstacle;
            cell.RemoveObstacle();
            return removed;
        }

        public Obstacle GetObstacle(BoardPosition position) => GetCell(position).Obstacle;
        public Block MoveBlock(BoardPosition from, BoardPosition to)
        {
            Cell source = GetCell(from);
            Cell destination = GetCell(to);

            if (!source.HasBlock)
            {
                throw new InvalidOperationException($"There is no block at {from} to move.");
            }
            if (from == to)
            {
                return source.Block;
            }
            if (!destination.IsEmpty)
            {
                throw new InvalidOperationException($"Cannot move to {to}: that cell is not empty.");
            }

            Block moved = source.Block;
            source.RemoveBlock();
            destination.SetBlock(moved);
            return moved;
        }
        public Block RemoveBlock(BoardPosition position)
        {
            Cell cell = GetCell(position);
            Block removed = cell.Block;
            cell.RemoveBlock();
            return removed;
        }
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
