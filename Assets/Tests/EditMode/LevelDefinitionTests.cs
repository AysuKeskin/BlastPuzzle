using System;
using BlastPuzzle.Blocks;
using BlastPuzzle.Goals;
using BlastPuzzle.Levels;
using BlastPuzzle.Obstacles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BlastPuzzle.Tests.EditMode
{
    // LevelDefinition's fields are private [SerializeField], so tests build instances the
    // same way the Inspector does -- through SerializedObject. That is a supported Unity
    // API rather than reflection into implementation details, and it means production needs
    // no test-only constructor or setters.
    public sealed class LevelDefinitionTests
    {
        private LevelDefinition level;

        [SetUp]
        public void SetUp() => level = ScriptableObject.CreateInstance<LevelDefinition>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(level);

        private static void Configure(
            LevelDefinition target,
            int rows = 8,
            int columns = 8,
            int moveLimit = 20,
            BlockColor[] colors = null,
            ColorGoalDefinition[] goals = null,
            ObstaclePlacement[] obstacles = null,
            int crateGoalTarget = 0)
        {
            colors ??= new[] { BlockColor.Red, BlockColor.Blue, BlockColor.Green };
            goals ??= new[] { new ColorGoalDefinition(BlockColor.Blue, 10) };

            var so = new SerializedObject(target);
            so.FindProperty("rows").intValue = rows;
            so.FindProperty("columns").intValue = columns;
            so.FindProperty("moveLimit").intValue = moveLimit;

            SerializedProperty colorsProp = so.FindProperty("availableColors");
            colorsProp.arraySize = colors.Length;
            for (int i = 0; i < colors.Length; i++)
            {
                colorsProp.GetArrayElementAtIndex(i).enumValueIndex = (int)colors[i];
            }

            SerializedProperty goalsProp = so.FindProperty("goals");
            goalsProp.arraySize = goals.Length;
            for (int i = 0; i < goals.Length; i++)
            {
                SerializedProperty element = goalsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("color").enumValueIndex = (int)goals[i].Color;
                element.FindPropertyRelative("targetCount").intValue = goals[i].TargetCount;
            }

            SerializedProperty obstaclesProp = so.FindProperty("obstacles");
            obstacles ??= Array.Empty<ObstaclePlacement>();
            obstaclesProp.arraySize = obstacles.Length;
            for (int i = 0; i < obstacles.Length; i++)
            {
                SerializedProperty element = obstaclesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("row").intValue = obstacles[i].Row;
                element.FindPropertyRelative("column").intValue = obstacles[i].Column;
                element.FindPropertyRelative("type").enumValueIndex = (int)obstacles[i].Type;
            }

            so.FindProperty("crateGoalTarget").intValue = crateGoalTarget;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ObstaclePlacement Crate(int row, int column) =>
            new ObstaclePlacement(row, column, ObstacleType.Crate);

        [Test]
        public void ValidLevelDefinition_IsAccepted()
        {
            Configure(level, rows: 6, columns: 6, moveLimit: 12);

            Assert.DoesNotThrow(() => level.Validate());
            Assert.That(level.Rows, Is.EqualTo(6));
            Assert.That(level.Columns, Is.EqualTo(6));
            Assert.That(level.MoveLimit, Is.EqualTo(12));
        }

        [Test]
        public void ZeroRows_IsRejected()
        {
            Configure(level, rows: 0);
            Assert.Throws<InvalidOperationException>(() => level.Validate());
        }

        [Test]
        public void ZeroColumns_IsRejected()
        {
            Configure(level, columns: 0);
            Assert.Throws<InvalidOperationException>(() => level.Validate());
        }

        [Test]
        public void ZeroMoveLimit_IsRejected()
        {
            Configure(level, moveLimit: 0);
            Assert.Throws<InvalidOperationException>(() => level.Validate());
        }

        [Test]
        public void NoAvailableColors_IsRejected()
        {
            Configure(level, colors: Array.Empty<BlockColor>());
            Assert.Throws<InvalidOperationException>(() => level.Validate());
        }

        [Test]
        public void GoalWithNonPositiveTarget_IsRejected()
        {
            Configure(level, goals: new[] { new ColorGoalDefinition(BlockColor.Blue, 0) });
            Assert.Throws<InvalidOperationException>(() => level.Validate());

            Configure(level, goals: new[] { new ColorGoalDefinition(BlockColor.Blue, -4) });
            Assert.Throws<InvalidOperationException>(() => level.Validate());
        }

        [Test]
        public void DuplicateGoalColors_AreRejected()
        {
            // "Blue x5" plus "Blue x8" is ambiguous; one "Blue x13" says it unambiguously.
            Configure(level, goals: new[]
            {
                new ColorGoalDefinition(BlockColor.Blue, 5),
                new ColorGoalDefinition(BlockColor.Blue, 8)
            });

            Assert.Throws<InvalidOperationException>(() => level.Validate());
        }

        [Test]
        public void GoalForUnavailableColor_IsRejected()
        {
            // Refill can never produce Purple here, so the goal could never be completed.
            Configure(level,
                colors: new[] { BlockColor.Red, BlockColor.Blue },
                goals: new[] { new ColorGoalDefinition(BlockColor.Purple, 5) });

            Assert.Throws<InvalidOperationException>(() => level.Validate());
        }

        [Test]
        public void RuntimeGoals_StartAtZeroProgress()
        {
            Configure(level, goals: new[]
            {
                new ColorGoalDefinition(BlockColor.Blue, 8),
                new ColorGoalDefinition(BlockColor.Red, 5)
            });

            GoalTracker tracker = level.CreateRuntimeGoals();

            Assert.That(tracker.Goals, Has.Count.EqualTo(2));
            foreach (ColorGoal goal in tracker.Goals)
            {
                Assert.That(goal.CurrentCount, Is.Zero);
            }

            Assert.That(tracker.Goals[0].TargetCount, Is.EqualTo(8));
            Assert.That(tracker.Goals[1].TargetCount, Is.EqualTo(5));
        }

        [Test]
        public void CreatingRuntimeGoalsTwice_ReturnsIndependentProgress()
        {
            Configure(level, goals: new[] { new ColorGoalDefinition(BlockColor.Blue, 8) });

            GoalTracker attemptA = level.CreateRuntimeGoals();
            attemptA.ProcessRemovedBlocks(new[] { Block.CreateNormal(BlockColor.Blue), Block.CreateNormal(BlockColor.Blue) });
            Assert.That(attemptA.Goals[0].CurrentCount, Is.EqualTo(2));

            // A retry: fresh objects, progress back at zero.
            GoalTracker attemptB = level.CreateRuntimeGoals();

            Assert.That(attemptB.Goals[0].CurrentCount, Is.Zero, "A new attempt must start from scratch.");
            Assert.That(attemptB.Goals[0], Is.Not.SameAs(attemptA.Goals[0]), "Runtime goals must not be shared.");
            Assert.That(attemptA.Goals[0].CurrentCount, Is.EqualTo(2), "The earlier attempt is unaffected.");
        }

        [Test]
        public void RuntimeGoalProgress_DoesNotMutateDefinition()
        {
            Configure(level, goals: new[] { new ColorGoalDefinition(BlockColor.Blue, 8) });

            GoalTracker tracker = level.CreateRuntimeGoals();
            for (int i = 0; i < 8; i++)
            {
                tracker.ProcessRemovedBlocks(new[] { Block.CreateNormal(BlockColor.Blue) });
            }

            Assert.That(tracker.AreAllGoalsComplete, Is.True);

            // The asset still only describes the requirement; it holds no progress at all.
            Assert.That(level.Goals.Count, Is.EqualTo(1));
            Assert.That(level.Goals[0].Color, Is.EqualTo(BlockColor.Blue));
            Assert.That(level.Goals[0].TargetCount, Is.EqualTo(8));
            Assert.That(level.CreateRuntimeGoals().Goals[0].CurrentCount, Is.Zero);
        }

        [Test]
        public void DifferentLevelDefinitions_ProduceDifferentRuntimeConfiguration()
        {
            LevelDefinition other = ScriptableObject.CreateInstance<LevelDefinition>();

            try
            {
                Configure(level, rows: 6, columns: 6, moveLimit: 12,
                    colors: new[] { BlockColor.Red, BlockColor.Blue, BlockColor.Green },
                    goals: new[] { new ColorGoalDefinition(BlockColor.Blue, 8) });

                Configure(other, rows: 8, columns: 8, moveLimit: 16,
                    colors: new[] { BlockColor.Red, BlockColor.Blue, BlockColor.Green, BlockColor.Yellow },
                    goals: new[]
                    {
                        new ColorGoalDefinition(BlockColor.Red, 10),
                        new ColorGoalDefinition(BlockColor.Blue, 10)
                    });

                Assert.That(level.Rows, Is.Not.EqualTo(other.Rows));
                Assert.That(level.MoveLimit, Is.Not.EqualTo(other.MoveLimit));
                Assert.That(level.AvailableColors.Count, Is.EqualTo(3));
                Assert.That(other.AvailableColors.Count, Is.EqualTo(4));
                Assert.That(level.CreateRuntimeGoals().Goals, Has.Count.EqualTo(1));
                Assert.That(other.CreateRuntimeGoals().Goals, Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void LevelValidationRejectsCrateOutsideBoard()
        {
            Configure(level, rows: 4, columns: 4, obstacles: new[] { Crate(9, 0) });
            Assert.Throws<InvalidOperationException>(() => level.Validate());

            Configure(level, rows: 4, columns: 4, obstacles: new[] { Crate(0, -1) });
            Assert.Throws<InvalidOperationException>(() => level.Validate());
        }

        [Test]
        public void LevelValidationRejectsDuplicateCratePositions()
        {
            Configure(level, rows: 4, columns: 4, obstacles: new[] { Crate(1, 1), Crate(1, 1) });
            Assert.Throws<InvalidOperationException>(() => level.Validate());
        }

        [Test]
        public void LevelValidationRejectsImpossibleCrateGoal()
        {
            // "Destroy 5 crates" on a board that places three is unwinnable.
            Configure(level, rows: 4, columns: 4,
                obstacles: new[] { Crate(0, 0), Crate(1, 1), Crate(2, 2) },
                crateGoalTarget: 5);

            Assert.Throws<InvalidOperationException>(() => level.Validate());
        }

        [Test]
        public void LevelWithCratesAndCrateGoal_IsAccepted()
        {
            Configure(level, rows: 6, columns: 6,
                obstacles: new[] { Crate(0, 0), Crate(1, 1), Crate(2, 2) },
                crateGoalTarget: 3);

            Assert.DoesNotThrow(() => level.Validate());
            Assert.That(level.Obstacles.Count, Is.EqualTo(3));
            Assert.That(level.CrateGoalTarget, Is.EqualTo(3));

            GoalTracker runtime = level.CreateRuntimeGoals();
            Assert.That(runtime.CrateGoal, Is.Not.Null);
            Assert.That(runtime.CrateGoal.CurrentCount, Is.Zero);
            Assert.That(runtime.CrateGoal.TargetCount, Is.EqualTo(3));
        }

        [Test]
        public void LevelWithoutCrateGoal_HasNoRuntimeCrateGoal()
        {
            Configure(level, crateGoalTarget: 0);

            Assert.That(level.CreateRuntimeGoals().CrateGoal, Is.Null);
        }

        [Test]
        public void ShippedLevelAssets_AreAllValid()
        {
            // Guards the real assets: a designer breaking one fails here, not at runtime.
            string[] guids = AssetDatabase.FindAssets("t:LevelDefinition");
            Assert.That(guids, Is.Not.Empty, "No LevelDefinition assets found in the project.");

            foreach (string guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.DoesNotThrow(() => asset.Validate(), $"{asset.name} is invalid.");
                Assert.That(asset.CreateRuntimeGoals().AreAllGoalsComplete, Is.False,
                    $"{asset.name} would be won before the player moves.");
            }
        }
    }
}
