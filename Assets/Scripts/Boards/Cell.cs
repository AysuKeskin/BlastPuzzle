using BlastPuzzle.Blocks;
using BlastPuzzle.Obstacles;

namespace BlastPuzzle.Boards
{
    // One square. It holds a block, an obstacle, or nothing.
    public sealed class Cell
    {
        public Cell(BoardPosition position)
        {
            Position = position;
        }

        public BoardPosition Position { get; }

        public Block Block { get; private set; }

        public Obstacle Obstacle { get; private set; }

        public bool HasBlock => Block != null;

        public bool HasObstacle => Obstacle != null;
        public bool IsEmpty => Block == null && Obstacle == null;
        internal void SetBlock(Block block) => Block = block;

        internal void RemoveBlock() => Block = null;

        internal void SetObstacle(Obstacle obstacle) => Obstacle = obstacle;

        internal void RemoveObstacle() => Obstacle = null;

        public override string ToString()
        {
            if (HasObstacle)
            {
                return $"{Position} {Obstacle}";
            }

            return IsEmpty ? $"{Position} empty" : $"{Position} {Block}";
        }
    }
}
