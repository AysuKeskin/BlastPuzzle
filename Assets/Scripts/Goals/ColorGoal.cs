using System;
using BlastPuzzle.Blocks;

namespace BlastPuzzle.Goals
{
    // "Destroy N blocks of this colour", plus how far along the player is.
    //
    // This is RUNTIME STATE, built fresh for each attempt at a level. The configuration is
    // the pair handed to the constructor; the progress lives only here. That split is what
    // stops Milestone 10's ScriptableObject from accumulating a player's progress: the asset
    // will describe "Blue x10", and a new ColorGoal gets constructed from it each time a
    // level starts. An asset that stored CurrentCount would carry one player's progress into
    // the next attempt, and into every other player's game.
    //
    // Pure C#: no UnityEngine.
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

        // Capped at the target so a big final blast reads "10 / 10", not "13 / 10". Overage
        // is not information the player needs, and letting it accumulate would make Remaining
        // go negative and any progress bar overshoot.
        //
        // GoalTracker is the intended caller; it is public only so this stays directly
        // testable without opening a hole in the assembly boundary.
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
