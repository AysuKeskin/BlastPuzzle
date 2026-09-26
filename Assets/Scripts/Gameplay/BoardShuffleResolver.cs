using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Rescues a board with no legal move by rearranging the blocks already on it.
    public static class BoardShuffleResolver
    {
        public const int DefaultMaximumAttempts = 100;
        public static ShuffleResult Shuffle(
            Board board,
            int minimumGroupSize,
            Random random,
            int maximumAttempts = DefaultMaximumAttempts)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (maximumAttempts <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumAttempts), maximumAttempts, "A shuffle needs at least one attempt.");
            }

            if (MoveAvailabilityChecker.HasAnyValidMove(board, minimumGroupSize))
            {
                return ShuffleResult.NotNeeded();
            }
            var positions = new List<BoardPosition>();
            var blocks = new List<Block>();

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);

                    if (cell.HasBlock)
                    {
                        positions.Add(cell.Position);
                        blocks.Add(cell.Block);
                    }
                }
            }

            if (blocks.Count < 2)
            {
                // Nothing to permute; one block can never form a group with itself.
                return ShuffleResult.Failed(0);
            }
            var original = new List<Block>(blocks);

            for (int attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                FisherYatesShuffle(blocks, random);
                Apply(board, positions, blocks);

                if (MoveAvailabilityChecker.HasAnyValidMove(board, minimumGroupSize))
                {
                    return ShuffleResult.Shuffled(BuildMoves(positions, original, blocks), attempt);
                }
            }
            Apply(board, positions, original);
            return ShuffleResult.Failed(maximumAttempts);
        }
        private static void FisherYatesShuffle(IList<Block> items, Random random)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
        private static void Apply(Board board, List<BoardPosition> positions, List<Block> arrangement)
        {
            // Every cell is cleared before any is filled: the destinations are occupied
            // by the very blocks being moved.
            foreach (BoardPosition position in positions)
            {
                board.RemoveBlock(position);
            }

            for (int i = 0; i < positions.Count; i++)
            {
                board.SetBlock(positions[i], arrangement[i]);
            }
        }
        private static IReadOnlyList<BlockMove> BuildMoves(
            List<BoardPosition> positions,
            List<Block> before,
            List<Block> after)
        {
            var origin = new Dictionary<Block, BoardPosition>(positions.Count);

            for (int i = 0; i < positions.Count; i++)
            {
                origin[before[i]] = positions[i];
            }

            var moves = new List<BlockMove>(positions.Count);

            for (int i = 0; i < positions.Count; i++)
            {
                BoardPosition from = origin[after[i]];
                BoardPosition to = positions[i];

                if (!from.Equals(to))
                {
                    moves.Add(new BlockMove(after[i], from, to));
                }
            }

            return moves;
        }
    }
}
