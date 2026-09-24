using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Settles the board: every Block falls as far down its own column as it can.
    //
    // Pure C#: no MonoBehaviour, no UnityEngine, no Rigidbody. Falling here is an array
    // compaction, not a simulation -- the result is computed in one pass and is identical
    // every time, which is what makes the board's state reproducible and testable.
    //
    // Unlike ConnectedGroupFinder, this one DOES mutate the board: applying gravity is a
    // state transition, not a query. It returns a description of every block that moved so
    // the presentation layer can update the views it already has.
    public static class GravityResolver
    {
        public static IReadOnlyList<BlockMove> ApplyGravity(Board board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            var moves = new List<BlockMove>();

            // Columns are independent: gravity never moves a block sideways, so each column
            // can be compacted on its own without looking at its neighbours.
            for (int column = 0; column < board.Columns; column++)
            {
                CompactColumn(board, column, moves);
            }

            return moves;
        }

        // Two pointers walking the same column from the BOTTOM up (row 0 is the bottom, so
        // "down" means toward lower indices and the lowest free slot is found first).
        //
        //   readRow  - the cell currently being examined
        //   writeRow - the lowest row that has not been filled yet
        //
        // writeRow only advances when a block is placed, so it always trails or equals
        // readRow. That single fact gives three guarantees at once:
        //
        //   * the destination is always already empty, because everything between writeRow
        //     and readRow was empty by the time we got here;
        //   * a block is never written onto a cell that has not been read yet, so nothing
        //     can be overwritten before it is moved;
        //   * blocks are placed in the order they are found, so their vertical order within
        //     the column is preserved -- nothing can fall past anything else.
        private static void CompactColumn(Board board, int column, List<BlockMove> moves)
        {
            int writeRow = 0;

            for (int readRow = 0; readRow < board.Rows; readRow++)
            {
                if (board.GetCell(readRow, column).IsEmpty)
                {
                    continue;
                }

                if (readRow != writeRow)
                {
                    var from = new BoardPosition(readRow, column);
                    var to = new BoardPosition(writeRow, column);

                    // Board performs the mutation and hands back the same instance, which is
                    // what keeps the block's identity intact across the fall.
                    Block moved = board.MoveBlock(from, to);
                    moves.Add(new BlockMove(moved, from, to));
                }

                writeRow++;
            }
        }
    }
}
