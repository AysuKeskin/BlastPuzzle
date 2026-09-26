using BlastPuzzle.Obstacles;

namespace BlastPuzzle.Boards
{
    // An obstacle that was destroyed, and where it stood.
    public readonly struct ObstacleRemoval
    {
        public ObstacleRemoval(BoardPosition position, Obstacle obstacle)
        {
            Position = position;
            Obstacle = obstacle;
        }

        public BoardPosition Position { get; }

        public Obstacle Obstacle { get; }

        public override string ToString() => $"{Obstacle} destroyed at {Position}";
    }
}
