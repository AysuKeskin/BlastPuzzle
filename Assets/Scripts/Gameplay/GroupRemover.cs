using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Applies a found group to the board by clearing those cells.
    //
    // Pure C#: no MonoBehaviour, no UnityEngine. It mutates the Board through the Board's
    // own API and returns the Block objects it took out, so the presentation layer can
    // find the matching views by identity.
    //
    // It ENFORCES the minimum-size rule but does not OWN it: the threshold is a parameter,
    // supplied by the orchestration layer. That keeps the policy ("a blast needs 2") in one
    // place while the mechanism ("clear these cells") stays here and stays testable.
    public static class GroupRemover
    {
        private static readonly IReadOnlyList<Block> NothingRemoved = Array.Empty<Block>();

        // Returns the Blocks actually removed, or an empty list when the group is too small
        // to be a legal blast. An empty result means the board was NOT touched -- callers can
        // treat "nothing removed" and "no move happened" as the same thing.
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

            // Checked before any mutation, so an invalid group leaves the board exactly as
            // it was rather than half-cleared.
            if (positions.Count < minimumGroupSize)
            {
                return NothingRemoved;
            }

            var removed = new List<Block>(positions.Count);

            foreach (BoardPosition position in positions)
            {
                // RemoveBlock returns what it took out, or null if the cell was already
                // empty. Positions come from a snapshot taken before this loop, so they stay
                // valid throughout it -- this is exactly why the finder returns positions
                // (values) rather than Cell references that would change underfoot.
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
