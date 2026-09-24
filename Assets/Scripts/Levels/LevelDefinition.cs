using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Goals;
using UnityEngine;

namespace BlastPuzzle.Levels
{
    // Everything that makes one level different from another, stored as an asset.
    //
    // A ScriptableObject rather than a MonoBehaviour because this data belongs to no scene
    // object and needs no Update: it is a file a designer edits, not a thing that lives in
    // the world. One Gameplay scene plus Level001.asset IS level 1; swap the reference to
    // Level002.asset and the same scene, running the same code, is level 2.
    //
    // CONFIGURATION ONLY. No MovesRemaining, no goal progress, no GameplayState, no Board,
    // no Block instances. Everything here is read-only to the game at runtime; the mutable
    // counterparts are created from it when an attempt starts.
    [CreateAssetMenu(fileName = "Level", menuName = "BlastPuzzle/Level Definition", order = 0)]
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

        // Converts this configuration into FRESH runtime progress objects.
        //
        // Called once per attempt. Every call allocates new ColorGoal instances starting at
        // zero, so two attempts at the same level share nothing, and nothing a player does
        // can reach back into the asset.
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

        // Throws on invalid configuration rather than quietly repairing it. A level with
        // zero moves or no colours is a mistake in the asset, and the useful moment to find
        // out is at startup with the level's name in the message -- not three systems later
        // when a board fails to fill.
        public void Validate()
        {
            if (TryFindConfigurationError(out string error))
            {
                throw new InvalidOperationException($"LevelDefinition '{name}' is invalid: {error}");
            }
        }

        // Shared by the runtime check above and the editor check below, so the two can
        // never drift apart.
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

                // "Blue x5" and "Blue x8" together is ambiguous -- and one "Blue x13" says
                // the same thing without the ambiguity.
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

        // Editor-only authoring aid. It warns while a designer types, but it is NOT the
        // safety net: OnValidate does not run in a build, which is why Validate() is called
        // explicitly at startup.
        private void OnValidate()
        {
            // An unnamed instance is one created in memory (ScriptableObject.CreateInstance),
            // not an asset on disk -- typically a test building a deliberately invalid
            // configuration. Warning about it tells a designer nothing they can act on and
            // buries genuine warnings, so only named assets are reported.
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
