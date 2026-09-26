using System;

namespace BlastPuzzle.Goals
{
    // "Destroy N crates", and how far along the player is.
    public sealed class CrateGoal
    {
        public CrateGoal(int targetCount)
        {
            if (targetCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetCount), targetCount, "A crate goal needs a target of at least one.");
            }

            TargetCount = targetCount;
        }

        public int TargetCount { get; }

        public int CurrentCount { get; private set; }

        public int Remaining => TargetCount - CurrentCount;

        public bool IsComplete => CurrentCount >= TargetCount;
        public void RecordDestroyed(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot destroy a negative number of crates.");
            }

            CurrentCount = Math.Min(TargetCount, CurrentCount + count);
        }

        public override string ToString() => $"Crates {CurrentCount}/{TargetCount}";
    }
}
