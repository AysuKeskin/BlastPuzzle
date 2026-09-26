using System;

namespace BlastPuzzle.Persistence
{
    [Serializable]
    // What is saved to disk: how far the player has unlocked.
    public sealed class PlayerProgressData
    {
        public int SaveVersion;
        public int HighestUnlockedLevelIndex;

        // Settings live here so they survive the trip between the menu and gameplay.
        // Defaults are on, and an older save simply leaves them at those values.
        public bool SoundEnabled = true;
        public bool HapticsEnabled = true;
    }
}
