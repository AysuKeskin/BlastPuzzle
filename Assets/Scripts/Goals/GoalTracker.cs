using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Obstacles;

namespace BlastPuzzle.Goals
{
    // The goals for one attempt, advanced by what a move destroyed.
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
        public void ProcessRemovedBlocks(IReadOnlyList<Block> removedBlocks)
        {
            if (removedBlocks == null)
            {
                throw new ArgumentNullException(nameof(removedBlocks));
            }

            foreach (Block block in removedBlocks)
            {
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
