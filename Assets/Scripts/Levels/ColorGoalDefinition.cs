using System;
using BlastPuzzle.Blocks;
using UnityEngine;

namespace BlastPuzzle.Levels
{
    // CONFIGURATION for one goal: "the player must destroy 10 Blue".
    //
    // Note what is absent: there is no CurrentCount. This type describes a requirement, it
    // never records a player's progress. That belongs to BlastPuzzle.Goals.ColorGoal, which
    // is constructed fresh from one of these each time a level attempt begins.
    //
    // The reason is not tidiness. A ScriptableObject asset is a SINGLE SHARED INSTANCE in
    // memory. Were progress stored here, it would survive a retry, so restarting a level
    // would resume mid-way -- and in the editor it would be written into the .asset file on
    // disk, meaning one playtest would permanently alter the level for everybody.
    //
    // A struct, not a class: this is a small immutable value. Unity serializes the private
    // fields so the Inspector can edit them, while code sees read-only properties.
    [Serializable]
    public struct ColorGoalDefinition
    {
        [SerializeField]
        private BlockColor color;

        [SerializeField]
        private int targetCount;

        public ColorGoalDefinition(BlockColor color, int targetCount)
        {
            this.color = color;
            this.targetCount = targetCount;
        }

        public BlockColor Color => color;

        public int TargetCount => targetCount;

        public override string ToString() => $"{color} x{targetCount}";
    }
}
