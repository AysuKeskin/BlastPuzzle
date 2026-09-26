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

        [Header("Camera framing")]
        [SerializeField] private bool fitCameraToBoard = true;
        [SerializeField] private Camera boardCamera;
        [Tooltip("Normalized screen area reserved for the board, leaving room for the HUD.")]
        [SerializeField] private Rect boardViewport = new Rect(0.04f, 0.12f, 0.92f, 0.64f);
        private Vector2 lastScreenSize;
        private Rect lastSafeArea;
        private RectTransform hudBottomBoundary;
        private Canvas hudCanvas;
        private float lastHudBottom = 1f;
        private readonly Vector3[] hudCorners = new Vector3[4];

        public void SetHudBoundary(RectTransform boundary)
        {
            hudBottomBoundary = boundary;
            hudCanvas = boundary != null ? boundary.GetComponentInParent<Canvas>() : null;
            RefreshCamera();
        }

        private float HudBottom()
        {
            if (hudBottomBoundary == null || hudCanvas == null || Screen.height <= 0) return 1f;
            hudBottomBoundary.GetWorldCorners(hudCorners);
            Camera uiCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(uiCamera, hudCorners[0]).y / Screen.height - 0.025f;
        }
        private readonly Dictionary<BlockView, Coroutine> selectionFeedback =
            new Dictionary<BlockView, Coroutine>();

        private void LateUpdate()
        {
            if (lastScreenSize != new Vector2(Screen.width, Screen.height) || lastSafeArea != Screen.safeArea ||
                !Mathf.Approximately(lastHudBottom, HudBottom())) RefreshCamera();
        }

        private void RefreshCamera()
        {
            lastScreenSize = new Vector2(Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;
            if (!fitCameraToBoard || board == null) return;
            if (boardCamera == null) boardCamera = Camera.main;
            if (boardCamera == null || Screen.width <= 0 || Screen.height <= 0) return;

            Canvas.ForceUpdateCanvases();
            lastHudBottom = HudBottom();
            Rect safe = Screen.safeArea;
            Rect area = Rect.MinMaxRect(
                Mathf.Max(boardViewport.xMin, safe.xMin / Screen.width),
                Mathf.Max(boardViewport.yMin, safe.yMin / Screen.height),
                Mathf.Min(boardViewport.xMax, safe.xMax / Screen.width),
                Mathf.Min(boardViewport.yMax, safe.yMax / Screen.height, lastHudBottom));
            FitCamera(boardCamera, area);
        }

        // The gameplay camera is orthographic and uses a full-screen viewport.
        public void FitCamera(Camera camera, Rect area)
        {
            if (board == null || camera == null || !camera.orthographic ||
                area.width <= 0f || area.height <= 0f) return;

            Vector3 center = camera.transform.InverseTransformPoint(transform.position);
            float halfWidth = 0f;
            float halfHeight = 0f;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                {
                    Vector3 corner = camera.transform.InverseTransformPoint(transform.TransformPoint(
                        new Vector3(x * HalfBoardWidth, y * HalfBoardHeight, 0f)));
                    halfWidth = Mathf.Max(halfWidth, Mathf.Abs(corner.x - center.x));
                    halfHeight = Mathf.Max(halfHeight, Mathf.Abs(corner.y - center.y));
                }

            const float padding = 0.25f;
            camera.orthographicSize = Mathf.Max(
                (halfHeight + padding) / area.height,
                (halfWidth + padding) / (camera.aspect * area.width));
            Vector2 offset = area.center - new Vector2(0.5f, 0.5f);
            float height = 2f * camera.orthographicSize;
            camera.transform.position += camera.transform.right * (center.x - offset.x * height * camera.aspect)
                + camera.transform.up * (center.y - offset.y * height);
        }

        public void ShowInvalidSelection(Block block)
        {
            BlockView view = GetViewFor(block);
            if (view == null || selectionFeedback.ContainsKey(view)) return;
            selectionFeedback.Add(view, StartCoroutine(ShakeSelection(view, block)));
        }

        private IEnumerator ShakeSelection(BlockView view, Block block)
        {
            const float duration = 0.18f;
            float elapsed = 0f;
            while (elapsed < duration && view != null && ReferenceEquals(view.Block, block))
            {
                float t = elapsed / duration;
                view.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Sin(t * Mathf.PI * 6f) * 9f * (1f - t));
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (view != null && ReferenceEquals(view.Block, block))
                view.transform.localRotation = Quaternion.identity;
            selectionFeedback.Remove(view);
        }

        public void CancelSelectionFeedback()
        {
            foreach (var pair in selectionFeedback)
            {
                StopCoroutine(pair.Value);
                if (pair.Key != null) pair.Key.transform.localRotation = Quaternion.identity;
            }
            selectionFeedback.Clear();
        }

        // --- animation timing ------------------------------------------------------------
        [Header("Animation timing (seconds)")]
        [SerializeField] private float removalDuration = 0.14f;
        [SerializeField] private float crateRemovalDuration = 0.16f;
        [SerializeField] private float gravityBaseDuration = 0.12f;
        [SerializeField] private float gravityPerCellDuration = 0.035f;
        [SerializeField] private float refillBaseDuration = 0.14f;
        [SerializeField] private float refillPerCellDuration = 0.035f;
        [SerializeField] private float powerUpPopDuration = 0.18f;
        [SerializeField] private float activationDuration = 0.12f;
        [SerializeField] private float shuffleBaseDuration = 0.18f;
        [SerializeField] private float shufflePerCellDuration = 0.02f;

        [Tooltip("Optional. Cosmetic effects; the board works without it.")]
        [SerializeField] private GameplayVFX vfx;

        private readonly List<Vector3> vfxPositions = new List<Vector3>();
        private readonly List<BlockColor> vfxColors = new List<BlockColor>();
        private ObjectPool<BlockView> viewPool;
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
            public ViewTravel(BlockView view, Vector3 from, Vector3 to, float duration, bool appear = false)
            {
                View = view;
                From = from;
                To = to;
                Duration = duration;
                Appear = appear;
            }

            public BlockView View { get; }
            public Vector3 From { get; }
            public Vector3 To { get; }
            public float Duration { get; }
            public bool Appear { get; }
        }
        private readonly Dictionary<Block, BlockView> viewsByBlock = new Dictionary<Block, BlockView>();
        private readonly Dictionary<BoardPosition, CrateView> crateViewsByPosition =
            new Dictionary<BoardPosition, CrateView>();

        private Board board;
        private Sprite boardAreaSprite;
        private Transform boardArea;
        private Transform boardFrame;
        private Transform boardBackdrop;

        private void ConfigureBoardArea()
        {
            if (boardArea == null)
            {
                boardAreaSprite = Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
                var maskObject = new GameObject("BoardClip", typeof(SpriteMask));
                boardArea = maskObject.transform;
                boardArea.SetParent(transform, false);
                maskObject.GetComponent<SpriteMask>().sprite = boardAreaSprite;
                boardFrame = CreateBoardSurface("BoardFrame", new Color(.30f, .42f, .55f), -12);
                boardBackdrop = CreateBoardSurface("BoardBackdrop", new Color(.035f, .075f, .12f), -11);
            }
            boardArea.gameObject.SetActive(true);
            boardFrame.gameObject.SetActive(true);
            boardBackdrop.gameObject.SetActive(true);
            boardArea.localScale = new Vector3(HalfBoardWidth * 2f, HalfBoardHeight * 2f, 1f);
            boardFrame.localScale = new Vector3(HalfBoardWidth * 2f + .18f, HalfBoardHeight * 2f + .18f, 1f);
            boardBackdrop.localScale = new Vector3(HalfBoardWidth * 2f + .08f, HalfBoardHeight * 2f + .08f, 1f);
        }

        private Transform CreateBoardSurface(string objectName, Color color, int order)
        {
            var surface = new GameObject(objectName, typeof(SpriteRenderer));
            surface.transform.SetParent(transform, false);
            var renderer = surface.GetComponent<SpriteRenderer>();
            renderer.sprite = boardAreaSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return surface.transform;
        }

        // Distance from one cell's centre to the next, including the gap between them.
        private float CellStride => cellSize + spacing;
        private float HalfBoardWidth => (board.Columns * CellStride - spacing) * 0.5f;

        private float HalfBoardHeight => (board.Rows * CellStride - spacing) * 0.5f;

        // Rebuild views for a new attempt and fit its board into the available screen area.
        public void Build(Board boardToRender)
        {
            Clear();
            board = boardToRender;
            ConfigureBoardArea();
            RefreshCamera();

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
                    if (!cell.HasBlock)
                    {
                        continue;
                    }

                    CreateViewFor(cell.Block, cell.Position);
                }
            }
        }

        private sealed class SettleRoute
        {
            public BlockView View;
            public readonly List<Vector3> Points = new List<Vector3>();
            public int Segment;
            public float SegmentStart;
            public bool Appear;
        }

        private readonly Dictionary<Block, SettleRoute> settleRoutes = new Dictionary<Block, SettleRoute>();
        private readonly List<SettleRoute> settleRoutePool = new List<SettleRoute>();
        private int[] settleEntries;
        private int usedSettleRoutes;

        public void BeginSettlePlan()
        {
            settleRoutes.Clear();
            usedSettleRoutes = 0;
            if (settleEntries == null || settleEntries.Length != board.Columns)
                settleEntries = new int[board.Columns];
            System.Array.Clear(settleEntries, 0, settleEntries.Length);
        }

        private SettleRoute GetSettleRoute(Block block, BlockView view)
        {
            if (settleRoutes.TryGetValue(block, out var existing)) return existing;
            if (usedSettleRoutes == settleRoutePool.Count) settleRoutePool.Add(new SettleRoute());
            var route = settleRoutePool[usedSettleRoutes++];
            route.View = view;
            route.Points.Clear();
            route.Points.Add(view.transform.localPosition);
            route.Segment = 0;
            route.SegmentStart = 0f;
            route.Appear = false;
            settleRoutes.Add(block, route);
            return route;
        }

        public void PlanSettleMoves(IReadOnlyList<BlockMove> moves)
        {
            foreach (var move in moves)
            {
                var view = GetViewFor(move.Block);
                if (view == null) continue;
                var route = GetSettleRoute(move.Block, view);
                route.Points.Add(BoardPositionToLocalPosition(move.To));
                view.SetBoardPosition(move.To);
                view.name = ViewName(move.To);
            }
        }

        public void PlanSettleSpawns(IReadOnlyList<BlockSpawn> spawns)
        {
            foreach (var spawn in spawns)
            {
                var view = CreateViewFor(spawn.Block, spawn.Position);
                bool enclosed = HasObstacleAbove(spawn.Position);
                Vector3 target = BoardPositionToLocalPosition(spawn.Position);
                // All incoming waves share one queue per column, keeping their order
                // and spacing while they enter together behind the board mask.
                var entry = new BoardPosition(board.Rows + settleEntries[spawn.Position.Column], spawn.Position.Column);
                view.SetLocalPosition(enclosed ? target : BoardPositionToLocalPosition(entry));
                var route = GetSettleRoute(spawn.Block, view);
                route.Points.Add(target);
                route.Appear = enclosed;
                if (enclosed) view.SetAlpha(0f);
                else settleEntries[spawn.Position.Column]++;
            }
        }

        public IEnumerator AnimateSettlePlan()
        {
            float elapsed = 0f;
            // Shared speed/acceleration preserves spacing. No easing or waiting at
            // intermediate vertical/diagonal waypoints: only the final cell stops a block.
            float speed = CellStride / Mathf.Max(.065f, gravityPerCellDuration);
            const float accelerationTime = .06f;
            bool moving;
            do
            {
                elapsed += Time.deltaTime;
                float distance = speed * (elapsed - accelerationTime * (1f - Mathf.Exp(-elapsed / accelerationTime)));
                moving = false;
                for (int i = 0; i < usedSettleRoutes; i++)
                {
                    var route = settleRoutePool[i];
                    while (route.Segment < route.Points.Count - 1)
                    {
                        float length = Vector3.Distance(route.Points[route.Segment], route.Points[route.Segment + 1]);
                        float remaining = distance - route.SegmentStart;
                        if (remaining < length)
                        {
                            route.View.SetLocalPosition(Vector3.Lerp(route.Points[route.Segment],
                                route.Points[route.Segment + 1], remaining / length));
                            moving = true;
                            break;
                        }
                        route.SegmentStart += length;
                        route.Segment++;
                        route.View.SetLocalPosition(route.Points[route.Segment]);
                    }
                    if (route.Appear)
                    {
                        float alpha = Mathf.Clamp01(elapsed / Mathf.Max(.01f, refillBaseDuration));
                        route.View.SetAlpha(alpha);
                        moving |= alpha < 1f;
                    }
                }
                if (moving) yield return null;
            } while (moving);
            settleRoutes.Clear();
        }

        // Blocks falling into the cells gravity moved them to.
        public IEnumerator AnimateMoves(IReadOnlyList<BlockMove> moves) =>
            AnimateBlockMoves(moves, gravityBaseDuration, gravityPerCellDuration);

        // Blocks sliding to new cells after a deadlock was cleared.
        public IEnumerator AnimateShuffle(IReadOnlyList<BlockMove> moves) =>
            AnimateBlockMoves(moves, shuffleBaseDuration, shufflePerCellDuration);

        // The one place existing views are repositioned to match the board.
        private IEnumerator AnimateBlockMoves(
            IReadOnlyList<BlockMove> moves,
            float baseDuration,
            float perCellDuration)
        {
            travels.Clear();
            float longest = 0f;

            foreach (BlockMove move in moves)
            {
                if (!viewsByBlock.TryGetValue(move.Block, out BlockView view) || view == null)
                {
                    continue;
                }
                view.SetBoardPosition(move.To);
                view.name = ViewName(move.To);

                Vector3 from = view.transform.localPosition;
                Vector3 to = BoardPositionToLocalPosition(move.To);
                int distance = Mathf.Abs(move.From.Row - move.To.Row)
                    + Mathf.Abs(move.From.Column - move.To.Column);

                float duration = baseDuration + distance * perCellDuration;

                travels.Add(new ViewTravel(view, from, to, duration));
                longest = Mathf.Max(longest, duration);
            }

            yield return RunTravels(longest);
        }

        // Open columns refill from above; cells beneath a crate fade in at their destination.
        public IEnumerator AnimateSpawns(IReadOnlyList<BlockSpawn> spawns)
        {
            travels.Clear();
            float longest = 0f;
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
                var entry = new BoardPosition(board.Rows + indexInColumn, spawn.Position.Column);
                Vector3 to = BoardPositionToLocalPosition(spawn.Position);
                bool covered = HasObstacleAbove(spawn.Position);
                Vector3 from = covered ? to : BoardPositionToLocalPosition(entry);
                view.SetLocalPosition(from);
                if (covered)
                {
                    view.SetScale(0.75f);
                    view.SetAlpha(0f);
                }

                float distance = entry.Row - spawn.Position.Row;
                float duration = covered ? refillBaseDuration : refillBaseDuration + distance * refillPerCellDuration;

                travels.Add(new ViewTravel(view, from, to, duration, covered));
                longest = Mathf.Max(longest, duration);
                if (!covered) indexInColumn++;
            }

            yield return RunTravels(longest);
        }

        private bool HasObstacleAbove(BoardPosition position)
        {
            for (int row = position.Row + 1; row < board.Rows; row++)
                if (board.GetCell(row, position.Column).HasObstacle) return true;
            return false;
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
                    float t = travel.Duration <= 0f ? 1f : Mathf.Clamp01(elapsed / travel.Duration);
                    travel.View.SetLocalPosition(Vector3.Lerp(travel.From, travel.To, EaseOutCubic(t)));
                    if (travel.Appear)
                    {
                        travel.View.SetScale(Mathf.Lerp(0.75f, 1f, EaseOutCubic(t)));
                        travel.View.SetAlpha(t);
                    }
                }

                yield return null;
            }
            for (int i = 0; i < travels.Count; i++)
            {
                travels[i].View.SetLocalPosition(travels[i].To);
                if (travels[i].Appear) travels[i].View.ResetAppearance();
            }

            travels.Clear();
        }
        private static float EaseOutCubic(float t)
        {
            float inverted = 1f - t;
            return 1f - inverted * inverted * inverted;
        }

        // Creates one view per newly spawned Block.
        public void AddViews(IEnumerable<BlockSpawn> spawns)
        {
            foreach (BlockSpawn spawn in spawns)
            {
                CreateViewFor(spawn.Block, spawn.Position);
            }
        }

        // Animates the destruction of blocks and crates TOGETHER, then destroys their views.
        public IEnumerator AnimateDestruction(
            IReadOnlyList<Block> blocks,
            IReadOnlyList<ObstacleRemoval> obstacles)
        {
            dyingBlocks.Clear();
            dyingCrates.Clear();
            vfxPositions.Clear();
            vfxColors.Clear();

            foreach (Block block in blocks)
            {
                if (!viewsByBlock.TryGetValue(block, out BlockView view))
                {
                    continue;
                }

                // Mapping dropped before the release, so a dead block can never resolve
                // to a view that now belongs to a live one.
                viewsByBlock.Remove(block);

                if (view != null)
                {
                    dyingBlocks.Add(view);

                    // Position and colour are collected HERE, where the block and its view
                    // are both in hand. Pairing them afterwards by index was wrong: blocks
                    // without a view are skipped above, so the lists drifted apart and a
                    // blue block could be given the next block's colour.
                    vfxPositions.Add(view.transform.position);
                    vfxColors.Add(block.Color);
                }
            }

            if (vfx != null)
            {
                vfx.PlayBlockBurst(vfxPositions, vfxColors);

                vfxPositions.Clear();

                foreach (ObstacleRemoval removal in obstacles)
                {
                    vfxPositions.Add(transform.TransformPoint(BoardPositionToLocalPosition(removal.Position)));
                }

                vfx.PlayCrateBreak(vfxPositions);
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
            // Released only now, after the animation: releasing earlier would let refill
            // hand the same object out while it was still fading.
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
        public IEnumerator AnimatePowerUpCreation(BlockSpawn spawn)
        {
            BlockView view = CreateViewFor(spawn.Block, spawn.Position);
            view.SetLocalPosition(BoardPositionToLocalPosition(spawn.Position));
            vfx?.PlayPowerUpCreated(view.transform.position);

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
        public IEnumerator AnimateActivation(Block powerUp)
        {
            if (!viewsByBlock.TryGetValue(powerUp, out BlockView view) || view == null)
            {
                yield break;
            }

            // Every fired power-up, including chain reactions, gets its own effect.
            if (vfx != null)
            {
                if (powerUp.Kind == BlockKind.Rocket)
                {
                    vfx.PlayRocketStreak(view.transform.position, powerUp.Direction);
                }
                else if (powerUp.Kind == BlockKind.Bomb)
                {
                    vfx.PlayBombBlast(view.transform.position);
                }
            }

            float duration = vfx != null ? Mathf.Max(activationDuration, vfx.ActivationDuration(powerUp.Kind)) : activationDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

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
                crateViewsByPosition.Remove(removal.Position);

                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }
        }
        public void RemoveViews(IEnumerable<Block> blocks)
        {
            foreach (Block block in blocks)
            {
                if (!viewsByBlock.TryGetValue(block, out BlockView view))
                {
                    continue;
                }
                viewsByBlock.Remove(block);

                ReleaseView(view);
            }
        }

        // Returns every view this component created to the pool and forgets them.
        public void Clear()
        {
            CancelSelectionFeedback();
            settleRoutes.Clear();
            foreach (var route in settleRoutePool)
            {
                route.View = null;
                route.Points.Clear();
            }
            usedSettleRoutes = 0;
            releaseBuffer.Clear();
            releaseBuffer.AddRange(viewsByBlock.Values);
            viewsByBlock.Clear();

            foreach (BlockView view in releaseBuffer)
            {
                ReleaseView(view);
            }

            releaseBuffer.Clear();
            foreach (CrateView view in crateViewsByPosition.Values)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            crateViewsByPosition.Clear();
            board = null;
            if (boardArea != null) boardArea.gameObject.SetActive(false);
            if (boardFrame != null) boardFrame.gameObject.SetActive(false);
            if (boardBackdrop != null) boardBackdrop.gameObject.SetActive(false);
        }

        // Turns a logical grid coordinate into a local offset from this object's origin.
        public Vector3 BoardPositionToLocalPosition(BoardPosition position)
        {
            float x = (position.Column - (board.Columns - 1) * 0.5f) * CellStride;
            float y = (position.Row - (board.Rows - 1) * 0.5f) * CellStride;
            return new Vector3(x, y, 0f);
        }
        public bool TryGetBoardPosition(Vector3 worldPosition, out BoardPosition position)
        {
            position = default;

            if (board == null)
            {
                return false;
            }

            // Into this object's local space first, so moving or scaling BoardRoot keeps working.
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            if (Mathf.Abs(local.x) > HalfBoardWidth || Mathf.Abs(local.y) > HalfBoardHeight)
            {
                return false;
            }
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
        private void OnDestroy()
        {
            viewPool?.Clear();
            if (boardAreaSprite != null) Destroy(boardAreaSprite);
        }

        private static string ViewName(BoardPosition position) =>
            $"Block_r{position.Row}_c{position.Column}";

        // Lazily built, so it can be sized from the board once one exists.
        private ObjectPool<BlockView> Pool => viewPool ??= CreatePool();

        private ObjectPool<BlockView> CreatePool()
        {
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
            BlockView view = Pool.Get();
            view.Bind(block, position);

            // Spawn animations may override this resting position.
            view.SetLocalPosition(BoardPositionToLocalPosition(position));
            view.name = ViewName(position);

            viewsByBlock.Add(block, view);
            return view;
        }
    }
}
