using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.PowerUps
{
    // Everything one power-up activation destroyed.
    //
    // Blocks and obstacles are reported separately because they feed different goals, and
    // the blocks list carries the instances themselves so colour goals can filter on
    // BlockKind rather than trusting a count.
    //
    // Purely logical: no Vector3, no GameObject, no VFX.
    public sealed class PowerUpActivationResult
    {
        public PowerUpActivationResult(
            IReadOnlyList<Block> removedBlocks,
            IReadOnlyList<BoardPosition> removedBlockPositions,
            IReadOnlyList<ObstacleRemoval> removedObstacles)
        {
            RemovedBlocks = removedBlocks;
            RemovedBlockPositions = removedBlockPositions;
            RemovedObstacles = removedObstacles;
        }

        // Includes the activated power-up itself, and any other power-ups caught in the
        // footprint -- which are removed as pieces but NOT activated.
        public IReadOnlyList<Block> RemovedBlocks { get; }

        public IReadOnlyList<BoardPosition> RemovedBlockPositions { get; }

        public IReadOnlyList<ObstacleRemoval> RemovedObstacles { get; }

        public override string ToString() =>
            $"{RemovedBlocks.Count} blocks, {RemovedObstacles.Count} obstacles";
    }
}
