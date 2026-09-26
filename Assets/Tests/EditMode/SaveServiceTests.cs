using System;
using System.IO;
using System.Text.RegularExpressions;
using BlastPuzzle.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class SaveServiceTests
    {
        private string directory;
        private string path;
        private SaveService service;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "BlastPuzzle-SaveTests-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, SaveService.FileName);
            service = new SaveService(path);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void MissingFile_ReturnsDefaultProgressWithoutWriting()
        {
            PlayerProgressData data = service.LoadOrCreate();
            Assert.That(data.SaveVersion, Is.EqualTo(1));
            Assert.That(data.HighestUnlockedLevelIndex, Is.Zero);
            Assert.That(File.Exists(path), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(2)]
        public void SaveThenLoad_RoundTripsVersionAndHighestUnlockedLevel(int index)
        {
            PlayerProgressData data = SaveService.CreateDefault();
            data.HighestUnlockedLevelIndex = index;
            Assert.That(service.Save(data), Is.True);
            Assert.That(File.Exists(path), Is.True);
            PlayerProgressData restored = new SaveService(path).LoadOrCreate();
            Assert.That(restored, Is.Not.SameAs(data));
            Assert.That(restored.SaveVersion, Is.EqualTo(SaveService.CurrentSaveVersion));
            Assert.That(restored.HighestUnlockedLevelIndex, Is.EqualTo(index));
        }

        [Test]
        public void SavingAgain_ReplacesExistingFileAndLeavesNoTemporaryFile()
        {
            PlayerProgressData data = SaveService.CreateDefault();
            Assert.That(service.Save(data), Is.True);
            data.HighestUnlockedLevelIndex = 2;
            Assert.That(service.Save(data), Is.True);
            Assert.That(service.LoadOrCreate().HighestUnlockedLevelIndex, Is.EqualTo(2));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void SaveFile_IsReadableJsonContainingProgressionAndSettings()
        {
            service.Save(SaveService.CreateDefault());
            string json = File.ReadAllText(path);
            Assert.That(json, Does.Contain("\n"));
            Assert.That(json, Does.Contain("\"SaveVersion\": 1"));
            Assert.That(json, Does.Contain("\"HighestUnlockedLevelIndex\": 0"));
            Assert.That(json, Does.Contain("\"CurrentLevelIndex\": 0"));
            Assert.That(Regex.Matches(json, "\"[^\"]+\"\\s*:").Count, Is.EqualTo(5));
        }

        [TestCase("this is not JSON")]
        [TestCase("{}")]
        [TestCase("{\"HighestUnlockedLevelIndex\":2}")]
        [TestCase("{\"SaveVersion\":99,\"HighestUnlockedLevelIndex\":2}")]
        public void CorruptMissingOrInvalidFields_WarnAndFallBackWithoutOverwriting(string json)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, json);
            LogAssert.Expect(LogType.Warning, new Regex("^Invalid player progress"));
            PlayerProgressData restored = service.LoadOrCreate();
            Assert.That(restored.SaveVersion, Is.EqualTo(1));
            Assert.That(restored.HighestUnlockedLevelIndex, Is.Zero);
            Assert.That(File.ReadAllText(path), Is.EqualTo(json));
        }

        [Test]
        public void InterruptedTemporaryWrite_DoesNotReplaceLastGoodSave()
        {
            var data = SaveService.CreateDefault();
            data.HighestUnlockedLevelIndex = 1;
            service.Save(data);
            File.WriteAllText(path + ".tmp", "{unfinished");
            Assert.That(service.LoadOrCreate().HighestUnlockedLevelIndex, Is.EqualTo(1));
        }

        [Test]
        public void DeleteSave_RemovesTheFileAndTheNextLoadReturnsDefaults()
        {
            service.Save(new PlayerProgressData { SaveVersion = 1, HighestUnlockedLevelIndex = 2 });
            Assert.That(File.Exists(path), Is.True);

            Assert.That(service.DeleteSave(), Is.True);
            Assert.That(File.Exists(path), Is.False);

            PlayerProgressData restored = service.LoadOrCreate();
            Assert.That(restored.HighestUnlockedLevelIndex, Is.Zero);
            Assert.That(restored.SaveVersion, Is.EqualTo(SaveService.CurrentSaveVersion));
        }

        [Test]
        public void InvalidCallerData_IsRejectedBeforeWriting()
        {
            Assert.Throws<ArgumentNullException>(() => service.Save(null));
            Assert.Throws<ArgumentException>(() => service.Save(new PlayerProgressData()));
            Assert.Throws<ArgumentException>(() => service.Save(new PlayerProgressData
                { SaveVersion = 1, HighestUnlockedLevelIndex = -1 }));
            Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public void IoFailure_IsReportedAsErrorRatherThanCorruptContent()
        {
            // A directory cannot be read/replaced as a file on any supported desktop OS.
            Directory.CreateDirectory(path);
            LogAssert.Expect(LogType.Error, new Regex("^Could not read player progress"));
            Assert.That(service.LoadOrCreate().HighestUnlockedLevelIndex, Is.Zero);
            LogAssert.Expect(LogType.Error, new Regex("^Could not save player progress"));
            Assert.That(service.Save(SaveService.CreateDefault()), Is.False);
            Assert.That(Directory.Exists(path), Is.True);
        }
    }
}
