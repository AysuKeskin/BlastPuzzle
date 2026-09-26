using System.Collections.Generic;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // Asks whether the player can do anything. Never changes the board.
    public static class MoveAvailabilityChecker
    {
        public static bool HasAnyValidMove(Board board, int minimumGroupSize)
        {
            if (board == null)
            {
                throw new System.ArgumentNullException(nameof(board));
            }
            var visited = new HashSet<BoardPosition>();

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);

                    if (!cell.HasBlock)
                    {
                        continue;
                    }
                    if (cell.Block.IsPowerUp)
                    {
                        return true;
                    }

                    if (visited.Contains(cell.Position))
                    {
                        continue;
                    }
                    IReadOnlyList<BoardPosition> group =
                        ConnectedGroupFinder.FindConnectedGroup(board, cell.Position);

                    if (group.Count >= minimumGroupSize)
                    {
                        return true;
                    }

                    foreach (BoardPosition position in group)
                    {
                        visited.Add(position);
                    }
                }
            }

            return false;
        }
    }
}
