namespace BlastPuzzle.Blocks
{
    // A playing piece: a colour plus a kind (normal, rocket or bomb).
    public sealed class Block
    {
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
        public bool IsNormal => Kind == BlockKind.Normal;

        public bool IsPowerUp => Kind != BlockKind.Normal;

        public override string ToString() => Kind == BlockKind.Normal
            ? Color.ToString()
            : Kind == BlockKind.Rocket ? $"{Color} Rocket({Direction})" : $"{Color} Bomb";
    }
}
