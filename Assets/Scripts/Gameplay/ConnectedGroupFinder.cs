using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Finds the same-coloured blocks connected to a cell. Diagonals do not count.
    public static class ConnectedGroupFinder
    {
        private static readonly IReadOnlyList<BoardPosition> NoGroup = Array.Empty<BoardPosition>();
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
            if (!startCell.HasBlock || !startCell.Block.IsNormal)
            {
                return NoGroup;
            }

            BlockColor targetColor = startCell.Block.Color;

            var group = new List<BoardPosition>();
            var queue = new Queue<BoardPosition>();
            var visited = new HashSet<BoardPosition> { start };
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                BoardPosition current = queue.Dequeue();
                group.Add(current);

                foreach (Cell neighbour in board.GetOrthogonalNeighbours(current))
                {
                    if (!neighbour.HasBlock || !neighbour.Block.IsNormal ||
                        neighbour.Block.Color != targetColor)
                    {
                        continue;
                    }
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
