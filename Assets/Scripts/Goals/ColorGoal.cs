using System;
using BlastPuzzle.Blocks;

namespace BlastPuzzle.Goals
{
    // "Destroy N blocks of this colour", and how far along the player is.
    public sealed class ColorGoal
    {
        public ColorGoal(BlockColor color, int targetCount)
        {
            if (targetCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetCount), targetCount, "A goal needs a target of at least one block.");
            }

            Color = color;
            TargetCount = targetCount;
        }

        public BlockColor Color { get; }

        public int TargetCount { get; }

        public int CurrentCount { get; private set; }

        // Never negative, because progress is capped at the target.
        public int Remaining => TargetCount - CurrentCount;

        public bool IsComplete => CurrentCount >= TargetCount;
        public void RecordRemoved(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot remove a negative number of blocks.");
            }

            CurrentCount = Math.Min(TargetCount, CurrentCount + count);
        }

        public override string ToString() => $"{Color} {CurrentCount}/{TargetCount}";
    }
}
