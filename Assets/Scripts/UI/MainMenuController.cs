using System.IO;
using BlastPuzzle.Core;
using BlastPuzzle.Persistence;
using TMPro;
using UnityEngine;

namespace BlastPuzzle.UI
{
    // The entry screen owns menu settings and progress reset.
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text playLabel;

        [SerializeField]
        private TMP_Text progressLabel;

        [SerializeField]
        private GameObject quitButton;

        [Header("Settings")]
        [SerializeField] private SettingsPanelView settingsPrefab;
        private SettingsPanelView settingsPanel;

        [SerializeField]
        private string playText = "PLAY";

        [SerializeField]
        private string continueText = "CONTINUE";

        private PlayerProgressData progress;
        private SaveService service;
        private bool progressDirty;
        private float nextSaveAttempt;

        public bool HasPendingSave => progressDirty;

        public bool FlushProgress()
        {
            if (!progressDirty || service == null) return true;
            progressDirty = !service.Save(progress);
            nextSaveAttempt = Time.unscaledTime + 5f;
            return !progressDirty;
        }

        private void Update()
        {
            if (progressDirty && Time.unscaledTime >= nextSaveAttempt) FlushProgress();
        }

        private void OnApplicationPause(bool paused) { if (paused) FlushProgress(); }
        private void OnApplicationFocus(bool focused) { if (!focused) FlushProgress(); }
        private void OnApplicationQuit() => FlushProgress();
        private void OnDisable() => FlushProgress();

        // Wiping progress is destructive, so the button asks once before doing it.
        private bool resetArmed;

        public int HighestUnlockedLevelIndex => progress?.HighestUnlockedLevelIndex ?? 0;

        public int CurrentLevelIndex => progress?.CurrentLevelIndex ?? 0;

        private void Start()
        {
            // Gameplay advances progress; the menu can change settings or reset it.
            service = new SaveService(
                Path.Combine(Application.persistentDataPath, SaveService.FileName));

            progress = service.LoadOrCreate();

            settingsPanel = Instantiate(settingsPrefab, transform);
            settingsPanel.Configure(ToggleSound, ToggleHaptics, ResetProgress, CloseSettings, "CLOSE");

            Refresh();
        }

        // Test seam: lets a test supply progress instead of reading the real save.
        public void Initialize(PlayerProgressData loadedProgress)
        {
            progress = loadedProgress;
            Refresh();
        }

        private void Refresh()
        {
            bool started = HighestUnlockedLevelIndex > 0 || CurrentLevelIndex > 0;

            playLabel.text = started ? continueText : playText;

            if (progressLabel != null)
            {
                progressLabel.gameObject.SetActive(true);
                progressLabel.text = $"Level {CurrentLevelIndex + 1}";
            }

            if (settingsPanel != null)
                settingsPanel.Refresh(progress.SoundEnabled, progress.HapticsEnabled,
                    resetArmed ? "TAP AGAIN TO CONFIRM" : "RESET TO LEVEL 1");

            if (quitButton != null)
            {
                // Quitting is meaningless on the web and on iOS, where the platform owns it.
                quitButton.SetActive(Application.platform != RuntimePlatform.IPhonePlayer
                    && Application.platform != RuntimePlatform.WebGLPlayer);
            }
        }

        // Gameplay decides which level to start; this only opens the scene.
        public void OpenSettings()
        {
            resetArmed = false;
            Refresh();
            settingsPanel.Show();
        }

        public void CloseSettings()
        {
            resetArmed = false;
            settingsPanel.Hide();
        }

        public void ToggleSound()
        {
            progress.SoundEnabled = !progress.SoundEnabled;
            Save();
        }

        public void ToggleHaptics()
        {
            progress.HapticsEnabled = !progress.HapticsEnabled;
            Save();
        }

        // First press arms, second press wipes. Nothing is lost on a stray tap.
        public void ResetProgress()
        {
            if (!resetArmed)
            {
                resetArmed = true;
                Refresh();
                return;
            }

            progress.HighestUnlockedLevelIndex = 0;
            progress.CurrentLevelIndex = 0;
            resetArmed = false;
            Save();
        }

        private void Save()
        {
            progressDirty = true;
            FlushProgress();
            Refresh();
        }

        public void Play()
        {
            // Keep the pending changes alive if the disk is temporarily unavailable.
            if (FlushProgress()) SceneNavigator.GoToGameplay();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
