namespace BlastPuzzle.Obstacles
{
    // A thing occupying a cell that is not a coloured block.
    //
    // One concrete sealed class with a discriminator field, deliberately not an inheritance
    // tree: there is a single obstacle type, so a base class would be an abstraction with
    // one implementation.
    //
    // No hit points. A Crate dies to one adjacent blast, and modelling durability before a
    // multi-hit obstacle exists would be building for a requirement we do not have.
    //
    // Pure C#: no UnityEngine.
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
