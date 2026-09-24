using System;
using BlastPuzzle.Obstacles;
using UnityEngine;

namespace BlastPuzzle.Levels
{
    // CONFIGURATION for one obstacle: "put a Crate at row 3, column 4".
    //
    // Deliberately plain coordinates rather than a tile-map or a grid-painting format. With
    // a handful of crates per level a short list is readable in the Inspector and diffs
    // cleanly in version control; a richer authoring format is worth designing when levels
    // actually need one.
    [Serializable]
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
