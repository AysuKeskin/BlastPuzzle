using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Drops blocks into the gaps below them. Crates split a column into segments.
    public static class GravityResolver
    {
        public static IReadOnlyList<BlockMove> ApplyGravity(Board board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            var moves = new List<BlockMove>();
            for (int column = 0; column < board.Columns; column++)
            {
                CompactColumn(board, column, moves);
            }

            return moves;
        }
        // One downward diagonal step per block. Called after vertical gravity settles.
        // A side opening is required so a block cannot pass through a solid crate wall.
        public static IReadOnlyList<BlockMove> ApplyDiagonalGravity(Board board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            var moves = new List<BlockMove>();
            for (int row = 0; row < board.Rows - 1; row++)
                for (int column = 0; column < board.Columns; column++)
                {
                    var to = new BoardPosition(row, column);
                    if (!board.GetCell(to).IsEmpty || !HasObstacleAbove(board, to)) continue;
                    // Alternate preference across the board, with a deterministic tie break.
                    int preferred = ((row + column) & 1) == 0 ? -1 : 1;
                    for (int side = 0; side < 2; side++)
                    {
                        int sourceColumn = column + (side == 0 ? preferred : -preferred);
                        if (!board.IsInside(row + 1, sourceColumn) ||
                            board.GetCell(row, sourceColumn).HasObstacle) continue;
                        var from = new BoardPosition(row + 1, sourceColumn);
                        if (!board.GetCell(from).HasBlock) continue;
                        Block block = board.MoveBlock(from, to);
                        moves.Add(new BlockMove(block, from, to));
                        break;
                    }
                }
            return moves;
        }

        public static bool HasObstacleAbove(Board board, BoardPosition position)
        {
            for (int row = position.Row + 1; row < board.Rows; row++)
                if (board.GetCell(row, position.Column).HasObstacle) return true;
            return false;
        }

        private static void CompactColumn(Board board, int column, List<BlockMove> moves)
        {
            int writeRow = 0;

            for (int readRow = 0; readRow < board.Rows; readRow++)
            {
                Cell cell = board.GetCell(readRow, column);

                if (cell.HasObstacle)
                {
                    // A crate blocks the fall, so the next segment starts above it.
                    writeRow = readRow + 1;
                    continue;
                }

                if (!cell.HasBlock)
                {
                    continue;
                }

                if (readRow != writeRow)
                {
                    var from = new BoardPosition(readRow, column);
                    var to = new BoardPosition(writeRow, column);
                    Block moved = board.MoveBlock(from, to);
                    moves.Add(new BlockMove(moved, from, to));
                }

                writeRow++;
            }
        }
    }
}
