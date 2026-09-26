namespace BlastPuzzle.Obstacles
{
    // A crate. It never moves and never refills.
    public sealed class Obstacle
    {
        public Obstacle(ObstacleType type)
        {
            Type = type;
        }

        public ObstacleType Type { get; }

        public override string ToString() => Type.ToString();
    }
}
