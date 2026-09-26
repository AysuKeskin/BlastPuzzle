using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class ColorGoalTests
    {

        [Test]
        public void ColorGoal_ProgressDoesNotExceedTarget()
        {
            var goal = new ColorGoal(BlockColor.Blue, 10);

            goal.RecordRemoved(8);
            Assert.That(goal.CurrentCount, Is.EqualTo(8));

            // 8 + 5 would be 13; a goal reads 10/10, never 13/10.
            goal.RecordRemoved(5);
            Assert.That(goal.CurrentCount, Is.EqualTo(10));
            Assert.That(goal.Remaining, Is.Zero, "Remaining must never go negative.");
            Assert.That(goal.IsComplete, Is.True);
        }

        [Test]
        public void ColorGoal_RejectsNonsenseConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorGoal(BlockColor.Blue, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ColorGoal(BlockColor.Blue, -3));
        }
    }

    public sealed class GoalTrackerTests
    {
        private static Block Of(BlockColor color) => Block.CreateNormal(color);

        [Test]
        public void ColorGoal_TracksMatchingRemovedBlocks()
        {
            var blue = new ColorGoal(BlockColor.Blue, 10);
            var tracker = new GoalTracker(new[] { blue });

            tracker.ProcessRemovedBlocks(new[] { Of(BlockColor.Blue), Of(BlockColor.Blue), Of(BlockColor.Blue) });

            Assert.That(blue.CurrentCount, Is.EqualTo(3));
            Assert.That(blue.Remaining, Is.EqualTo(7));
        }

        [Test]
        public void ColorGoal_IgnoresOtherColors()
        {
            var blue = new ColorGoal(BlockColor.Blue, 5);
            var tracker = new GoalTracker(new[] { blue });

            // Blasting a colour the level never asked for is a legitimate move.
            tracker.ProcessRemovedBlocks(new[] { Of(BlockColor.Red), Of(BlockColor.Green), Of(BlockColor.Yellow) });

            Assert.That(blue.CurrentCount, Is.Zero);
        }

        [Test]
        public void GoalTracker_HandlesMultipleGoals()
        {
            var blue = new ColorGoal(BlockColor.Blue, 4);
            var red = new ColorGoal(BlockColor.Red, 3);
            var tracker = new GoalTracker(new[] { blue, red });
            tracker.ProcessRemovedBlocks(new[]
            {
                Of(BlockColor.Blue), Of(BlockColor.Red), Of(BlockColor.Blue), Of(BlockColor.Green)
            });

            Assert.That(blue.CurrentCount, Is.EqualTo(2));
            Assert.That(red.CurrentCount, Is.EqualTo(1));
        }

        [Test]
        public void GoalTracker_AllGoalsCompleteOnlyWhenEveryGoalComplete()
        {
            var blue = new ColorGoal(BlockColor.Blue, 2);
            var red = new ColorGoal(BlockColor.Red, 2);
            var tracker = new GoalTracker(new[] { blue, red });

            Assert.That(tracker.AreAllGoalsComplete, Is.False);

            tracker.ProcessRemovedBlocks(new[] { Of(BlockColor.Blue), Of(BlockColor.Blue) });
            Assert.That(blue.IsComplete, Is.True);
            Assert.That(tracker.AreAllGoalsComplete, Is.False, "One finished goal is not the level.");

            tracker.ProcessRemovedBlocks(new[] { Of(BlockColor.Red), Of(BlockColor.Red) });
            Assert.That(tracker.AreAllGoalsComplete, Is.True);
        }

        [Test]
        public void GoalTracker_RejectsNonsenseConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => new GoalTracker(null));
            Assert.Throws<ArgumentException>(() => new GoalTracker(Array.Empty<ColorGoal>()));
            Assert.Throws<ArgumentException>(() => new GoalTracker(new[]
            {
                new ColorGoal(BlockColor.Blue, 3),
                new ColorGoal(BlockColor.Blue, 4)
            }), "Two goals for one colour is ambiguous.");
        }
    }

    public sealed class LevelOutcomeTests
    {
        [Test]
        public void FinalMoveCompletingGoal_ResultsInWin()
        {
            // The case the ordering exists for: goals finished AND moves exhausted at once.
            Assert.That(LevelOutcome.Evaluate(true, 0), Is.EqualTo(GameplayState.Won));
        }

        [Test]
        public void FinalMoveWithoutGoalCompletion_ResultsInLoss()
        {
            Assert.That(LevelOutcome.Evaluate(false, 0), Is.EqualTo(GameplayState.Lost));
        }

        [Test]
        public void WinningBeforeMovesReachZero_ResultsInWin()
        {
            Assert.That(LevelOutcome.Evaluate(true, 7), Is.EqualTo(GameplayState.Won));
        }

        [Test]
        public void UnfinishedGoalsWithMovesLeft_ContinuesPlay()
        {
            Assert.That(LevelOutcome.Evaluate(false, 7), Is.EqualTo(GameplayState.WaitingForInput));
        }
    }
}
