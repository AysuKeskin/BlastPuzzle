using BlastPuzzle.Blocks;

namespace BlastPuzzle.Boards
{
    // A record of one newly created Block being placed into a cell.
    //
    // The sibling of BlockMove: that one describes a block changing cells, this one
    // describes a block coming into existence. Both carry the Block REFERENCE, because the
    // presentation layer keys its views on identity -- so a spawn tells BoardView both
    // which object to represent and where it belongs.
    //
    // Purely logical. No Vector3, no spawn height, no duration: WHAT was created and WHERE
    // it belongs on the grid. HOW it visually enters the scene is the view's business, which
    // is what lets Milestone 14 animate blocks in from above without touching RefillResolver.
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
