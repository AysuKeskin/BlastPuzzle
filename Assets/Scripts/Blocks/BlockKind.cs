namespace BlastPuzzle.Blocks
{
    // What a Block does when it is part of a move.
    //
    // A discriminator on one concrete Block class rather than a hierarchy of
    // BoardPiece/ColouredPiece/SpecialPiece/RocketPiece. Power-ups are stored in cells,
    // fall under gravity and are rendered exactly like ordinary blocks, so they differ only
    // in what happens when they are removed -- which is a behaviour switch, not a shape.
    public enum BlockKind
    {
        Normal,
        Rocket,
        Bomb
    }
}
