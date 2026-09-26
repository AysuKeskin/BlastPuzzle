using System;
using BlastPuzzle.Blocks;
using UnityEngine;

namespace BlastPuzzle.Levels
{
    [Serializable]
    // A colour goal as authored: which colour, how many.
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
