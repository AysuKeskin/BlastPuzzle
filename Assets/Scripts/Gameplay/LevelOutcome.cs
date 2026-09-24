namespace BlastPuzzle.Gameplay
{
    // Decides what the level becomes once a move has fully resolved.
    //
    // A pure function of two facts, extracted from the controller so the ordering rule below
    // can be tested directly rather than only through a wired scene.
    public static class LevelOutcome
    {
        // ORDER MATTERS. Goals are checked BEFORE the move count.
        //
        // On the last move a player can finish the final goal and drop to zero moves at the
        // same instant. Both conditions are then true, and checking moves first would call
        // that a loss -- punishing the player for winning on the buzzer. Winning takes
        // precedence: running out of moves only loses when there was still something left
        // to achieve.
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
