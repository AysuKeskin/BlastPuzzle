using UnityEngine;

namespace BlastPuzzle.Core
{
    public static class MobileRuntimeSettings
    {
        // A mobile rendering budget, not a measured performance guarantee.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (Application.isMobilePlatform) Application.targetFrameRate = 60;
        }
    }
}
