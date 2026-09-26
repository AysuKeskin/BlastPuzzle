using System;
using System.IO;
using UnityEngine;

namespace BlastPuzzle.Persistence
{
    // Reads and writes the progress file. Knows nothing about gameplay.
    public sealed class SaveService
    {
        public const int CurrentSaveVersion = 1;
        public const string FileName = "player-progress.json";

        private readonly string filePath;

        public SaveService(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A save file path is required.", nameof(filePath));
            }

            this.filePath = Path.GetFullPath(filePath);
        }

        public static PlayerProgressData CreateDefault() => new PlayerProgressData
        {
            SaveVersion = CurrentSaveVersion,
            HighestUnlockedLevelIndex = 0,
            CurrentLevelIndex = 0,
            SoundEnabled = true,
            HapticsEnabled = true
        };

        public PlayerProgressData LoadOrCreate()
        {
            string json;
            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (FileNotFoundException)
            {
                return CreateDefault();
            }
            catch (DirectoryNotFoundException)
            {
                return CreateDefault();
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogError($"Could not read player progress at '{filePath}': {error.Message}. Using defaults for this session.");
                return CreateDefault();
            }

            try
            {
                // Sentinels, because FromJsonOverwrite leaves missing fields untouched --
                // an absent field would otherwise look like a valid index 0.
                // Only the two ints get sentinels; the bools keep their defaults so an
                // older save without them stays fully enabled.
                var loaded = new PlayerProgressData
                    { SaveVersion = -1, HighestUnlockedLevelIndex = -1, CurrentLevelIndex = -1 };
                string trimmed = json.Trim();
                if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}"))
                {
                    throw new ArgumentException("Expected a JSON object.");
                }

                JsonUtility.FromJsonOverwrite(json, loaded);
                if (loaded.HighestUnlockedLevelIndex >= 0)
                {
                    // Missing in older saves; never ahead of what has been unlocked.
                    loaded.CurrentLevelIndex = loaded.CurrentLevelIndex < 0
                        ? loaded.HighestUnlockedLevelIndex
                        : Math.Min(loaded.CurrentLevelIndex, loaded.HighestUnlockedLevelIndex);
                }
                Validate(loaded);
                return loaded;
            }
            catch (ArgumentException error)
            {
                Debug.LogWarning($"Invalid player progress at '{filePath}': {error.Message}. Using default progress.");
                return CreateDefault();
            }
        }

        public static void Validate(PlayerProgressData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data.SaveVersion != CurrentSaveVersion)
            {
                throw new ArgumentException($"Unsupported save version {data.SaveVersion}; expected {CurrentSaveVersion}.");
            }
            if (data.HighestUnlockedLevelIndex < 0)
            {
                throw new ArgumentException("Highest unlocked level index cannot be negative or missing.");
            }
            if (data.CurrentLevelIndex < 0 || data.CurrentLevelIndex > data.HighestUnlockedLevelIndex)
            {
                throw new ArgumentException("Current level index must be between 0 and the highest unlocked level.");
            }
        }
        public bool DeleteSave()
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                File.Delete(filePath);
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogError($"Could not delete player progress at '{filePath}': {error.Message}.");
                return false;
            }
        }
        public bool Save(PlayerProgressData data)
        {
            Validate(data);
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            string temporaryPath = filePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(filePath))
                {
                    File.Replace(temporaryPath, filePath, null);
                }
                else
                {
                    File.Move(temporaryPath, filePath);
                }
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                                         error is NotSupportedException)
            {
                Debug.LogError($"Could not save player progress at '{filePath}': {error.Message}. Progress is only available in this session.");
                return false;
            }
        }
    }
}
