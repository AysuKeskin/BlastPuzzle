namespace BlastPuzzle.Gameplay
{
    // What the gameplay layer is currently doing, and therefore whether a player
    // selection may start a move.
    //
    // Values are added in the milestone that gives them behaviour. Won and Lost arrive
    // here now that moves and goals exist, so both are genuinely reachable and testable.
    // Paused and Animating are still absent for the same reason they were before: nothing
    // can currently enter them.
    //
    // FUTURE (Milestone 14, not implemented now): once removal, falling and refill are
    // animated, ResolvingMove will span those animations rather than returning within a
    // single synchronous call. If waiting on animation turns out to need distinguishing
    // from resolving logic, an Animating value gets added then -- with the behaviour.
    public enum GameplayState
    {
        WaitingForInput,
        ResolvingMove,

        // Terminal. Both are rejected by the input guard for free, because it only accepts
        // WaitingForInput -- no extra check is needed to stop play after the level ends.
        Won,
        Lost
    }
}
