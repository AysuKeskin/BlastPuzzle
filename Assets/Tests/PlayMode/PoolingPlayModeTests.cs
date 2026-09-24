using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using BlastPuzzle.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BlastPuzzle.Tests.PlayMode
{
    // BlockView pooling.
    //
    // Pooling is an optimisation, so the first thing these tests protect is that it changed
    // NOTHING about behaviour: every block still has exactly one view, at the right cell,
    // showing the right thing. On top of that they assert the two properties that make it an
    // optimisation at all -- GameObjects stop being created once the board has warmed up,
    // and a recycled view carries nothing over from the block it used to be.
    //
    // They run in Play Mode because the pool instantiates prefabs and deactivates
    // GameObjects, neither of which is valid in Edit Mode.
    public sealed class PoolingPlayModeTests
    {
        private const string GameplaySceneName = "Gameplay";

        private GameplayController controller;
        private BoardView boardView;
        private Board board;

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene(GameplaySceneName);
            yield return null;
            yield return null;

            controller = Object.FindFirstObjectByType<GameplayController>();
            boardView = Object.FindFirstObjectByType<BoardView>();

            FieldInfo field = typeof(GameplayController)
                .GetField("board", BindingFlags.NonPublic | BindingFlags.Instance);
            board = (Board)field.GetValue(controller);

            Assert.That(controller, Is.Not.Null);
            Assert.That(boardView, Is.Not.Null);
            Assert.That(board, Is.Not.Null);
        }

        private IEnumerator WaitUntilResolved(float timeoutSeconds = 10f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (controller.State == GameplayState.ResolvingMove)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"Resolution did not finish within {timeoutSeconds}s.");
                }

                yield return null;
            }
        }

        private static GoalTracker UnreachableGoal() =>
            new GoalTracker(new[] { new ColorGoal(BlockColor.Blue, 9999) });

        private static IReadOnlyList<BlockColor> DemoColors() => new[]
        {
            BlockColor.Red, BlockColor.Blue, BlockColor.Green, BlockColor.Yellow, BlockColor.Purple
        };

        private int OccupiedCells()
        {
            int count = 0;

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    if (board.GetCell(row, column).HasBlock)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private BoardPosition FirstValidGroup()
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    var position = new BoardPosition(row, column);

                    if (!board.GetCell(position).HasBlock)
                    {
                        continue;
                    }

                    if (ConnectedGroupFinder.FindConnectedGroup(board, position).Count >= 2)
                    {
                        return position;
                    }
                }
            }

            return new BoardPosition(-1, -1);
        }

        // Replaces a cell's contents and its view through the public presentation API, so
        // the pool sees exactly the calls gameplay would make.
        private void PlaceBlock(BoardPosition position, Block block)
        {
            if (board.GetCell(position).HasBlock)
            {
                boardView.RemoveViews(new[] { board.RemoveBlock(position) });
            }

            board.SetBlock(position, block);
            boardView.AddViews(new[] { new BlockSpawn(block, position) });
        }

        private BoardPosition FirstFreeStandingCell()
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    if (board.GetCell(row, column).HasBlock)
                    {
                        return new BoardPosition(row, column);
                    }
                }
            }

            Assert.Fail("The demo board has no blocks at all.");
            return default;
        }

        private static SpriteRenderer IconRendererOf(BlockView view)
        {
            Transform icon = view.transform.Find("Icon");
            Assert.That(icon, Is.Not.Null, "The BlockView prefab is expected to have an Icon child.");
            return icon.GetComponent<SpriteRenderer>();
        }

        // Shrinks every animation so a long game can be played inside a test's patience.
        // The production coroutines still run start to finish -- nothing is skipped, and no
        // test-only shortcut through the lifecycle is opened. Only the clock is faster.
        private void UseFastAnimations()
        {
            string[] durations =
            {
                "removalDuration", "crateRemovalDuration",
                "gravityBaseDuration", "gravityPerCellDuration",
                "refillBaseDuration", "refillPerCellDuration",
                "powerUpPopDuration", "activationDuration"
            };

            foreach (string name in durations)
            {
                FieldInfo field = typeof(BoardView)
                    .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

                Assert.That(field, Is.Not.Null, $"BoardView no longer has a {name} field.");
                field.SetValue(boardView, 0.01f);
            }
        }

        // ---------------------------------------------------------------- baseline

        // The starting board is fully staffed and nothing is sitting spare.
        [UnityTest]
        public IEnumerator BuildFillsTheBoardAndLeavesThePoolEmpty()
        {
            yield return LoadScene();

            Assert.That(boardView.ActiveViewCount, Is.EqualTo(OccupiedCells()),
                "Every occupied cell should have exactly one live view after Build.");
            Assert.That(boardView.PoolInactiveCount, Is.EqualTo(0),
                "A freshly built board has released nothing yet.");
            Assert.That(boardView.TotalViewsCreated, Is.EqualTo(boardView.ActiveViewCount),
                "The first build has to instantiate one view per block; nothing existed to recycle.");
        }

        // ---------------------------------------------------------------- reuse

        // The heart of the milestone: after the first board is built, playing does not
        // create GameObjects. Removal releases views and refill takes the same ones back.
        [UnityTest]
        public IEnumerator AMoveRecyclesViewsInsteadOfCreatingNewOnes()
        {
            yield return LoadScene();

            int createdAfterBuild = boardView.TotalViewsCreated;
            BoardPosition target = FirstValidGroup();
            Assert.That(target.Row, Is.GreaterThanOrEqualTo(0), "The demo board has no valid group to tap.");

            controller.HandleBlockSelected(target);
            yield return WaitUntilResolved();

            Assert.That(boardView.TotalViewsCreated, Is.EqualTo(createdAfterBuild),
                "Refill should have been served entirely from the pool.");
            Assert.That(boardView.ActiveViewCount, Is.EqualTo(OccupiedCells()),
                "The board is full again, so every view should be back in play.");
        }

        // Releasing hands the GameObject back, it does not destroy it: the object survives,
        // simply switched off, which is the whole point of the exercise.
        [UnityTest]
        public IEnumerator AReleasedViewIsKeptAliveButDeactivated()
        {
            yield return LoadScene();

            BoardPosition position = FirstFreeStandingCell();
            BlockView view = boardView.GetViewFor(board.GetCell(position).Block);
            Assert.That(view, Is.Not.Null);

            boardView.RemoveViews(new[] { board.RemoveBlock(position) });

            // Destroy is deferred to the end of the frame, so waiting one frame is what
            // would expose an accidental Destroy hiding behind the release.
            yield return null;

            Assert.That(view == null, Is.False, "The released view must not have been destroyed.");
            Assert.That(view.gameObject.activeSelf, Is.False, "A pooled view has to be switched off.");
            Assert.That(view.Block, Is.Null, "A pooled view must not still point at its old block.");
            Assert.That(boardView.PoolInactiveCount, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- identity

        // The dangerous half of reuse: the GameObject comes back, the dead Block must not.
        // If the mapping outlived the block, a stale lookup would silently succeed and hand
        // out a view that now belongs to somebody else.
        [UnityTest]
        public IEnumerator ARemovedBlockLosesItsMappingAlthoughItsViewReturns()
        {
            yield return LoadScene();

            BoardPosition position = FirstFreeStandingCell();
            Block deadBlock = board.GetCell(position).Block;
            BlockView view = boardView.GetViewFor(deadBlock);

            boardView.RemoveViews(new[] { board.RemoveBlock(position) });

            Assert.That(boardView.GetViewFor(deadBlock), Is.Null,
                "The removed block must no longer resolve to any view.");

            // Take the same object back out for a brand new block.
            Block newBlock = Block.CreateNormal(BlockColor.Green);
            PlaceBlock(position, newBlock);

            Assert.That(ReferenceEquals(boardView.GetViewFor(newBlock), view), Is.True,
                "The pooled object should have been recycled for the new block.");
            Assert.That(boardView.GetViewFor(deadBlock), Is.Null,
                "The dead block must still resolve to nothing, even now that its old view is live again.");
            Assert.That(view.Block, Is.SameAs(newBlock));
        }

        // ---------------------------------------------------------------- visual reset

        // A bomb's view reused as an ordinary block must show no trace of the bomb. This is
        // the failure pooling is famous for, and it is invisible to every logic test.
        [UnityTest]
        public IEnumerator ARecycledPowerUpViewShowsNothingOfItsPreviousLife()
        {
            yield return LoadScene();

            BoardPosition position = FirstFreeStandingCell();

            Block bomb = Block.CreateBomb(BlockColor.Red);
            PlaceBlock(position, bomb);

            BlockView view = boardView.GetViewFor(bomb);
            SpriteRenderer icon = IconRendererOf(view);
            Assert.That(icon.enabled, Is.True, "A bomb is expected to show its icon.");

            // Rough it up exactly as a removal animation would before it is released.
            view.SetScale(0.2f);
            view.SetAlpha(0.1f);

            boardView.RemoveViews(new[] { board.RemoveBlock(position) });

            Block plainBlock = Block.CreateNormal(BlockColor.Blue);
            PlaceBlock(position, plainBlock);

            Assert.That(ReferenceEquals(boardView.GetViewFor(plainBlock), view), Is.True,
                "This test is only meaningful if the same object really was recycled.");

            Assert.That(icon.enabled, Is.False, "The bomb icon must be gone.");
            Assert.That(icon.sprite, Is.Null, "The bomb sprite must have been cleared.");
            Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one),
                "The shrunken removal scale must not survive into the next life.");

            SpriteRenderer body = view.GetComponent<SpriteRenderer>();
            Assert.That(body.color.a, Is.EqualTo(1f).Within(0.0001f),
                "The faded removal alpha must not survive into the next life.");
            Assert.That(body.color, Is.EqualTo(BlockColorPalette.ToDisplayColor(BlockColor.Blue)),
                "The recycled view must take the new block's colour.");
        }

        // The reverse direction: a plain block's view reused for a rocket must pick the
        // icon up, not merely fail to clear one.
        [UnityTest]
        public IEnumerator ARecycledPlainViewTakesOnAPowerUpIcon()
        {
            yield return LoadScene();

            BoardPosition position = FirstFreeStandingCell();
            boardView.RemoveViews(new[] { board.RemoveBlock(position) });

            Block rocket = Block.CreateRocket(BlockColor.Yellow, RocketDirection.Vertical);
            PlaceBlock(position, rocket);

            SpriteRenderer icon = IconRendererOf(boardView.GetViewFor(rocket));

            Assert.That(icon.enabled, Is.True);
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(icon.color.a, Is.EqualTo(1f).Within(0.0001f));
        }

        // ---------------------------------------------------------------- clear

        // Rebuilding a board recycles the previous one's views rather than churning the
        // whole screen through Destroy and Instantiate.
        [UnityTest]
        public IEnumerator ClearReturnsViewsToThePoolAndRebuildTakesThemBack()
        {
            yield return LoadScene();

            int createdAfterBuild = boardView.TotalViewsCreated;
            int live = boardView.ActiveViewCount;

            var survivors = new List<BlockView>();

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);

                    if (cell.HasBlock)
                    {
                        survivors.Add(boardView.GetViewFor(cell.Block));
                    }
                }
            }

            boardView.Clear();
            yield return null;

            Assert.That(boardView.ActiveViewCount, Is.EqualTo(0));
            Assert.That(boardView.PoolInactiveCount, Is.EqualTo(live),
                "Clear should have released every view rather than destroying it.");

            foreach (BlockView view in survivors)
            {
                Assert.That(view == null, Is.False, "Clear must not destroy block views any more.");
            }

            boardView.Build(board);

            Assert.That(boardView.TotalViewsCreated, Is.EqualTo(createdAfterBuild),
                "A rebuild of the same board should not have instantiated anything.");
            Assert.That(boardView.ActiveViewCount, Is.EqualTo(OccupiedCells()));
            Assert.That(boardView.PoolInactiveCount, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------- safety net

        // collectionCheck is what turns the worst pooling bug -- the same object handed to
        // two owners -- from a silent corruption into an exception. This test releases a
        // view that is already in the pool, which is the shape that mistake takes.
        [UnityTest]
        public IEnumerator ReleasingTheSameViewTwiceThrows()
        {
            yield return LoadScene();

            BoardPosition position = FirstFreeStandingCell();
            BlockView view = boardView.GetViewFor(board.GetCell(position).Block);

            boardView.RemoveViews(new[] { board.RemoveBlock(position) });

            FieldInfo field = typeof(BoardView)
                .GetField("viewPool", BindingFlags.NonPublic | BindingFlags.Instance);
            var pool = (UnityEngine.Pool.ObjectPool<BlockView>)field.GetValue(boardView);

            Assert.That(() => pool.Release(view), Throws.InvalidOperationException,
                "collectionCheck should reject a second release of the same view.");
        }

        // ---------------------------------------------------------------- stress

        // Long play is where a leak shows itself: one view lost per move looks like nothing
        // for three moves and like a bug after thirty. Animations are shortened, but every
        // move still goes through the real coroutines, the real release points and the real
        // refill -- the same code path a player drives, just on a faster clock.
        //
        // The bound is the CELL COUNT, not the number of views the first build needed. A
        // board that starts with crates has fewer blocks than cells, and every crate that
        // breaks frees its cell for refill -- so demand legitimately grows during play. What
        // may never happen is a view being created beyond one per cell, or going missing.
        [UnityTest]
        public IEnumerator ManyMovesStayWithinOneViewPerCellAndLoseNone()
        {
            yield return LoadScene();

            UseFastAnimations();
            controller.Initialise(board, DemoColors(), new System.Random(7), 200, UnreachableGoal());

            int cells = board.Rows * board.Columns;
            int movesPlayed = 0;

            // What an unpooled implementation would have instantiated: every blasted block
            // is replaced by a refilled one. Compared against TotalViewsCreated at the end,
            // this is the measurement that says pooling actually did something.
            int naiveInstantiations = 0;

            for (int move = 0; move < 40; move++)
            {
                BoardPosition target = FirstValidGroup();

                if (target.Row < 0)
                {
                    // No group left to tap. Rare, and not this test's subject.
                    break;
                }

                naiveInstantiations += ConnectedGroupFinder.FindConnectedGroup(board, target).Count;

                controller.HandleBlockSelected(target);
                yield return WaitUntilResolved();
                movesPlayed++;

                Assert.That(boardView.ActiveViewCount, Is.EqualTo(OccupiedCells()),
                    $"View count drifted from the board on move {movesPlayed}.");
                Assert.That(boardView.ActiveViewCount + boardView.PoolInactiveCount,
                    Is.EqualTo(boardView.TotalViewsCreated),
                    $"A view went missing on move {movesPlayed}: it is neither live nor pooled.");
                Assert.That(boardView.TotalViewsCreated, Is.LessThanOrEqualTo(cells),
                    $"More views exist than the board has cells, after move {movesPlayed}.");
            }

            Assert.That(movesPlayed, Is.GreaterThanOrEqualTo(20),
                "The stress test needs a decent run of moves to be worth anything.");

            // Not a benchmark -- no timing is claimed. It is a straight object count: the
            // run blasted this many blocks and instantiated far fewer GameObjects.
            Assert.That(naiveInstantiations, Is.GreaterThan(boardView.TotalViewsCreated * 2),
                $"Only {naiveInstantiations} blocks were blasted over {movesPlayed} moves, which is "
                + "too few for this test to demonstrate reuse at all.");

            Debug.Log($"Pooling stress: {movesPlayed} moves, {naiveInstantiations} blocks blasted, "
                + $"{boardView.TotalViewsCreated} GameObjects ever created "
                + $"({boardView.ActiveViewCount} live, {boardView.PoolInactiveCount} pooled).");
        }

    }
}
