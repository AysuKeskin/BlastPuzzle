using UnityEngine.SceneManagement;

namespace BlastPuzzle.Core
{
    // Scene names in one place. Two scenes do not need a navigation framework.
    public static class SceneNavigator
    {
        public const string MainMenu = "MainMenu";
        public const string Gameplay = "Gameplay";

        public static void GoToMainMenu() => SceneManager.LoadScene(MainMenu);

        public static void GoToGameplay() => SceneManager.LoadScene(Gameplay);
    }
}
