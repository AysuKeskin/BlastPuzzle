using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Obstacles;

namespace BlastPuzzle.Goals
{
    // Owns the goals for one attempt at a level and advances them from what was destroyed.
    //
    // It consumes the actual removed Block instances rather than a count, because a group
    // size says nothing about colour -- and from Milestone 12 a single power-up will destroy
    // blocks of several colours at once. Counting the blocks themselves works for both.
    //
    // Two goal categories with two separate feeds: blocks advance colour goals, destroyed
    // crates advance the crate goal, and neither can advance the other. Gravity and refill
    // advance nothing, because there is no code path from them to this class.
    //
    // Pure C#: no UnityEngine.
    public sealed class GoalTracker
    {
        private readonly List<ColorGoal> goals;
        private readonly Dictionary<BlockColor, ColorGoal> goalsByColor = new Dictionary<BlockColor, ColorGoal>();

        // crateGoal is optional: a level may ask only for colours, only for crates, or both.
        public GoalTracker(IReadOnlyList<ColorGoal> levelGoals, CrateGoal levelCrateGoal = null)
        {
            if (levelGoals == null)
            {
                throw new ArgumentNullException(nameof(levelGoals));
            }

            // A level with nothing to achieve would be won by its first move, which is far
            // more likely a broken configuration than an intended one.
            if (levelGoals.Count == 0 && levelCrateGoal == null)
            {
                throw new ArgumentException("A level needs at least one goal.", nameof(levelGoals));
            }

            CrateGoal = levelCrateGoal;
            goals = new List<ColorGoal>(levelGoals);

            foreach (ColorGoal goal in goals)
            {
                // Two goals for the same colour would make "which one advances?" ambiguous.
                if (!goalsByColor.ContainsKey(goal.Color))
                {
                    goalsByColor.Add(goal.Color, goal);
                    continue;
                }

                throw new ArgumentException($"Duplicate goal for colour {goal.Color}.", nameof(levelGoals));
            }
        }

        public IReadOnlyList<ColorGoal> Goals => goals;

        // Null when the level asks for no crates.
        public CrateGoal CrateGoal { get; }

        public bool AreAllGoalsComplete
        {
            get
            {
                foreach (ColorGoal goal in goals)
                {
                    if (!goal.IsComplete)
                    {
                        return false;
                    }
                }

                return CrateGoal == null || CrateGoal.IsComplete;
            }
        }

        // Only blocks that were actually destroyed reach here. Blocks that merely fall during
        // gravity, and blocks created by refill, never do -- so the board refilling itself
        // with Blue can never advance a Blue goal.
        //
        // A removed colour with no matching goal is simply ignored: blasting a colour the
        // level does not ask for is a legitimate move, not an error.
        public void ProcessRemovedBlocks(IReadOnlyList<Block> removedBlocks)
        {
            if (removedBlocks == null)
            {
                throw new ArgumentNullException(nameof(removedBlocks));
            }

            foreach (Block block in removedBlocks)
            {
                // Only NORMAL blocks count. Power-ups carry a colour so the view can tint
                // them, but destroying a Blue Rocket is not destroying a Blue block -- a
                // Rocket clearing a row would otherwise inflate the goal by itself.
                if (!block.IsNormal)
                {
                    continue;
                }

                if (goalsByColor.TryGetValue(block.Color, out ColorGoal goal))
                {
                    goal.RecordRemoved(1);
                }
            }
        }

        // Destroyed crates, and only destroyed crates, advance the crate goal. A crate that
        // is merely sitting on the board, or a block removed beside one, does nothing here.
        public void ProcessRemovedObstacles(IReadOnlyList<ObstacleRemoval> removedObstacles)
        {
            if (removedObstacles == null)
            {
                throw new ArgumentNullException(nameof(removedObstacles));
            }

            if (CrateGoal == null || removedObstacles.Count == 0)
            {
                return;
            }

            int crates = 0;

            foreach (ObstacleRemoval removal in removedObstacles)
            {
                if (removal.Obstacle != null && removal.Obstacle.Type == ObstacleType.Crate)
                {
                    crates++;
                }
            }

            CrateGoal.RecordDestroyed(crates);
        }

        public override string ToString()
        {
            string colors = string.Join(", ", goals);

            if (CrateGoal == null)
            {
                return colors;
            }

            return goals.Count == 0 ? CrateGoal.ToString() : colors + ", " + CrateGoal;
        }
    }
}
