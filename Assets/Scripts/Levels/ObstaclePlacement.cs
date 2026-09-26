using System;
using BlastPuzzle.Obstacles;
using UnityEngine;

namespace BlastPuzzle.Levels
{
    [Serializable]
    // Where a level puts a crate.
    public struct ObstaclePlacement
    {
        [SerializeField]
        private int row;

        [SerializeField]
        private int column;

        [SerializeField]
        private ObstacleType type;

        public ObstaclePlacement(int row, int column, ObstacleType type)
        {
            this.row = row;
            this.column = column;
            this.type = type;
        }

        public int Row => row;

        public int Column => column;

        public ObstacleType Type => type;

        public override string ToString() => $"{type}@r{row}c{column}";
    }
}
