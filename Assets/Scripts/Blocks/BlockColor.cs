namespace BlastPuzzle.Blocks
{
    // No "None" or "Empty" member on purpose: an empty space is a Cell that
    // holds no Block, never a Block wearing a special colour. Keeping the two
    // ideas separate means "is this space empty?" has exactly one answer.
    public enum BlockColor
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple
    }
}
