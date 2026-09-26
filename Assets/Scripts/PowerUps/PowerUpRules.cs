using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.PowerUps
{
    // Decides whether a blasted group earns a power-up, and which one.
    public static class PowerUpRules
    {
        public const int RocketThreshold = 5;
        public const int BombThreshold = 7;
        public static Block TryCreatePowerUp(IReadOnlyList<BoardPosition> group, BlockColor color)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (group.Count >= BombThreshold)
            {
                // Clears the 3x3 square around itself, diagonals included.
                return Block.CreateBomb(color);
            }

            if (group.Count >= RocketThreshold)
            {
                return Block.CreateRocket(color, DirectionFor(group));
            }

            return null;
        }
        public static RocketDirection DirectionFor(IReadOnlyList<BoardPosition> group)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (group.Count == 0)
            {
                throw new ArgumentException("An empty group has no shape.", nameof(group));
            }

            int minRow = int.MaxValue, maxRow = int.MinValue;
            int minColumn = int.MaxValue, maxColumn = int.MinValue;

            foreach (BoardPosition position in group)
            {
                minRow = Math.Min(minRow, position.Row);
                maxRow = Math.Max(maxRow, position.Row);
                minColumn = Math.Min(minColumn, position.Column);
                maxColumn = Math.Max(maxColumn, position.Column);
            }

            int width = maxColumn - minColumn + 1;
            int height = maxRow - minRow + 1;

            return width >= height ? RocketDirection.Horizontal : RocketDirection.Vertical;
        }
    }
}
