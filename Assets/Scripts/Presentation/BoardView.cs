using System.Collections;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Obstacles;
using UnityEngine;
using UnityEngine.Pool;

namespace BlastPuzzle.Presentation
{
    // Renders a logical Board as a grid of BlockViews parented under this object.
    //
    // The dependency runs one way only: BoardView reads Board. Board has never heard of
    // BoardView, so the logical state is never derived from where a Transform happens to be.
    // This class contains no gameplay rules -- no matching, no gravity, no scoring. It
    // answers one question: "given this board, what should be on screen?"
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField]
        private BlockView blockPrefab;

        [SerializeField]
        private CrateView cratePrefab;

        [SerializeField]
        private float cellSize = 1f;

        [SerializeField]
        private float spacing = 0.05f;

        // --- animation timing ------------------------------------------------------------
        //
        // All presentation tuning, deliberately here rather than on LevelDefinition: how
        // long a block takes to fall is not a property of a level, and a designer changing
        // level 3 should not be able to change how the game feels everywhere.
        [Header("Animation timing (seconds)")]
        [SerializeField] private float removalDuration = 0.14f;
        [SerializeField] private float crateRemovalDuration = 0.16f;
        [SerializeField] private float gravityBaseDuration = 0.12f;
        [SerializeField] private float gravityPerCellDuration = 0.035f;
        [SerializeField] private float refillBaseDuration = 0.14f;
        [SerializeField] private float refillPerCellDuration = 0.035f;
        [SerializeField] private float powerUpPopDuration = 0.18f;
        [SerializeField] private float activationDuration = 0.12f;

        // BlockViews are recycled rather than destroyed. Refill creates one per removed
        // block every single move, so without this the game would Instantiate and Destroy
        // ten-odd GameObjects per tap for the whole session.
        //
        // The pool lives HERE rather than in its own component because BoardView already
        // owns BlockView lifecycle -- it creates them, maps them to blocks and disposes of
        // them. Splitting that across two objects would divide one responsibility and add a
        // scene reference to wire, for no gain. It is deliberately specific to BlockView:
        // no IPoolable, no registry, no generic framework for a single pooled type.
        private ObjectPool<BlockView> viewPool;

        // Development instrumentation. TotalViewsCreated is the number of GameObjects ever
        // instantiated; if pooling works it stops rising once the board has warmed up,
        // however many moves are played.
        public int TotalViewsCreated { get; private set; }

        public int PoolInactiveCount => viewPool?.CountInactive ?? 0;

        public int ActiveViewCount => viewsByBlock.Count;

        // Reused across animations so a move does not allocate a fresh list per phase.
        private readonly List<ViewTravel> travels = new List<ViewTravel>();
        private readonly List<BlockView> dyingBlocks = new List<BlockView>();
        private readonly List<CrateView> dyingCrates = new List<CrateView>();
        private readonly List<BlockView> releaseBuffer = new List<BlockView>();

        // Used only when no board is available to size the pool from.
        private const int DefaultPoolCapacity = 64;

        // One view's journey for this phase. A struct in a reused list: no per-frame garbage.
        private readonly struct ViewTravel
        {
            public ViewTravel(BlockView view, Vector3 from, Vector3 to, float duration)
            {
                View = view;
                From = from;
                To = to;
                Duration = duration;
            }

            public BlockView View { get; }
            public Vector3 From { get; }
            public Vector3 To { get; }
            public float Duration { get; }
        }

        // Reference identity, not value equality: Block does not override Equals, so two
        // blocks are the same key only if they are literally the same object. That is
        // exactly what is wanted -- "the view showing THIS block" -- and it is what lets
        // Milestone 5/6 find the right view for a block that was removed or moved.
        private readonly Dictionary<Block, BlockView> viewsByBlock = new Dictionary<Block, BlockView>();

        // Crates are keyed by POSITION, not by object identity, precisely because they never
        // move: a crate's cell identifies it for its whole life. Blocks need identity keys
        // because gravity relocates them and a coordinate would go stale mid-move.
        private readonly Dictionary<BoardPosition, CrateView> crateViewsByPosition =
            new Dictionary<BoardPosition, CrateView>();

        private Board board;

        // Distance from one cell's centre to the next, including the gap between them.
        private float CellStride => cellSize + spacing;

        // Half the board's visible extent: every cell's width plus the gaps between them.
        // n cells and (n - 1) gaps, i.e. n * stride - one trailing gap.
        private float HalfBoardWidth => (board.Columns * CellStride - spacing) * 0.5f;

        private float HalfBoardHeight => (board.Rows * CellStride - spacing) * 0.5f;

        // One-shot render. No events and no per-frame Update: nothing changes the board
        // yet, so re-rendering on a schedule would be work with no cause.
        public void Build(Board boardToRender)
        {
            Clear();
            board = boardToRender;

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);

                    if (cell.HasObstacle)
                    {
                        CreateCrateViewFor(cell.Obstacle, cell.Position);
                        continue;
                    }

                    // An empty cell gets no view at all, rather than a hidden or
                    // transparent one. "No block here" and "a block you cannot see"
                    // must never be the same thing on screen.
                    if (!cell.HasBlock)
                    {
                        continue;
                    }

                    CreateViewFor(cell.Block, cell.Position);
                }
            }
        }

        // Animates existing views to the cells their blocks now occupy.
        //
        // No view is created and none is destroyed: these are the same objects, found by
        // Block identity, being repositioned. The board has ALREADY settled by the time this
        // runs -- these moves describe what happened, they do not cause it.
        //
        // ONE coroutine drives every falling view. Yielding per block would serialise them:
        // fifteen blocks would fall one after another over several seconds instead of
        // together in a fraction of one. Here each view's progress is computed against its
        // own duration inside a single per-frame loop, so they travel concurrently and the
        // whole phase costs one coroutine regardless of how many blocks move.
        public IEnumerator AnimateMoves(IReadOnlyList<BlockMove> moves)
        {
            travels.Clear();
            float longest = 0f;

            foreach (BlockMove move in moves)
            {
                if (!viewsByBlock.TryGetValue(move.Block, out BlockView view) || view == null)
                {
                    continue;
                }

                // The cached coordinate follows the BOARD immediately; only the transform
                // lags behind and animates.
                view.SetBoardPosition(move.To);
                view.name = ViewName(move.To);

                Vector3 from = view.transform.localPosition;
                Vector3 to = BoardPositionToLocalPosition(move.To);
                float duration = gravityBaseDuration
                    + Mathf.Abs(move.From.Row - move.To.Row) * gravityPerCellDuration;

                travels.Add(new ViewTravel(view, from, to, duration));
                longest = Mathf.Max(longest, duration);
            }

            yield return RunTravels(longest);
        }

        // Creates the views for newly spawned blocks ABOVE the board and drops them in.
        //
        // BlockSpawn stays pure logical data -- block and target cell. The entry height is
        // computed HERE, because where a block visually comes from is a presentation choice
        // that RefillResolver has no business knowing.
        public IEnumerator AnimateSpawns(IReadOnlyList<BlockSpawn> spawns)
        {
            travels.Clear();
            float longest = 0f;

            // Stagger by column: several blocks entering the same column start at
            // successively greater heights so they queue up rather than overlapping. Spawns
            // arrive column-major, bottom-to-top, so a per-column counter is enough.
            int lastColumn = -1;
            int indexInColumn = 0;

            foreach (BlockSpawn spawn in spawns)
            {
                if (spawn.Position.Column != lastColumn)
                {
                    lastColumn = spawn.Position.Column;
                    indexInColumn = 0;
                }

                BlockView view = CreateViewFor(spawn.Block, spawn.Position);

                // One row above the top of the board, plus one more for each block already
                // entering this column.
                var entry = new BoardPosition(board.Rows + indexInColumn, spawn.Position.Column);
                Vector3 from = BoardPositionToLocalPosition(entry);
                Vector3 to = BoardPositionToLocalPosition(spawn.Position);
                view.SetLocalPosition(from);

                float distance = entry.Row - spawn.Position.Row;
                float duration = refillBaseDuration + distance * refillPerCellDuration;

                travels.Add(new ViewTravel(view, from, to, duration));
                longest = Mathf.Max(longest, duration);
                indexInColumn++;
            }

            yield return RunTravels(longest);
        }

        // The shared batch loop: advance every travel each frame, then snap them all exactly.
        private IEnumerator RunTravels(float longest)
        {
            if (travels.Count == 0)
            {
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < longest)
            {
                elapsed += Time.deltaTime;

                for (int i = 0; i < travels.Count; i++)
                {
                    ViewTravel travel = travels[i];
                    float t = Mathf.Clamp01(elapsed / travel.Duration);
                    travel.View.SetLocalPosition(Vector3.Lerp(travel.From, travel.To, EaseOutCubic(t)));
                }

                yield return null;
            }

            // Snap explicitly rather than trusting the loop to land on t == 1. A frame is
            // never exactly the remaining time, so without this every view would sit a
            // fraction of a pixel short and the error would accumulate across moves.
            for (int i = 0; i < travels.Count; i++)
            {
                travels[i].View.SetLocalPosition(travels[i].To);
            }

            travels.Clear();
        }

        // Fast at first, settling gently at the end -- how a falling object reads. Linear
        // motion looks mechanical because the block arrives at full speed and stops dead.
        private static float EaseOutCubic(float t)
        {
            float inverted = 1f - t;
            return 1f - inverted * inverted * inverted;
        }

        // Creates one view per newly spawned Block.
        //
        // Only the new blocks get views: everything that survived gravity keeps the view it
        // already had, repositioned by ApplyMoves. Recreating survivors would throw away
        // objects that are already correct and, worse, break the Block -> BlockView identity
        // that the rest of the presentation layer depends on.
        public void AddViews(IEnumerable<BlockSpawn> spawns)
        {
            foreach (BlockSpawn spawn in spawns)
            {
                CreateViewFor(spawn.Block, spawn.Position);
            }
        }

        // Animates the destruction of blocks and crates TOGETHER, then destroys their views.
        //
        // One loop for both, so a crate breaks at the same moment as the blocks beside it
        // rather than afterwards. Crates must be visually gone before gravity starts, since
        // their cells are about to be fallen through -- running this phase to completion
        // before the gravity phase is what guarantees that.
        //
        // The dictionary entries are dropped IMMEDIATELY, before any animation: a removed
        // block must stop being queryable through BoardView the instant the board says it is
        // gone. Only a local reference to the doomed GameObject is kept, so it can finish
        // its animation while already being logically absent.
        public IEnumerator AnimateDestruction(
            IReadOnlyList<Block> blocks,
            IReadOnlyList<ObstacleRemoval> obstacles)
        {
            dyingBlocks.Clear();
            dyingCrates.Clear();

            foreach (Block block in blocks)
            {
                if (!viewsByBlock.TryGetValue(block, out BlockView view))
                {
                    continue;
                }

                viewsByBlock.Remove(block);

                if (view != null)
                {
                    dyingBlocks.Add(view);
                }
            }

            foreach (ObstacleRemoval removal in obstacles)
            {
                if (!crateViewsByPosition.TryGetValue(removal.Position, out CrateView view))
                {
                    continue;
                }

                crateViewsByPosition.Remove(removal.Position);

                if (view != null)
                {
                    dyingCrates.Add(view);
                }
            }

            if (dyingBlocks.Count == 0 && dyingCrates.Count == 0)
            {
                yield break;
            }

            float longest = Mathf.Max(
                dyingBlocks.Count > 0 ? removalDuration : 0f,
                dyingCrates.Count > 0 ? crateRemovalDuration : 0f);

            float elapsed = 0f;

            while (elapsed < longest)
            {
                elapsed += Time.deltaTime;

                float blockT = Mathf.Clamp01(elapsed / removalDuration);
                float crateT = Mathf.Clamp01(elapsed / crateRemovalDuration);

                for (int i = 0; i < dyingBlocks.Count; i++)
                {
                    dyingBlocks[i].SetScale(PopThenShrink(blockT));
                    dyingBlocks[i].SetAlpha(1f - blockT);
                }

                for (int i = 0; i < dyingCrates.Count; i++)
                {
                    dyingCrates[i].SetScale(PopThenShrink(crateT));
                    dyingCrates[i].SetAlpha(1f - crateT);
                }

                yield return null;
            }

            // Released only NOW, after the removal animation has finished. Releasing at the
            // moment the block died would let refill hand the same GameObject straight back
            // out while this loop was still shrinking and fading it -- the new block would
            // visibly dissolve on arrival.
            foreach (BlockView view in dyingBlocks)
            {
                ReleaseView(view);
            }

            foreach (CrateView view in dyingCrates)
            {
                Destroy(view.gameObject);
            }

            dyingBlocks.Clear();
            dyingCrates.Clear();
        }

        // A short swell before collapsing, which reads as "popped" rather than "switched off".
        private static float PopThenShrink(float t)
        {
            const float peakAt = 0.3f;
            const float peakScale = 1.15f;

            return t < peakAt
                ? Mathf.Lerp(1f, peakScale, t / peakAt)
                : Mathf.Lerp(peakScale, 0f, (t - peakAt) / (1f - peakAt));
        }

        // A newly earned power-up swelling into existence at the tapped cell. Its logical
        // Block already exists on the board; only the entrance is animated.
        public IEnumerator AnimatePowerUpCreation(BlockSpawn spawn)
        {
            BlockView view = CreateViewFor(spawn.Block, spawn.Position);
            view.SetLocalPosition(BoardPositionToLocalPosition(spawn.Position));

            float elapsed = 0f;

            while (elapsed < powerUpPopDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / powerUpPopDuration);

                // 0 -> 1.15 -> 1
                float scale = t < 0.6f
                    ? Mathf.Lerp(0f, 1.15f, t / 0.6f)
                    : Mathf.Lerp(1.15f, 1f, (t - 0.6f) / 0.4f);

                view.SetScale(scale);
                yield return null;
            }

            view.ResetAppearance();
        }

        // A brief swell on the power-up the player tapped, so the cause of the clear is
        // readable before its footprint disappears.
        public IEnumerator AnimateActivation(Block powerUp)
        {
            if (!viewsByBlock.TryGetValue(powerUp, out BlockView view) || view == null)
            {
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < activationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / activationDuration);

                // Out and back: 1 -> 1.3 -> 1
                float scale = t < 0.5f
                    ? Mathf.Lerp(1f, 1.3f, t / 0.5f)
                    : Mathf.Lerp(1.3f, 1f, (t - 0.5f) / 0.5f);

                view.SetScale(scale);
                yield return null;
            }

            view.ResetAppearance();
        }

        // Destroys the views for crates that were just blasted.
        public void RemoveObstacleViews(IEnumerable<ObstacleRemoval> removals)
        {
            foreach (ObstacleRemoval removal in removals)
            {
                if (!crateViewsByPosition.TryGetValue(removal.Position, out CrateView view))
                {
                    continue;
                }

                // Drop the entry first, for the same reason as blocks: Destroy only takes
                // effect at end of frame, and a stale entry would keep handing out a view
                // that is about to vanish.
                crateViewsByPosition.Remove(removal.Position);

                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }
        }

        // Removes the views showing these blocks immediately, without rebuilding the rest
        // of the board and without animating. Blocks with no view are simply skipped.
        public void RemoveViews(IEnumerable<Block> blocks)
        {
            foreach (Block block in blocks)
            {
                if (!viewsByBlock.TryGetValue(block, out BlockView view))
                {
                    continue;
                }

                // Drop the dictionary entry FIRST. The view is about to go back into the
                // pool and can be handed straight out to a different Block, so a surviving
                // entry would map a dead Block to a view that now belongs to a live one --
                // and ViewCount would drift away from the board's occupied count.
                viewsByBlock.Remove(block);

                ReleaseView(view);
            }
        }

        // Returns every view this component created to the pool and forgets them.
        public void Clear()
        {
            // Copy first: releasing mutates nothing here, but clearing the dictionary while
            // enumerating it would throw, and a rebuild for a different level runs exactly
            // this path.
            releaseBuffer.Clear();
            releaseBuffer.AddRange(viewsByBlock.Values);
            viewsByBlock.Clear();

            foreach (BlockView view in releaseBuffer)
            {
                ReleaseView(view);
            }

            releaseBuffer.Clear();

            // Crates are still destroyed. There are a handful per level and they never
            // respawn during play, so pooling them would add a second lifecycle to reason
            // about in exchange for almost no churn. Worth revisiting only if profiling
            // ever shows crate churn mattering.
            foreach (CrateView view in crateViewsByPosition.Values)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            crateViewsByPosition.Clear();
            board = null;
        }

        // Turns a logical grid coordinate into a local offset from this object's origin.
        //
        // Row 0 is the bottom row and column 0 the left, matching the domain convention,
        // so +row moves up (+y) and +column moves right (+x) with no flip anywhere.
        //
        // Centering: cell centres run from index 0 to index (count - 1), so the midpoint
        // of that range is (count - 1) / 2. Subtracting it puts the middle of the board at
        // this object's origin. Note the 0.5f rather than an integer 2: with 8 columns the
        // centre sits at 3.5, and integer division would truncate it to 3 and shift the
        // whole board half a cell off-centre.
        public Vector3 BoardPositionToLocalPosition(BoardPosition position)
        {
            float x = (position.Column - (board.Columns - 1) * 0.5f) * CellStride;
            float y = (position.Row - (board.Rows - 1) * 0.5f) * CellStride;
            return new Vector3(x, y, 0f);
        }

        // The inverse of BoardPositionToLocalPosition: a point in the world becomes the
        // grid coordinate it falls on. This is arithmetic, not a physics query -- see the
        // milestone notes for why a uniform grid does not need colliders.
        //
        // Returns false when the point lies outside the board, which is how "tapped the
        // background" is distinguished from "tapped a cell".
        public bool TryGetBoardPosition(Vector3 worldPosition, out BoardPosition position)
        {
            position = default;

            if (board == null)
            {
                return false;
            }

            // Into this object's local space first, so moving or scaling BoardRoot keeps working.
            Vector3 local = transform.InverseTransformPoint(worldPosition);

            // Reject anything beyond the board's visible rectangle BEFORE rounding.
            // Without this, rounding to nearest extends each edge cell's catchment half a
            // gap (spacing / 2) past the artwork, so a tap just outside the board would
            // snap back onto an edge block. Interior gaps are unaffected: they lie inside
            // this rectangle and still resolve to the nearest cell.
            if (Mathf.Abs(local.x) > HalfBoardWidth || Mathf.Abs(local.y) > HalfBoardHeight)
            {
                return false;
            }

            // Rounding to the nearest index means a tap landing in the gap between two
            // blocks resolves to the closer one, rather than being swallowed. On a phone
            // that forgiveness matters more than pixel-exactness.
            int column = Mathf.RoundToInt(local.x / CellStride + (board.Columns - 1) * 0.5f);
            int row = Mathf.RoundToInt(local.y / CellStride + (board.Rows - 1) * 0.5f);

            if (!board.IsInside(row, column))
            {
                return false;
            }

            position = new BoardPosition(row, column);
            return true;
        }

        // Exposed for verification and for later systems that need a specific view.
        public BlockView GetViewFor(Block block) =>
            viewsByBlock.TryGetValue(block, out BlockView view) ? view : null;

        public int ViewCount => viewsByBlock.Count;

        public int CrateViewCount => crateViewsByPosition.Count;

        private void CreateCrateViewFor(Obstacle obstacle, BoardPosition position)
        {
            CrateView view = Instantiate(cratePrefab, transform);
            view.Bind(obstacle, position);
            view.SetLocalPosition(BoardPositionToLocalPosition(position));
            view.name = $"Crate_r{position.Row}_c{position.Column}";

            crateViewsByPosition.Add(position, view);
        }

        // Inactive pooled GameObjects are children of this transform, so Unity destroys them
        // with it. Clearing the pool explicitly keeps that intent obvious and releases the
        // stack immediately rather than at scene teardown.
        private void OnDestroy()
        {
            viewPool?.Clear();
        }

        private static string ViewName(BoardPosition position) =>
            $"Block_r{position.Row}_c{position.Column}";

        // Lazily built, so it can be sized from the board once one exists.
        private ObjectPool<BlockView> Pool => viewPool ??= CreatePool();

        private ObjectPool<BlockView> CreatePool()
        {
            // A full board is the steady-state demand. Double it for headroom: during a move
            // the removed views are still animating out while the refill views are being
            // taken, so both generations are briefly alive at once.
            int cells = board != null ? board.Rows * board.Columns : DefaultPoolCapacity;

            return new ObjectPool<BlockView>(
                createFunc: InstantiateBlockView,
                actionOnGet: view => view.gameObject.SetActive(true),
                actionOnRelease: view =>
                {
                    view.PrepareForPool();
                    view.gameObject.SetActive(false);
                },
                actionOnDestroy: view =>
                {
                    if (view != null)
                    {
                        Destroy(view.gameObject);
                    }
                },
                // Fails loudly on a double release rather than silently corrupting the pool.
                collectionCheck: true,
                defaultCapacity: cells,
                maxSize: cells * 2);
        }

        // The ONLY place a BlockView GameObject is instantiated.
        private BlockView InstantiateBlockView()
        {
            TotalViewsCreated++;
            return Instantiate(blockPrefab, transform);
        }

        private void ReleaseView(BlockView view)
        {
            if (view != null)
            {
                Pool.Release(view);
            }
        }

        private BlockView CreateViewFor(Block block, BoardPosition position)
        {
            // Get returns a recycled view when one is available and only instantiates when
            // the pool is empty. Bind then overwrites every piece of visual state, so a view
            // that previously showed a bomb cannot bring anything of that life with it.
            BlockView view = Pool.Get();
            view.Bind(block, position);

            // The single place a new view's starting position is decided. Milestone 14 can
            // start it above the board and animate it down to here, and nothing outside this
            // class -- least of all RefillResolver -- needs to know.
            view.SetLocalPosition(BoardPositionToLocalPosition(position));

            // Naming generated objects after their coordinate makes the live hierarchy
            // readable while debugging.
            view.name = ViewName(position);

            viewsByBlock.Add(block, view);
            return view;
        }
    }
}
