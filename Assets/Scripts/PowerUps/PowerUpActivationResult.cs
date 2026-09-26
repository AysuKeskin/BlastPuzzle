using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.PowerUps
{
    // Everything one activation destroyed, and which power-ups fired.
    public sealed class PowerUpActivationResult
    {
        public PowerUpActivationResult(
            IReadOnlyList<Block> removedBlocks,
            IReadOnlyList<BoardPosition> removedBlockPositions,
            IReadOnlyList<ObstacleRemoval> removedObstacles,
            IReadOnlyList<Block> activatedPowerUps)
        {
            RemovedBlocks = removedBlocks;
            RemovedBlockPositions = removedBlockPositions;
            RemovedObstacles = removedObstacles;
            ActivatedPowerUps = activatedPowerUps;
        }

        // Includes every power-up that went off, since each is also cleared from the board.
        public IReadOnlyList<Block> RemovedBlocks { get; }

        public IReadOnlyList<BoardPosition> RemovedBlockPositions { get; }

        public IReadOnlyList<ObstacleRemoval> RemovedObstacles { get; }
        public IReadOnlyList<Block> ActivatedPowerUps { get; }

        public override string ToString() =>
            $"{RemovedBlocks.Count} blocks, {RemovedObstacles.Count} obstacles"
            + (ActivatedPowerUps.Count > 1 ? $", chain of {ActivatedPowerUps.Count}" : string.Empty);
    }
}
