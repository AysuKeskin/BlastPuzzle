namespace BlastPuzzle.Obstacles
{
    // Exactly one kind today. The enum exists rather than being implied so that Cell,
    // Board and LevelDefinition already speak in terms of "obstacle" instead of "crate" --
    // adding Ice later becomes a new value plus its behaviour, not a rename across a dozen
    // files. Note what is NOT here: no base class, no interface, no hierarchy.
    public enum ObstacleType
    {
        Crate
    }
}
