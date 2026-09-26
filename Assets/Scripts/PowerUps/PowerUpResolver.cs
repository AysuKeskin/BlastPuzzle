using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.PowerUps
{
    // Fires a power-up and clears what it reaches. Power-ups caught in the blast fire too.
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

            var removedBlocks = new List<Block>();
            var removedPositions = new List<BoardPosition>();
            var removedObstacles = new List<ObstacleRemoval>();
            var activated = new List<Block>();
            var pending = new Queue<Blast>();
            var fired = new HashSet<BoardPosition>();

            pending.Enqueue(new Blast(position, cell.Block));
            fired.Add(position);

            while (pending.Count > 0)
            {
                Blast blast = pending.Dequeue();
                activated.Add(blast.PowerUp);

                foreach (BoardPosition target in Footprint(board, blast.Position, blast.PowerUp))
                {
                    Cell targetCell = board.GetCell(target);

                    if (targetCell.HasObstacle)
                    {
                        removedObstacles.Add(new ObstacleRemoval(target, board.RemoveObstacle(target)));
                        continue;
                    }

                    if (!targetCell.HasBlock)
                    {
                        continue;
                    }

                    Block hit = targetCell.Block;
                    removedBlocks.Add(board.RemoveBlock(target));
                    removedPositions.Add(target);
                    // fired.Add is what ends the chain: two rockets that reach each
                    // other would otherwise trigger one another forever.
                    if (hit.IsPowerUp && fired.Add(target))
                    {
                        pending.Enqueue(new Blast(target, hit));
                    }
                }
            }

            return new PowerUpActivationResult(removedBlocks, removedPositions, removedObstacles, activated);
        }
        private readonly struct Blast
        {
            public Blast(BoardPosition position, Block powerUp)
            {
                Position = position;
                PowerUp = powerUp;
            }

            public BoardPosition Position { get; }

            public Block PowerUp { get; }
        }
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
                    // The 3x3 square around the bomb, diagonals included, clipped to the board.
                    for (int row = position.Row - 1; row <= position.Row + 1; row++)
                    {
                        for (int column = position.Column - 1; column <= position.Column + 1; column++)
                        {
                            if (board.IsInside(row, column))
                            {
                                cells.Add(new BoardPosition(row, column));
                            }
                        }
                    }

                    break;

                default:
                    throw new InvalidOperationException($"{powerUp.Kind} is not an activatable power-up.");
            }

            return cells;
        }
    }
}
