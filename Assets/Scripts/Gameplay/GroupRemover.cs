using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Removes a group, but only if it is big enough to be a legal move.
    public static class GroupRemover
    {
        private static readonly IReadOnlyList<Block> NothingRemoved = Array.Empty<Block>();
        public static IReadOnlyList<Block> TryRemoveGroup(
            Board board,
            IReadOnlyList<BoardPosition> positions,
            int minimumGroupSize)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (positions == null)
            {
                throw new ArgumentNullException(nameof(positions));
            }
            if (positions.Count < minimumGroupSize)
            {
                return NothingRemoved;
            }

            var removed = new List<Block>(positions.Count);

            foreach (BoardPosition position in positions)
            {
                Block block = board.RemoveBlock(position);

                if (block != null)
                {
                    removed.Add(block);
                }
            }

            return removed;
        }
    }
}
