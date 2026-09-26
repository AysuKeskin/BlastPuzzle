using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Fills empty cells from the top with new random blocks.
    public static class RefillResolver
    {
        public static IReadOnlyList<BlockSpawn> ApplyRefill(
            Board board,
            IReadOnlyList<BlockColor> availableColors,
            Random random,
            bool topAccessibleOnly = false)
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
                    if (!board.GetCell(position).IsEmpty ||
                        (topAccessibleOnly && GravityResolver.HasObstacleAbove(board, position)))
                    {
                        continue;
                    }
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
