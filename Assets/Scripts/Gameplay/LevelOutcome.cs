namespace BlastPuzzle.Gameplay
{
    // Decides won, lost, or carry on. Goals are checked before the move count.
    public static class LevelOutcome
    {
        public static GameplayState Evaluate(bool allGoalsComplete, int movesRemaining)
        {
            if (allGoalsComplete)
            {
                return GameplayState.Won;
            }

            if (movesRemaining <= 0)
            {
                return GameplayState.Lost;
            }

            return GameplayState.WaitingForInput;
        }
    }
}
