namespace BlastPuzzle.Gameplay
{
    // Input is accepted only while WaitingForInput.
    public enum GameplayState
    {
        WaitingForInput,
        ResolvingMove,
        Won,
        Lost,
        Blocked
    }
}
