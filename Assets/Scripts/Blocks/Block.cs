namespace BlastPuzzle.Blocks
{
    // A reference type on purpose. A block keeps its identity while it falls, so later
    // milestones can say "this exact block moved from here to there" and the view layer can
    // animate the matching sprite.
    //
    // A Block deliberately does not store its own position. The Cell it sits in knows where
    // it is, so there is only one place that can be wrong.
    //
    // Power-ups are Blocks, not a parallel kind of piece: they occupy a cell, fall under
    // gravity and are refilled around exactly like ordinary blocks. Only what happens when
    // they are removed differs.
    public sealed class Block
    {
        // Private, with named factories instead. A public constructor taking
        // (colour, kind, direction) would let callers build nonsense -- a Bomb with a
        // meaningful RocketDirection, or a Normal block claiming to be Horizontal -- and
        // every call site would have to pass arguments that mean nothing for its case.
        // The factories make each valid combination one obvious call and the invalid ones
        // unspellable.
        private Block(BlockColor color, BlockKind kind, RocketDirection direction)
        {
            Color = color;
            Kind = kind;
            Direction = direction;
        }

        public static Block CreateNormal(BlockColor color) =>
            new Block(color, BlockKind.Normal, default);

        public static Block CreateRocket(BlockColor color, RocketDirection direction) =>
            new Block(color, BlockKind.Rocket, direction);

        public static Block CreateBomb(BlockColor color) =>
            new Block(color, BlockKind.Bomb, default);

        public BlockColor Color { get; }

        public BlockKind Kind { get; }

        // Only meaningful when Kind is Rocket.
        public RocketDirection Direction { get; }

        // Power-ups keep a colour purely so the view can tint them like the group that
        // produced them. That colour must never count toward a colour goal, which is why
        // every goal-facing check asks IsNormal rather than looking at Color.
        public bool IsNormal => Kind == BlockKind.Normal;

        public bool IsPowerUp => Kind != BlockKind.Normal;

        public override string ToString() => Kind == BlockKind.Normal
            ? Color.ToString()
            : Kind == BlockKind.Rocket ? $"{Color} Rocket({Direction})" : $"{Color} Bomb";
    }
}
