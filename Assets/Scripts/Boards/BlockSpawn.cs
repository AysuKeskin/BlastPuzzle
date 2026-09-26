using BlastPuzzle.Blocks;

namespace BlastPuzzle.Boards
{
    // A block refill created, and the cell it belongs in.
    public readonly struct BlockSpawn
    {
        public BlockSpawn(Block block, BoardPosition position)
        {
            Block = block;
            Position = position;
        }

        public Block Block { get; }

        public BoardPosition Position { get; }

        public override string ToString() => $"{Block} spawned at {Position}";
    }
}
