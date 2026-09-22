using UnityEngine;

namespace BlastPuzzle.Core
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField]
        private bool logStartup = true;

        private void Awake()
        {
            if (logStartup)
            {
                Debug.Log("Blast Puzzle started successfully.");
            }
        }
    }
}