using BlastPuzzle.Obstacles;

namespace BlastPuzzle.Boards
{
    // A record of one obstacle being destroyed: which one, and where it stood.
    //
    // The sibling of BlockMove and BlockSpawn. Unlike those it carries a POSITION as the
    // identifying detail rather than relying on object identity, because an obstacle never
    // moves -- its cell identifies it for its whole life, so the view layer can key on the
    // coordinate instead of the instance.
    //
    // Purely logical: no Vector3, no GameObject, no VFX.
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
