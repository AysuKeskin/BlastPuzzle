using System;
using System.Reflection;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using NUnit.Framework;
using UnityEngine;

namespace BlastPuzzle.Tests.EditMode
{
    // GameplayController is a MonoBehaviour, but the three cases below never reach its
    // BoardView: an invalid selection returns before any view call, and a rejected
    // re-entrant selection returns on the very first line. So they need a component but no
    // scene, no prefab and no rendering, and stay ordinary EditMode tests.
    //
    // The successful-move case DOES need a wired BoardView and a block prefab, so it is
    // verified in Play Mode instead of dragging a prefab into a unit test.
    public sealed class GameplayControllerStateTests
    {
        private GameObject host;
        private GameplayController controller;

        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("GameplayControllerUnderTest");
            controller = host.AddComponent<GameplayController>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
        }

        // Test-only access to the controller's private state field. Deliberately reflection
        // rather than an internal setter: production code exposes no way to write State, and
        // adding one purely so a test could call it would put a hole in the real API.
        // Level setup these state tests do not care about, in one place.
        private static void InitialiseWith(GameplayController target, Board board, params BlockColor[] colors)
        {
            var goals = new GoalTracker(new[] { new ColorGoal(colors[0], 99) });
            target.Initialise(board, colors, new System.Random(1), 20, goals);
        }

        private static void ForceState(GameplayController target, GameplayState value)
        {
            FieldInfo field = typeof(GameplayController)
                .GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null,
                "GameplayController.state was renamed; update this test seam.");

            field.SetValue(target, value);
        }

        [Test]
        public void InitialState_IsWaitingForInput()
        {
            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
        }

        [Test]
        public void InvalidSelection_LeavesStateWaitingForInput()
        {
            //  R B     the lone Red is a real group, but only of size 1
            //  B B
            Board board = BoardLayout.Build(
                "R B",
                "B B");
            InitialiseWith(controller, board, BlockColor.Red, BlockColor.Blue);

            string before = Describe(board);
            controller.HandleBlockSelected(At(1, 0));

            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
            Assert.That(Describe(board), Is.EqualTo(before), "An undersized group must not change the board.");
        }

        [Test]
        public void EmptyCellSelection_LeavesStateWaitingForInput()
        {
            Board board = BoardLayout.Build("R . R");
            InitialiseWith(controller, board, BlockColor.Red);

            string before = Describe(board);
            controller.HandleBlockSelected(At(0, 1));

            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
            Assert.That(Describe(board), Is.EqualTo(before));
        }

        [Test]
        public void OutOfBoardSelection_LeavesStateWaitingForInput()
        {
            Board board = BoardLayout.Build("R R");
            InitialiseWith(controller, board, BlockColor.Red);

            string before = Describe(board);
            controller.HandleBlockSelected(At(99, 99));

            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
            Assert.That(Describe(board), Is.EqualTo(before));
        }

        [Test]
        public void SelectionWhileResolving_IsIgnored()
        {
            // A genuinely valid group: without the guard this WOULD blast.
            Board board = BoardLayout.Build(
                "R R",
                "B B");
            InitialiseWith(controller, board, BlockColor.Red, BlockColor.Blue);

            string before = Describe(board);
            ForceState(controller, GameplayState.ResolvingMove);

            // No BoardView is wired on this component. If the guard failed, the pipeline
            // would run and throw a NullReferenceException on the first view call -- so this
            // test fails loudly rather than quietly if re-entrancy stops being blocked.
            controller.HandleBlockSelected(At(1, 0));

            Assert.That(Describe(board), Is.EqualTo(before), "A re-entrant selection must not touch the board.");
            Assert.That(controller.State, Is.EqualTo(GameplayState.ResolvingMove),
                "The rejected call must not alter the in-progress state.");
        }

        [Test]
        public void GuardRejectsWithoutConsultingTheBoard()
        {
            // The guard is the very first thing in the method: a controller with no board at
            // all must still return quietly rather than warn or throw.
            ForceState(controller, GameplayState.ResolvingMove);

            Assert.DoesNotThrow(() => controller.HandleBlockSelected(At(0, 0)));
            Assert.That(controller.State, Is.EqualTo(GameplayState.ResolvingMove));
        }

        private static string Describe(Board board)
        {
            var text = new System.Text.StringBuilder();

            for (int row = board.Rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);
                    text.Append(cell.IsEmpty ? "." : cell.Block.Color.ToString()[0].ToString());
                }

                text.Append('/');
            }

            return text.ToString();
        }
    }
}
