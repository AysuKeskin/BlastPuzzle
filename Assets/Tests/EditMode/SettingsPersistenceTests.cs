using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BlastPuzzle.Core;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Levels;
using BlastPuzzle.Persistence;
using BlastPuzzle.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class SettingsPersistenceTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void FailedSettingWrite_RemainsPendingAndCanBeRetried(bool sound)
        {
            string directory = Path.Combine(Path.GetTempPath(), "BlastSettings-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            string blocker = Path.Combine(directory, "blocked");
            File.WriteAllText(blocker, "blocks directory creation");
            var service = new SaveService(Path.Combine(blocker, "save.json"));
            var host = new GameObject("SettingsTest");
            host.SetActive(false);
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                var gameplay = host.AddComponent<GameplayController>();
                var feedback = host.AddComponent<GameFeedback>();
                var flow = host.AddComponent<GameFlowController>();
                Set(flow, "gameplay", gameplay);
                Set(flow, "feedback", feedback);
                Set(flow, "levels", new[] { level });
                flow.Initialize(SaveService.CreateDefault(), service);
                LogAssert.Expect(LogType.Error, new Regex("^Could not save player progress"));
                if (sound) flow.SetSoundEnabled(false); else flow.SetHapticsEnabled(false);
                Assert.That(flow.HasPendingSave, Is.True);
                Assert.That(sound ? feedback.AudioEnabled : feedback.HapticsEnabled, Is.False);
                File.Delete(blocker);
                Assert.That(flow.FlushProgress(), Is.True);
                Assert.That(flow.HasPendingSave, Is.False);
                var saved = service.LoadOrCreate();
                Assert.That(sound ? saved.SoundEnabled : saved.HapticsEnabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(level);
                Directory.Delete(directory, true);
            }
        }

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
