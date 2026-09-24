using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Fills every empty cell with a newly created Block.
    //
    // Pure C#: no MonoBehaviour, no UnityEngine, and deliberately no UnityEngine.Random --
    // that is a static global tied to the engine, which would make this untestable outside
    // play mode and impossible to seed per caller. System.Random is an ordinary object the
    // caller owns and can seed.
    //
    // Like GravityResolver this one mutates the board, because refilling is a state
    // transition rather than a query.
    public static class RefillResolver
    {
        // Traversal is column by column, bottom to top, and this order is the documented
        // contract: with a fixed seed and the same board, the same colours land in the same
        // cells. After gravity the empty cells sit at the tops of columns, but nothing here
        // relies on that -- ANY empty cell is filled, wherever it is.
        public static IReadOnlyList<BlockSpawn> ApplyRefill(
            Board board,
            IReadOnlyList<BlockColor> availableColors,
            Random random)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (availableColors == null)
            {
                throw new ArgumentNullException(nameof(availableColors));
            }

            // A board that can spawn no colours is a broken configuration, not a board that
            // quietly stays empty. Fail where the mistake is, rather than three systems later.
            if (availableColors.Count == 0)
            {
                throw new ArgumentException(
                    "Refill needs at least one available colour.", nameof(availableColors));
            }

            var spawns = new List<BlockSpawn>();

            for (int column = 0; column < board.Columns; column++)
            {
                for (int row = 0; row < board.Rows; row++)
                {
                    var position = new BoardPosition(row, column);

                    // IsEmpty means "nothing here at all", so a crate cell is skipped
                    // without any obstacle-specific branch: a crate is not an empty block
                    // slot waiting to be filled, it is the cell's occupant.
                    if (!board.GetCell(position).IsEmpty)
                    {
                        continue;
                    }

                    // A brand new Block, never a recycled one: the old block was destroyed as
                    // a game object in its own right, and reusing the instance would hand the
                    // presentation layer a key it may still associate with a dead view.
                    BlockColor color = availableColors[random.Next(availableColors.Count)];
                    Block block = Block.CreateNormal(color);

                    board.SetBlock(position, block);
                    spawns.Add(new BlockSpawn(block, position));
                }
            }

            return spawns;
        }
    }
}
