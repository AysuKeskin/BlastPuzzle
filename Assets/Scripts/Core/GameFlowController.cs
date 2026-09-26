using System;
using System.IO;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Levels;
using BlastPuzzle.Persistence;
using BlastPuzzle.Presentation;
using UnityEngine;

namespace BlastPuzzle.Core
{
    // Decides which level runs, and remembers what the player has unlocked.
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField]
        private GameBootstrap bootstrap;

        [SerializeField]
        private GameplayController gameplay;

        [Tooltip("Optional. Settings are pushed into it when a session starts.")]
        [SerializeField]
        private GameFeedback feedback;

        [Tooltip("The levels of this session, in the order they are played.")]
        [SerializeField]
        private LevelDefinition[] levels;

        private PlayerProgressData progress;
        private SaveService saveService;
        private bool progressDirty;
        private float nextSaveAttempt;
        private const float SaveRetryInterval = 5f;

        public bool HasPendingSave => progressDirty;

        // Failed writes remain pending, including when replaying an already unlocked level.
        public bool FlushProgress()
        {
            if (!progressDirty || saveService == null) return true;
            progressDirty = !saveService.Save(progress);
            nextSaveAttempt = Time.unscaledTime + SaveRetryInterval;
            return !progressDirty;
        }

        private void Update()
        {
            if (progressDirty && Time.unscaledTime >= nextSaveAttempt) FlushProgress();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) FlushProgress();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) FlushProgress();
        }

        private void OnApplicationQuit() => FlushProgress();
        public int CurrentLevelIndex { get; private set; }

        public int HighestUnlockedLevelIndex => progress?.HighestUnlockedLevelIndex ?? 0;

        public LevelDefinition CurrentLevel =>
            levels != null && CurrentLevelIndex >= 0 && CurrentLevelIndex < levels.Length
                ? levels[CurrentLevelIndex]
                : null;

        public int LevelCount => levels?.Length ?? 0;
        public bool HasNextLevel => CurrentLevelIndex + 1 < LevelCount;
        private void Start()
        {
            if (LevelCount == 0)
            {
                Debug.LogError("GameFlowController needs at least one configured level.", this);
                return;
            }

            if (progress == null)
            {
                var service = new SaveService(Path.Combine(Application.persistentDataPath, SaveService.FileName));
                Initialize(service.LoadOrCreate(), service);
            }

            StartCurrentLevel();
        }

        public void Initialize(PlayerProgressData loadedProgress, SaveService service)
        {
            SaveService.Validate(loadedProgress);
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (LevelCount == 0) throw new InvalidOperationException("Cannot initialize progression without levels.");

            saveService = service;
            progressDirty = false;
            // Own a copy so callers cannot mutate session progress behind the flow's back.
            progress = new PlayerProgressData
            {
                SaveVersion = loadedProgress.SaveVersion,
                HighestUnlockedLevelIndex = Math.Min(loadedProgress.HighestUnlockedLevelIndex, LevelCount - 1)
            };
            progress.CurrentLevelIndex = Math.Min(loadedProgress.CurrentLevelIndex, progress.HighestUnlockedLevelIndex);
            progress.SoundEnabled = loadedProgress.SoundEnabled;
            progress.HapticsEnabled = loadedProgress.HapticsEnabled;
            CurrentLevelIndex = progress.CurrentLevelIndex;
            ApplySettings();
            if (progress.HighestUnlockedLevelIndex != loadedProgress.HighestUnlockedLevelIndex ||
                progress.CurrentLevelIndex != loadedProgress.CurrentLevelIndex)
            {
                progressDirty = true;
                FlushProgress();
            }
        }

        private void OnEnable()
        {
            gameplay.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            // Scene navigation destroys this component, so retry pending writes before leaving.
            FlushProgress();
            gameplay.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameplayState state)
        {
            if (state != GameplayState.Won || progress == null)
            {
                return;
            }

            // Progress only ever moves forward, so replaying an early level cannot
            // revoke access to later ones.
            int candidate = Math.Min(CurrentLevelIndex + 1, LevelCount - 1);
            int unlocked = Math.Max(progress.HighestUnlockedLevelIndex, candidate);
            if (unlocked != progress.HighestUnlockedLevelIndex)
            {
                progress.HighestUnlockedLevelIndex = unlocked;
                progressDirty = true;
            }

            // Continue resumes after the level just won; finishing the last one starts over.
            RememberCurrentLevel(HasNextLevel ? CurrentLevelIndex + 1 : 0);
            FlushProgress();
        }

        private void RememberCurrentLevel(int index)
        {
            if (progress == null || progress.CurrentLevelIndex == index)
            {
                return;
            }

            progress.CurrentLevelIndex = index;
            progressDirty = true;
        }

        public bool SoundEnabled => progress?.SoundEnabled ?? true;

        public bool HapticsEnabled => progress?.HapticsEnabled ?? true;

        public void SetSoundEnabled(bool enabled)
        {
            if (progress == null || progress.SoundEnabled == enabled)
            {
                return;
            }

            progress.SoundEnabled = enabled;
            ApplySettings();
            progressDirty = true;
            FlushProgress();
        }

        public void SetHapticsEnabled(bool enabled)
        {
            if (progress == null || progress.HapticsEnabled == enabled)
            {
                return;
            }

            progress.HapticsEnabled = enabled;
            ApplySettings();
            progressDirty = true;
            FlushProgress();
        }

        // Settings are stored, so they have to be pushed into the thing that acts on them.
        private void ApplySettings()
        {
            if (feedback == null || progress == null)
            {
                return;
            }

            feedback.AudioEnabled = progress.SoundEnabled;
            feedback.HapticsEnabled = progress.HapticsEnabled;
        }

        public void StartFirstLevel()
        {
            CurrentLevelIndex = 0;
            StartCurrentLevel();
        }
        public void RetryCurrentLevel()
        {
            StartCurrentLevel();
        }

        public void StartNextLevel()
        {
            // Guard direct calls beyond the sequence. The final win button offers replay.
            if (!HasNextLevel)
            {
                return;
            }

            CurrentLevelIndex++;
            StartCurrentLevel();
        }

        // What the win panel's primary button does.
        public void ContinueAfterWin()
        {
            if (HasNextLevel)
            {
                StartNextLevel();
                return;
            }

            StartFirstLevel();
        }
        private void StartCurrentLevel()
        {
            if (CurrentLevel == null)
            {
                Debug.LogError($"GameFlowController has no level at index {CurrentLevelIndex}.", this);
                return;
            }

            RememberCurrentLevel(CurrentLevelIndex);
            FlushProgress();
            bootstrap.StartLevel(CurrentLevel);
        }
    }
}
