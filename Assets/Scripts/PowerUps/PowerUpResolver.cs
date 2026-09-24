using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.PowerUps
{
    // Works out a power-up's footprint and clears it.
    //
    // Pure C#: no MonoBehaviour, no UnityEngine. The board geometry lives here rather than
    // in GameplayController, which orchestrates the sequence but should not contain
    // row-scanning or 3x3 loops.
    public static class PowerUpResolver
    {
        public static PowerUpActivationResult Activate(Board board, BoardPosition position)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            Cell cell = board.GetCell(position);

            if (!cell.HasBlock || !cell.Block.IsPowerUp)
            {
                throw new InvalidOperationException($"There is no power-up at {position} to activate.");
            }

            Block powerUp = cell.Block;
            IReadOnlyList<BoardPosition> footprint = Footprint(board, position, powerUp);

            var removedBlocks = new List<Block>();
            var removedPositions = new List<BoardPosition>();
            var removedObstacles = new List<ObstacleRemoval>();

            // Each footprint cell is visited exactly once, so nothing is counted twice --
            // no deduplication pass is needed, unlike adjacency-based crate hits where one
            // crate can neighbour several removed blocks.
            foreach (BoardPosition target in footprint)
            {
                Cell targetCell = board.GetCell(target);

                if (targetCell.HasObstacle)
                {
                    // Crates die to a single hit, and a power-up hitting one directly counts
                    // exactly once -- one ObstacleRemoval, one point of crate-goal progress.
                    removedObstacles.Add(new ObstacleRemoval(target, board.RemoveObstacle(target)));
                    continue;
                }

                if (!targetCell.HasBlock)
                {
                    continue;
                }

                // NO CHAIN REACTIONS. A Rocket or Bomb caught in this footprint is removed
                // as a piece and never activated. This is a deliberate scope limit, not an
                // oversight: the loop simply removes what it finds and never recurses.
                //
                // Adding chaining later would mean collecting the power-ups found here into
                // a queue and draining it after this pass, with a visited set to stop a
                // Rocket pair activating each other forever.
                removedBlocks.Add(board.RemoveBlock(target));
                removedPositions.Add(target);
            }

            return new PowerUpActivationResult(removedBlocks, removedPositions, removedObstacles);
        }

        // The cells a power-up affects, including its own.
        //
        //   Rocket Horizontal : the whole row
        //   Rocket Vertical   : the whole column
        //   Bomb              : itself plus its four orthogonal neighbours (a plus shape),
        //                       clamped to the board -- never diagonals
        //
        // A Rocket does NOT stop at a crate: the line is computed geometrically, so the
        // blast continues across the full row or column regardless of what it passes through.
        public static IReadOnlyList<BoardPosition> Footprint(Board board, BoardPosition position, Block powerUp)
        {
            var cells = new List<BoardPosition>();

            switch (powerUp.Kind)
            {
                case BlockKind.Rocket when powerUp.Direction == RocketDirection.Horizontal:
                    for (int column = 0; column < board.Columns; column++)
                    {
                        cells.Add(new BoardPosition(position.Row, column));
                    }

                    break;

                case BlockKind.Rocket:
                    for (int row = 0; row < board.Rows; row++)
                    {
                        cells.Add(new BoardPosition(row, position.Column));
                    }

                    break;

                case BlockKind.Bomb:
                    // A PLUS, not a square: the bomb's own cell plus its four orthogonal
                    // neighbours. Diagonals are deliberately excluded, for the same reason
                    // they are excluded from colour matching and crate hits -- "touching"
                    // means sharing an edge everywhere in this game, and a bomb that broke
                    // that rule would be the one inconsistent thing on the board.
                    //
                    // Reuses Board.GetOrthogonalNeighbours rather than re-deriving the four
                    // offsets, so there is exactly one definition of "orthogonal" in the
                    // project. It already skips off-board coordinates, which is why a corner
                    // bomb affects three cells and an edge bomb four with no special case.
                    cells.Add(position);

                    foreach (Cell neighbour in board.GetOrthogonalNeighbours(position))
                    {
                        cells.Add(neighbour.Position);
                    }

                    break;

                default:
                    throw new InvalidOperationException($"{powerUp.Kind} is not an activatable power-up.");
            }

            return cells;
        }
    }
}
