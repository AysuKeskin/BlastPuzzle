using System;

namespace BlastPuzzle.Goals
{
    // "Destroy N crates", plus how far along the player is.
    //
    // Same SHAPE as ColorGoal but a different SOURCE: this advances from destroyed
    // obstacles, that one from destroyed blocks. They are deliberately separate classes
    // rather than siblings under a base: the shared part is about ten lines of capped
    // counting, and a hierarchy to save that would be inheritance introduced for its own
    // sake rather than to solve a problem.
    //
    // Like ColorGoal this is RUNTIME STATE, constructed fresh from LevelDefinition for each
    // attempt, so the asset never accumulates a player's progress.
    //
    // Pure C#: no UnityEngine.
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

        // Capped at the target, so a blast destroying four crates when two were needed reads
        // "2 / 2" rather than "4 / 2" and Remaining never goes negative.
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
