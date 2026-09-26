using System;
using System.Collections.Generic;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Destroys crates next to the cells a blast cleared.
    public static class ObstacleResolver
    {
        public static IReadOnlyList<ObstacleRemoval> ResolveAdjacentHits(
            Board board,
            IReadOnlyList<BoardPosition> removedBlockPositions)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (removedBlockPositions == null)
            {
                throw new ArgumentNullException(nameof(removedBlockPositions));
            }

            var alreadyHit = new HashSet<BoardPosition>();
            var removals = new List<ObstacleRemoval>();

            foreach (BoardPosition position in removedBlockPositions)
            {
                foreach (Cell neighbour in board.GetOrthogonalNeighbours(position))
                {
                    if (!neighbour.HasObstacle)
                    {
                        continue;
                    }
                    if (!alreadyHit.Add(neighbour.Position))
                    {
                        continue;
                    }

                    Obstacles.Obstacle destroyed = board.RemoveObstacle(neighbour.Position);
                    removals.Add(new ObstacleRemoval(neighbour.Position, destroyed));
                }
            }

            return removals;
        }
    }
}
