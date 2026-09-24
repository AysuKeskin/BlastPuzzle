using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using BlastPuzzle.Levels;
using BlastPuzzle.Obstacles;
using BlastPuzzle.Presentation;
using UnityEngine;

namespace BlastPuzzle.Core
{
    // Composition root: reads the level asset, turns that CONFIGURATION into RUNTIME objects,
    // hands them to the systems that need them, and gets out of the way.
    //
    // This is the only class that knows a LevelDefinition exists. Everything downstream --
    // the board, the resolvers, the controller -- receives plain values, so none of them
    // depends on ScriptableObjects or could accidentally write back into the asset.
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField]
        private bool logStartup = true;

        [Tooltip("Which level this scene plays. Swap this asset to play a different level.")]
        [SerializeField]
        private LevelDefinition levelDefinition;

        [SerializeField]
        private BoardView boardView;

        [SerializeField]
        private GameplayController gameplayController;

        // Start rather than Awake: every component's Awake has already run by then, so the
        // BoardView and input handler are guaranteed initialised before they are used.
        private void Start()
        {
            if (levelDefinition == null)
            {
                Debug.LogError("GameBootstrap has no LevelDefinition assigned.", this);
                return;
            }

            // Fail here, naming the asset, rather than somewhere downstream with no context.
            levelDefinition.Validate();

            // One Random for the whole session, created here in the composition root and
            // shared with the controller, so there is a single source of randomness rather
            // than one per system or one per click. Deliberately NOT on the asset: a
            // ScriptableObject is configuration, not an RNG service.
            var random = new System.Random();

            // Dimensions come from the asset -- nothing here knows or cares that 8x8 was
            // ever a default.
            var board = new Board(levelDefinition.Rows, levelDefinition.Columns);

            // Crates go down FIRST, so the refill below sees their cells as occupied and
            // fills only around them. Placing them afterwards would mean either overwriting
            // blocks or teaching refill about obstacles -- neither is necessary.
            foreach (ObstaclePlacement placement in levelDefinition.Obstacles)
            {
                board.PlaceObstacle(
                    new BoardPosition(placement.Row, placement.Column),
                    new Obstacle(placement.Type));
            }

            // The board starts completely empty apart from the crates, and the REAL refill
            // system fills the rest. There is no separate "initial board" code path to drift
            // out of step with gameplay, and refill skips crate cells for free because they
            // are not empty.
            RefillResolver.ApplyRefill(board, levelDefinition.AvailableColors, random);

            // Configuration becomes fresh runtime progress. Every attempt gets new ColorGoal
            // objects starting at zero; the asset is only ever read.
            GoalTracker goals = levelDefinition.CreateRuntimeGoals();

            boardView.Build(board);
            gameplayController.Initialise(
                board,
                levelDefinition.AvailableColors,
                random,
                levelDefinition.MoveLimit,
                goals);

            if (logStartup)
            {
                Debug.Log($"Started {levelDefinition} | {boardView.ViewCount} block views"
                    + $", {boardView.CrateViewCount} crate views"
                    + $" | colours: {string.Join(", ", levelDefinition.AvailableColors)}");
            }
        }
    }
}
