using System;
using System.Collections.Generic;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Destroys the obstacles touched by a blast.
    //
    // Pure C#: no MonoBehaviour, no UnityEngine. Like the other resolvers it mutates the
    // board through the Board's own API and returns a description of what happened.
    public static class ObstacleResolver
    {
        // MUST run before gravity. Adjacency is judged against where the blasted blocks
        // WERE: once gravity has settled the column, the cells next to a crate hold entirely
        // different blocks, and the crate would be spared or hit by the wrong ones.
        //
        // Crates currently die to a single hit, so being touched at all destroys them.
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

                    // A crate in the middle of a blasted group is adjacent to several removed
                    // blocks and would otherwise be processed once per neighbour -- counting
                    // three or four times toward a "destroy 5 crates" goal from a single
                    // crate. HashSet.Add tests and marks in one step, so each crate is
                    // handled exactly once per blast.
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
