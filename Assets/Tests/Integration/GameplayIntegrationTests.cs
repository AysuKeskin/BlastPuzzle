using System.Collections;
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using BlastPuzzle.Presentation;
using BlastPuzzle.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BlastPuzzle.Tests.Integration
{
    public sealed class GameplayIntegrationTests
    {
        [UnitySetUp]
        public IEnumerator LoadGameplay()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnclosedGap_StaysEmptyUntilRoofIsRemoved()
        {
            var board = new Board(3, 3);
            var gap = new BoardPosition(1, 1);
            var roof = new BoardPosition(2, 1);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    var position = new BoardPosition(row, col);
                    if (position == gap) continue;
                    if (position == roof || (row == 1 && col != 1))
                        board.PlaceObstacle(position, new BlastPuzzle.Obstacles.Obstacle(BlastPuzzle.Obstacles.ObstacleType.Crate));
                    else board.SetBlock(position, Block.CreateNormal(BlockColor.Blue));
                }
            var view = Object.FindFirstObjectByType<BoardView>();
            var gameplay = Object.FindFirstObjectByType<GameplayController>();
            view.Build(board);
            gameplay.Initialise(board, new[] { BlockColor.Blue }, new System.Random(1), 20,
                new GoalTracker(new[] { new ColorGoal(BlockColor.Blue, 1000) }));
            var settle = typeof(GameplayController).GetMethod("SettleBoard", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                yield return gameplay.StartCoroutine((IEnumerator)settle.Invoke(gameplay, null));
                Assert.That(board.GetCell(gap).IsEmpty, Is.True);
                Assert.That(view.ActiveViewCount, Is.EqualTo(5));
            }
            gameplay.HandleBlockSelected(gap);
            Assert.That(gameplay.MovesRemaining, Is.EqualTo(20));
            Assert.That(gameplay.State, Is.EqualTo(GameplayState.WaitingForInput));
            BoardShuffleResolver.Shuffle(board, 2, new System.Random(2));
            Assert.That(board.GetCell(gap).IsEmpty, Is.True);

            board.RemoveObstacle(roof);
            view.Build(board);
            yield return gameplay.StartCoroutine((IEnumerator)settle.Invoke(gameplay, null));
            Assert.That(board.GetCell(gap).HasBlock, Is.True, "Opening the roof must restore natural refill.");
            Assert.That(view.ActiveViewCount, Is.EqualTo(7));
            Assert.That(view.GetViewFor(board.GetCell(gap).Block).Position, Is.EqualTo(gap));
        }

        [UnityTest]
        public IEnumerator CoveredGap_SettlesUsingExistingBlocksAndKeepsPoolInSync()
        {
            var board = new Board(3, 3);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    board.SetBlock(new BoardPosition(row, col), Block.CreateNormal(BlockColor.Blue));
            board.RemoveBlock(new BoardPosition(2, 1));
            board.PlaceObstacle(new BoardPosition(2, 1),
                new BlastPuzzle.Obstacles.Obstacle(BlastPuzzle.Obstacles.ObstacleType.Crate));
            board.RemoveBlock(new BoardPosition(1, 1));
            var view = Object.FindFirstObjectByType<BoardView>();
            var gameplay = Object.FindFirstObjectByType<GameplayController>();
            view.Build(board);
            gameplay.Initialise(board, new[] { BlockColor.Blue }, new System.Random(1), 20,
                new GoalTracker(new[] { new ColorGoal(BlockColor.Blue, 1000) }));
            var sourceLeft = board.GetCell(2, 0).Block;
            var sourceRight = board.GetCell(2, 2).Block;
            var settle = typeof(GameplayController).GetMethod("SettleBoard", BindingFlags.NonPublic | BindingFlags.Instance);
            var speed = typeof(BoardView).GetField("gravityPerCellDuration", BindingFlags.NonPublic | BindingFlags.Instance);
            float originalSpeed = (float)speed.GetValue(view);
            speed.SetValue(view, .5f); // Make the intermediate animation observable independent of normal timing.
            try
            {
                var routine = gameplay.StartCoroutine((IEnumerator)settle.Invoke(gameplay, null));
                var moved = board.GetCell(1, 1).Block;
                var movedView = view.GetViewFor(moved);
                int sourceColumn = moved == sourceLeft ? 0 : 2;
                var incomingView = view.GetViewFor(board.GetCell(2, sourceColumn).Block);
                Assert.That(incomingView, Is.Not.Null, "Refill must be planned before the diagonal animation finishes.");
                Vector3 diagonalStart = movedView.transform.localPosition;
                Vector3 incomingStart = incomingView.transform.localPosition;
                yield return null;
                Assert.That(movedView.transform.localPosition, Is.Not.EqualTo(diagonalStart));
                Assert.That(incomingView.transform.localPosition.y, Is.LessThan(incomingStart.y),
                    "Incoming blocks must follow while diagonal movement is still in progress.");
                yield return routine;
            }
            finally { speed.SetValue(view, originalSpeed); }
            Assert.That(board.GetCell(1, 1).Block == sourceLeft || board.GetCell(1, 1).Block == sourceRight, Is.True);
            Assert.That(view.ActiveViewCount, Is.EqualTo(8));
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    var cell = board.GetCell(row, col);
                    if (!cell.HasBlock) continue;
                    Assert.That(view.GetViewFor(cell.Block).Position, Is.EqualTo(cell.Position));
                    Assert.That(view.GetViewFor(cell.Block).GetComponent<SpriteRenderer>().maskInteraction,
                        Is.EqualTo(SpriteMaskInteraction.VisibleInsideMask));
                }
        }

        [UnityTest]
        public IEnumerator UITap_DoesNotConsumeBoardMove()
        {
            var input = Object.FindFirstObjectByType<BoardInputHandler>();
            var gameplay = Object.FindFirstObjectByType<GameplayController>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var cover = new GameObject("TestModal", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)cover.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return null;
                var position = new Vector2(Screen.width * .5f, Screen.height * .4f);
                Assert.That(input.IsOverUI(position), Is.True);
                int moves = gameplay.MovesRemaining;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
                yield return null;
                Assert.That(gameplay.MovesRemaining, Is.EqualTo(moves));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                Object.Destroy(cover);
            }
        }

        [UnityTest]
        public IEnumerator SettingsPanel_BlocksBoardPressesAndReleasesInputOnClose()
        {
            var panel = Object.FindFirstObjectByType<SettingsPanel>();
            var input = Object.FindFirstObjectByType<BoardInputHandler>();
            var gameplay = Object.FindFirstObjectByType<GameplayController>();
            int moves = gameplay.MovesRemaining;
            panel.Open();
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(input.InputBlocked, Is.True);
            var press = typeof(BoardInputHandler).GetMethod("OnPressPerformed", BindingFlags.NonPublic | BindingFlags.Instance);
            // A blocked press must return before accessing input or board coordinates.
            press.Invoke(input, new object[] { System.Activator.CreateInstance(press.GetParameters()[0].ParameterType) });
            yield return null;
            Assert.That(gameplay.MovesRemaining, Is.EqualTo(moves));
            panel.Close();
            Assert.That(input.InputBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator ChainReaction_PlaysEveryActivationAndConsumesOneMove()
        {
            var board = new Board(3, 3);
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    board.SetBlock(new BoardPosition(row, column), Block.CreateNormal(BlockColor.Blue));
            board.RemoveBlock(new BoardPosition(0, 0));
            board.SetBlock(new BoardPosition(0, 0), Block.CreateRocket(BlockColor.Blue, RocketDirection.Horizontal));
            board.RemoveBlock(new BoardPosition(0, 1));
            board.SetBlock(new BoardPosition(0, 1), Block.CreateBomb(BlockColor.Blue));
            var view = Object.FindFirstObjectByType<BoardView>();
            var gameplay = Object.FindFirstObjectByType<GameplayController>();
            var feedback = Object.FindFirstObjectByType<GameFeedback>();
            var vfx = Object.FindFirstObjectByType<GameplayVFX>();
            int rockets = vfx.RocketStreakCount;
            int bombs = vfx.BombBlastCount;
            view.Build(board);
            // Keep the goal incomplete so this test never changes the real progress save.
            gameplay.Initialise(board, new[] { BlockColor.Blue }, new System.Random(1), 20,
                new GoalTracker(new[] { new ColorGoal(BlockColor.Blue, 1000) }));
            feedback.ResetCounters();
            gameplay.HandleBlockSelected(new BoardPosition(0, 0));
            float deadline = Time.realtimeSinceStartup + 10f;
            while (gameplay.State == GameplayState.ResolvingMove && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(gameplay.State, Is.EqualTo(GameplayState.WaitingForInput));
            Assert.That(gameplay.MovesRemaining, Is.EqualTo(19));
            Assert.That(feedback.RocketActivationCount, Is.EqualTo(1));
            Assert.That(feedback.BombActivationCount, Is.EqualTo(1));
            Assert.That(vfx.RocketStreakCount, Is.EqualTo(rockets + 1));
            Assert.That(vfx.BombBlastCount, Is.EqualTo(bombs + 1));
        }
    }
}
