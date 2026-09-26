using BlastPuzzle.Core;
using BlastPuzzle.Presentation;
using UnityEngine;

namespace BlastPuzzle.UI
{
    // The in-game settings panel: sound, haptics, and the way back to the main menu.
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField]
        private SettingsPanelView settingsPrefab;

        private SettingsPanelView panel;

        [Tooltip("Owns the saved settings, so a toggle here survives leaving the level.")]
        [SerializeField]
        private GameFlowController flow;

        [SerializeField]
        private BoardInputHandler boardInput;

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        private void Awake()
        {
            panel = Instantiate(settingsPrefab, transform);
            panel.Configure(ToggleSound, ToggleHaptics, GoToMainMenu, Close, "RESUME");
        }

        public void Open()
        {
            Refresh();
            boardInput.InputBlocked = true;
            panel.Show();
        }

        public void Close()
        {
            panel.Hide();
            boardInput.InputBlocked = false;
        }

        private void OnDisable()
        {
            if (boardInput != null) boardInput.InputBlocked = false;
        }

        public void ToggleSound()
        {
            flow.SetSoundEnabled(!flow.SoundEnabled);
            Refresh();
        }

        public void ToggleHaptics()
        {
            flow.SetHapticsEnabled(!flow.HapticsEnabled);
            Refresh();
        }

        // Leaving abandons the attempt. GameFlowController retries pending progress writes
        // when the gameplay scene is disabled.
        public void GoToMainMenu()
        {
            if (flow.FlushProgress()) SceneNavigator.GoToMainMenu();
        }

        private void Refresh()
        {
            panel.Refresh(flow.SoundEnabled, flow.HapticsEnabled, "MAIN MENU");
        }
    }
}
