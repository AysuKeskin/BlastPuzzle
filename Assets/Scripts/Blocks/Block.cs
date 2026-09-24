namespace BlastPuzzle.Blocks
{
    // A reference type on purpose. A block keeps its identity while it falls,
    // so later milestones can say "this exact block moved from here to there"
    // and the view layer can animate the matching sprite.
    //
    // A Block deliberately does not store its own position. The Cell it sits in
    // knows where it is, so there is only one place that can be wrong.
    public sealed class Block
    {
        public Block(BlockColor color)
        {
            Color = color;
        }

        public BlockColor Color { get; }

        public override string ToString() => Color.ToString();
    }
}
