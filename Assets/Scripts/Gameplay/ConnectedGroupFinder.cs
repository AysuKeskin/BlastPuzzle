using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Finds every Block orthogonally connected to a starting cell and sharing its colour.
    //
    // Pure C#: no MonoBehaviour, no UnityEngine. It reads a Board and returns coordinates.
    // It never writes to the board, and it has no opinion on whether the group it found
    // is a legal move -- that is the caller's rule to apply.
    public static class ConnectedGroupFinder
    {
        private static readonly IReadOnlyList<BoardPosition> NoGroup = Array.Empty<BoardPosition>();

        // Returns POSITIONS rather than Cells on purpose.
        //
        // A BoardPosition is a value: once returned it is a frozen snapshot of "where".
        // A Cell is a live reference whose contents change the moment removal starts, so a
        // list of Cells collected before a removal would quietly describe the board's new
        // state halfway through the loop that is mutating it. Positions cannot go stale.
        // Removal also takes a position (Board.RemoveBlock), so this is what the caller needs.
        //
        // CONTRACT: an out-of-bounds or empty start returns an EMPTY list, not an exception.
        // This is a question ("what is connected here?"), and "nothing" is a valid answer to
        // it. That differs deliberately from Board.GetCell, which throws: GetCell is a demand
        // ("give me the cell at X") where an off-grid argument can only be a programming bug.
        public static IReadOnlyList<BoardPosition> FindConnectedGroup(Board board, BoardPosition start)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (!board.IsInside(start))
            {
                return NoGroup;
            }

            Cell startCell = board.GetCell(start);

            if (startCell.IsEmpty)
            {
                return NoGroup;
            }

            BlockColor targetColor = startCell.Block.Color;

            var group = new List<BoardPosition>();
            var queue = new Queue<BoardPosition>();

            // visited is seeded with the start BEFORE the loop, for the same reason
            // neighbours are marked at enqueue time: see the note below.
            var visited = new HashSet<BoardPosition> { start };
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                BoardPosition current = queue.Dequeue();
                group.Add(current);

                foreach (Cell neighbour in board.GetOrthogonalNeighbours(current))
                {
                    if (neighbour.IsEmpty || neighbour.Block.Color != targetColor)
                    {
                        continue;
                    }

                    // HashSet.Add returns false if the item was already present, so this
                    // tests and marks in one step.
                    //
                    // Marking here -- at ENQUEUE time, not when the item is later dequeued --
                    // is what keeps the queue from filling with duplicates. Any cell in the
                    // middle of a group is reached from up to four neighbours; if it were
                    // only marked on dequeue, each of those four would enqueue it again and
                    // the work would multiply with every layer of the search.
                    if (!visited.Add(neighbour.Position))
                    {
                        continue;
                    }

                    queue.Enqueue(neighbour.Position);
                }
            }

            return group;
        }
    }
}
