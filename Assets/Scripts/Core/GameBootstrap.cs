using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using BlastPuzzle.Levels;
using BlastPuzzle.Obstacles;
using BlastPuzzle.Presentation;
using UnityEngine;

namespace BlastPuzzle.Core
{
    // Turns a level asset into a fresh set of runtime objects.
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField]
        private bool logStartup = true;

        [SerializeField]
        private BoardView boardView;

        [SerializeField]
        private GameplayController gameplayController;

        // One Random for the whole session. A fresh one per attempt would be seeded from
        // the clock, so a quick retry could deal the very same board again.
        private readonly System.Random random = new System.Random();

        // Turns one LevelDefinition into a complete, fresh runtime attempt.
        public void StartLevel(LevelDefinition levelDefinition)
        {
            if (levelDefinition == null)
            {
                Debug.LogError("GameBootstrap was asked to start a null LevelDefinition.", this);
                return;
            }

            // Fail here, naming the asset, rather than somewhere downstream with no context.
            levelDefinition.Validate();
            var board = new Board(levelDefinition.Rows, levelDefinition.Columns);
            foreach (ObstaclePlacement placement in levelDefinition.Obstacles)
            {
                board.PlaceObstacle(
                    new BoardPosition(placement.Row, placement.Column),
                    new Obstacle(placement.Type));
            }
            RefillResolver.ApplyRefill(board, levelDefinition.AvailableColors, random);
            ShuffleResult shuffle = BoardShuffleResolver.Shuffle(
                board, gameplayController.MinimumGroupSize, random);

            if (shuffle.WasNeeded && !shuffle.Succeeded)
            {
                Debug.LogWarning(
                    $"'{levelDefinition.name}' could not be shuffled ({shuffle}); retry is available.", this);
            }
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
