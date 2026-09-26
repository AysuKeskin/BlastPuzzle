using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Goals;
using UnityEngine;

namespace BlastPuzzle.Levels
{
    [CreateAssetMenu(fileName = "Level", menuName = "BlastPuzzle/Level Definition", order = 0)]
    // One level's configuration, stored as an asset. No player progress lives here.
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField]
        private int levelNumber = 1;

        [SerializeField]
        private int rows = 8;

        [SerializeField]
        private int columns = 8;

        [SerializeField]
        private int moveLimit = 20;

        [Tooltip("The colours refill may generate for this level.")]
        [SerializeField]
        private BlockColor[] availableColors =
        {
            BlockColor.Red, BlockColor.Blue, BlockColor.Green, BlockColor.Yellow, BlockColor.Purple
        };

        [Tooltip("What the player must destroy to win. One entry per colour.")]
        [SerializeField]
        private ColorGoalDefinition[] goals =
        {
            new ColorGoalDefinition(BlockColor.Blue, 10)
        };

        [Tooltip("Crates placed on the board at level start. They never move and never refill.")]
        [SerializeField]
        private ObstaclePlacement[] obstacles = Array.Empty<ObstaclePlacement>();

        [Tooltip("How many crates must be destroyed to satisfy the crate goal. Zero means no crate goal.")]
        [SerializeField]
        private int crateGoalTarget;

        public int LevelNumber => levelNumber;

        public int Rows => rows;

        public int Columns => columns;

        public int MoveLimit => moveLimit;

        public IReadOnlyList<BlockColor> AvailableColors => availableColors;

        public IReadOnlyList<ColorGoalDefinition> Goals => goals;

        public IReadOnlyList<ObstaclePlacement> Obstacles => obstacles;

        // Zero means this level asks for no crates.
        public int CrateGoalTarget => crateGoalTarget;
        public GoalTracker CreateRuntimeGoals()
        {
            Validate();

            var runtimeGoals = new List<ColorGoal>(goals.Length);

            foreach (ColorGoalDefinition definition in goals)
            {
                runtimeGoals.Add(new ColorGoal(definition.Color, definition.TargetCount));
            }

            CrateGoal runtimeCrateGoal = crateGoalTarget > 0 ? new CrateGoal(crateGoalTarget) : null;

            return new GoalTracker(runtimeGoals, runtimeCrateGoal);
        }
        public void Validate()
        {
            if (TryFindConfigurationError(out string error))
            {
                throw new InvalidOperationException($"LevelDefinition '{name}' is invalid: {error}");
            }
        }
        private bool TryFindConfigurationError(out string error)
        {
            if (rows <= 0)
            {
                error = $"Rows must be greater than zero (was {rows}).";
                return true;
            }

            if (columns <= 0)
            {
                error = $"Columns must be greater than zero (was {columns}).";
                return true;
            }

            if (moveLimit <= 0)
            {
                error = $"MoveLimit must be greater than zero (was {moveLimit}).";
                return true;
            }

            if (availableColors == null || availableColors.Length == 0)
            {
                error = "AvailableColors must contain at least one colour.";
                return true;
            }

            if (goals == null)
            {
                error = "Goals array is missing.";
                return true;
            }

            // A level may ask only for crates, or only for colours, but not for nothing.
            if (goals.Length == 0 && crateGoalTarget <= 0)
            {
                error = "A level needs at least one goal.";
                return true;
            }

            if (crateGoalTarget < 0)
            {
                error = $"CrateGoalTarget cannot be negative (was {crateGoalTarget}).";
                return true;
            }

            var seen = new HashSet<BlockColor>();

            foreach (ColorGoalDefinition goal in goals)
            {
                if (goal.TargetCount <= 0)
                {
                    error = $"Goal '{goal}' must have a target greater than zero.";
                    return true;
                }
                if (!seen.Add(goal.Color))
                {
                    error = $"Duplicate goal for colour {goal.Color}; combine them into one.";
                    return true;
                }

                // A goal for a colour refill can never produce is unwinnable.
                if (Array.IndexOf(availableColors, goal.Color) < 0)
                {
                    error = $"Goal '{goal}' asks for a colour that is not in AvailableColors.";
                    return true;
                }
            }

            if (obstacles == null)
            {
                error = "Obstacles array is missing.";
                return true;
            }

            var occupied = new HashSet<(int, int)>();

            foreach (ObstaclePlacement placement in obstacles)
            {
                if (placement.Row < 0 || placement.Row >= rows ||
                    placement.Column < 0 || placement.Column >= columns)
                {
                    error = $"Obstacle {placement} is outside a {rows}x{columns} board.";
                    return true;
                }

                if (!occupied.Add((placement.Row, placement.Column)))
                {
                    error = $"Two obstacles share the cell r{placement.Row}c{placement.Column}.";
                    return true;
                }
            }

            // "Destroy 5 crates" on a board holding 3 is unwinnable.
            if (crateGoalTarget > obstacles.Length)
            {
                error = $"Crate goal asks for {crateGoalTarget} crates but the level places only {obstacles.Length}.";
                return true;
            }

            error = null;
            return false;
        }
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (TryFindConfigurationError(out string error))
            {
                Debug.LogWarning($"LevelDefinition '{name}': {error}", this);
            }
        }

        public override string ToString() =>
            $"Level {levelNumber} ({rows}x{columns}, {moveLimit} moves, {string.Join("/", goals)}"
            + (crateGoalTarget > 0 ? $"/Crates x{crateGoalTarget}" : string.Empty)
            + (obstacles.Length > 0 ? $", {obstacles.Length} crates placed" : string.Empty) + ")";
    }
}
