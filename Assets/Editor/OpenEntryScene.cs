using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Opens the entry scene when the Editor starts with nothing loaded.
//
// Unity remembers the last open scene in Library/LastSceneManagerSetup.txt, but any
// headless batch run (tests, asset scripts) ends with no scene open and overwrites that
// file, so the next launch lands on an empty untitled scene.
public static class OpenEntryScene
{
    private const string EntryScene = "Assets/Scenes/MainMenu.unity";
    private const string RanThisSessionKey = "BlastPuzzle.OpenedEntryScene";

    [InitializeOnLoadMethod]
    private static void OpenOnLaunch()
    {
        // Batch runs are supposed to end with no scene open; only steer the real Editor.
        if (Application.isBatchMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= HandlePlayMode;
        EditorApplication.playModeStateChanged += HandlePlayMode;

        // Play always starts at the menu, even while editing the gameplay scene.
        // This does not replace the scene currently being edited.
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.playModeStartScene =
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(EntryScene);
            }
        };

        // InitializeOnLoad also fires on every script recompile. SessionState lives for one
        // Editor process, so this runs at launch and never yanks the scene out from under
        // someone mid-session.
        if (SessionState.GetBool(RanThisSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(RanThisSessionKey, true);

        EditorApplication.delayCall += () =>
        {
            var scene = EditorSceneManager.GetActiveScene();

            // A real scene is already open, or it has unsaved work: leave it alone.
            if (!string.IsNullOrEmpty(scene.path) || scene.isDirty)
            {
                return;
            }

            if (System.IO.File.Exists(EntryScene))
            {
                EditorSceneManager.OpenScene(EntryScene);
            }
        };
    }
    private static void HandlePlayMode(PlayModeStateChange state)
    {
        // Unity Test Runner creates an InitTestScene containing its runner. Loading
        // the menu instead would discard that runner and leave the suite hanging.
        if (state == PlayModeStateChange.ExitingEditMode &&
            EditorSceneManager.GetActiveScene().name.StartsWith("InitTestScene", System.StringComparison.Ordinal))
            EditorSceneManager.playModeStartScene = null;
        else if (state == PlayModeStateChange.EnteredEditMode && !Application.isBatchMode)
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(EntryScene);
    }

}
