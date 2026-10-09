using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Garden.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Garden.Tests
{
    public class TimeWindowTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Days_IsTheLengthOfTheWindow() => Assert.AreEqual(2.5, new TimeWindow(T0, T0.AddDays(2.5)).Days, 1e-9);

        [Test]
        public void Days_IsZero_ForABackwardsWindow() => Assert.AreEqual(0.0, new TimeWindow(T0, T0.AddDays(-1)).Days);

        [Test]
        public void FromDays_BuildsTheExpectedWindow()
        {
            TimeWindow window = TimeWindow.FromDays(T0, 3);

            Assert.AreEqual(T0, window.fromUtc);
            Assert.AreEqual(T0.AddDays(3), window.toUtc);
        }

        [Test]
        public void TryWindow_ReturnsFalse_WhenNoTimePassed() => Assert.IsFalse(GardenClock.TryWindow(T0, T0, 365, out _));

        [Test]
        public void TryWindow_ReturnsFalse_WhenClockWentBackwards() => Assert.IsFalse(GardenClock.TryWindow(T0, T0.AddHours(-3), 365, out _));

        [Test]
        public void TryWindow_KeepsTheExactStart_WhenNotCapped()
        {
            Assert.IsTrue(GardenClock.TryWindow(T0, T0.AddDays(3), 365, out TimeWindow window));

            Assert.AreEqual(T0, window.fromUtc);
            Assert.AreEqual(T0.AddDays(3), window.toUtc);
        }

        [Test]
        public void TryWindow_OnlyCountsTheMostRecentMaxDays_WhenAwayTooLong()
        {
            Assert.IsTrue(GardenClock.TryWindow(T0, T0.AddDays(1000), 365, out TimeWindow window));

            Assert.AreEqual(T0.AddDays(1000), window.toUtc);
            Assert.AreEqual(365.0, window.Days, 1e-6);
        }
    }

    public class SessionWindowTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        string directory;
        string path;
        DateTime now;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "garden-windows-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "state.json");
            now = T0;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        GardenSession NewSession(params IGardenModule[] modules) => new GardenSession(path, modules, () => now);

        [Test]
        public void TheOfflineWindow_RunsFromTheLastTimeSeenToNow()
        {
            NewSession(new FakeModule("a")).Start(1);
            now = T0.AddDays(3);

            var module = new FakeModule("a");
            NewSession(module).Start(1);

            Assert.AreEqual(1, module.windows.Count);
            Assert.AreEqual(T0, module.windows[0].fromUtc);
            Assert.AreEqual(T0.AddDays(3), module.windows[0].toUtc);
        }

        [Test]
        public void RefreshWindows_AreContiguous_SoNoTimeIsCountedTwiceOrSkipped()
        {
            var module = new FakeModule("a");
            GardenSession session = NewSession(module);
            session.Start(1);

            now = T0.AddHours(5);
            session.Refresh();
            now = T0.AddHours(9);
            session.Refresh();

            Assert.AreEqual(2, module.windows.Count);
            Assert.AreEqual(module.windows[0].toUtc, module.windows[1].fromUtc);
            Assert.AreEqual(T0, module.windows[0].fromUtc);
            Assert.AreEqual(T0.AddHours(9), module.windows[1].toUtc);
        }

        [Test]
        public void ALongAbsence_IsCappedToTheMostRecentYear()
        {
            NewSession(new FakeModule("a")).Start(1);
            now = T0.AddDays(2000);

            var module = new FakeModule("a");
            NewSession(module).Start(1);

            Assert.AreEqual(365.0, module.windows[0].Days, 1e-6);
            Assert.AreEqual(now, module.windows[0].toUtc);
        }
    }

    public class GardenSettingsTests
    {
        [Serializable] struct Sample { public int number; }

        string directory;
        string path;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "garden-settings-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "settings.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        // ---------- Dữ liệu ----------

        [Test]
        public void Default_IsValid() => Assert.IsTrue(GardenSettings.Default.IsValid());

        [Test]
        public void WithSection_ThenTryGetSection_ReturnsTheValue()
        {
            GardenSettings settings = GardenSettings.Default.WithSection("rain", new Sample { number = 4 });

            Assert.IsTrue(settings.TryGetSection("rain", out Sample sample));
            Assert.AreEqual(4, sample.number);
        }

        [Test]
        public void GetOrDefault_ReturnsTheFallback_WhenTheSectionIsMissing()
        {
            Assert.AreEqual(9, GardenSettings.Default.GetOrDefault("rain", new Sample { number = 9 }).number);
        }

        [Test]
        public void WithSection_DoesNotMutateTheOriginal()
        {
            GardenSettings original = GardenSettings.Default;

            original.WithSection("rain", new Sample { number = 1 });

            Assert.AreEqual(0, original.sections.Length);
        }

        [Test]
        public void SectionsOfDifferentModules_AreIndependent()
        {
            GardenSettings settings = GardenSettings.Default
                .WithSection("rain", new Sample { number = 1 })
                .WithSection("moss", new Sample { number = 2 })
                .WithSection("rain", new Sample { number = 3 });

            settings.TryGetSection("moss", out Sample moss);
            settings.TryGetSection("rain", out Sample rain);
            Assert.AreEqual(2, moss.number);
            Assert.AreEqual(3, rain.number);
        }

        // ---------- File ----------

        [Test]
        public void Load_ReturnsDefaults_WhenFileMissing()
        {
            Assert.IsTrue(GardenSettingsStore.Load(path).IsValid());
            Assert.IsFalse(File.Exists(path));
        }

        [Test]
        public void SaveThenLoad_RestoresTheSettings()
        {
            GardenSettingsStore.Save(path, GardenSettings.Default.WithSection("rain", new Sample { number = 7 }));

            GardenSettingsStore.Load(path).TryGetSection("rain", out Sample sample);

            Assert.AreEqual(7, sample.number);
        }

        [Test]
        public void Save_LeavesNoTempFile_AndOverwritesTheOldOne()
        {
            GardenSettingsStore.Save(path, GardenSettings.Default.WithSection("a", new Sample { number = 1 }));
            GardenSettingsStore.Save(path, GardenSettings.Default.WithSection("a", new Sample { number = 2 }));

            GardenSettingsStore.Load(path).TryGetSection("a", out Sample sample);
            Assert.AreEqual(2, sample.number);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void Load_KeepsABackup_AndUsesDefaults_WhenFileIsCorrupt()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{ not json");

            GardenSettings settings = GardenSettingsStore.Load(path);

            Assert.IsTrue(settings.IsValid());
            Assert.AreEqual("{ not json", File.ReadAllText(path + GardenSettingsStore.CorruptBackupSuffix));
        }

        [Test]
        public void Load_KeepsABackup_WhenFileIsFromANewerApp()
        {
            Directory.CreateDirectory(directory);
            string newer = "{\"version\":" + (GardenSettings.CurrentVersion + 3) + ",\"extra\":1}";
            File.WriteAllText(path, newer);

            GardenSettingsStore.Load(path);

            Assert.AreEqual(newer, File.ReadAllText(path + GardenSettingsStore.NewerBackupSuffix));
        }

        [Test]
        public void Load_TreatsAFileWithoutVersionAsCorrupt()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"sections\":[]}");

            GardenSettingsStore.Load(path);

            Assert.IsTrue(File.Exists(path + GardenSettingsStore.CorruptBackupSuffix));
        }
    }

    public class DebouncedSettingsWriterTests
    {
        [Serializable] struct Sample { public int number; }

        static GardenSettings Settings(int number) => GardenSettings.Default.WithSection("a", new Sample { number = number });

        static int NumberOf(GardenSettings settings)
        {
            settings.TryGetSection("a", out Sample sample);
            return sample.number;
        }

        [Test]
        public void NothingChanged_NothingIsWritten()
        {
            int writes = 0;
            var writer = new DebouncedSettingsWriter(_ => writes++);

            writer.Update(100f);
            writer.Flush(100f);

            Assert.AreEqual(0, writes);
            Assert.IsFalse(writer.HasPendingChange);
        }

        [Test]
        public void AChange_IsNotWrittenImmediately_ButAfterTheDelay()
        {
            int writes = 0;
            var writer = new DebouncedSettingsWriter(_ => writes++, 0.25f);

            writer.MarkChanged(Settings(1), 10f);
            writer.Update(10.1f);
            Assert.AreEqual(0, writes);

            writer.Update(10.3f);
            Assert.AreEqual(1, writes);
            Assert.IsFalse(writer.HasPendingChange);
        }

        [Test]
        public void ManyQuickChanges_AreCoalescedIntoOneWriteOfTheLatest()
        {
            var written = new List<int>();
            var writer = new DebouncedSettingsWriter(s => written.Add(NumberOf(s)), 0.25f);

            writer.MarkChanged(Settings(1), 10.00f);
            writer.MarkChanged(Settings(2), 10.10f);   // đang kéo thanh trượt
            writer.MarkChanged(Settings(3), 10.20f);
            writer.Update(10.30f);   // chưa đủ 0.25s kể từ lần chỉnh cuối
            writer.Update(10.50f);

            CollectionAssert.AreEqual(new[] { 3 }, written);
        }

        [Test]
        public void Flush_WritesPendingChangesAtOnce()
        {
            var written = new List<int>();
            var writer = new DebouncedSettingsWriter(s => written.Add(NumberOf(s)), 5f);
            writer.MarkChanged(Settings(7), 10f);

            writer.Flush(10.01f);

            CollectionAssert.AreEqual(new[] { 7 }, written);
        }

        [Test]
        public void AfterAWrite_NothingIsWrittenAgainUntilTheNextChange()
        {
            int writes = 0;
            var writer = new DebouncedSettingsWriter(_ => writes++, 0.25f);
            writer.MarkChanged(Settings(1), 10f);
            writer.Update(11f);

            writer.Update(12f);
            writer.Flush(12f);

            Assert.AreEqual(1, writes);
        }

        [Test]
        public void AFailedWrite_KeepsTheChange_AndRetriesLaterInsteadOfEveryFrame()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new Regex("DebouncedSettingsWriter"));
            int attempts = 0;
            var writer = new DebouncedSettingsWriter(_ =>
            {
                attempts++;
                if (attempts == 1) throw new IOException("đĩa đầy");
            }, 0.25f);
            writer.MarkChanged(Settings(1), 10f);

            writer.Update(10.3f);   // lần 1: lỗi
            Assert.AreEqual(1, attempts);
            Assert.IsTrue(writer.HasPendingChange, "thay đổi chưa ghi được phải được giữ lại");

            writer.Update(10.4f);   // chưa tới giờ thử lại
            writer.Update(11f);
            Assert.AreEqual(1, attempts);

            writer.Update(12.4f);   // đã qua thời gian chờ thử lại
            Assert.AreEqual(2, attempts);
            Assert.IsFalse(writer.HasPendingChange);
        }

        [Test]
        public void ANonIoException_IsNotSwallowed()
        {
            var writer = new DebouncedSettingsWriter(_ => throw new InvalidOperationException("lỗi lập trình"), 0.25f);
            writer.MarkChanged(Settings(1), 10f);

            Assert.Throws<InvalidOperationException>(() => writer.Update(11f));
        }
    }

    public class ConfigurableApplierTests
    {
        sealed class Recorder : IGardenConfigurable
        {
            public int applied;
            public void ApplySettings(GardenSettings settings) { applied++; }
        }

        sealed class Failing : IGardenConfigurable
        {
            public void ApplySettings(GardenSettings settings) => throw new InvalidOperationException("boom");
        }

        [Test]
        public void Apply_ReachesEveryTarget()
        {
            var a = new Recorder();
            var b = new Recorder();

            ConfigurableApplier.Apply(new IGardenConfigurable[] { a, b }, GardenSettings.Default);

            Assert.AreEqual(1, a.applied);
            Assert.AreEqual(1, b.applied);
        }

        [Test]
        public void AFailingTarget_DoesNotStopTheOthers()
        {
            LogAssert.Expect(LogType.Error, new Regex("ConfigurableApplier.*Failing"));
            var healthy = new Recorder();

            ConfigurableApplier.Apply(new IGardenConfigurable[] { new Failing(), healthy }, GardenSettings.Default);

            Assert.AreEqual(1, healthy.applied);
        }
    }

    public class GardenArgsTests
    {
        [Test]
        public void TryGet_ReturnsTheValueAfterTheName()
        {
            string[] args = { "app.exe", "-gardenAgeDays", "7", "-gardenTab", "1" };

            Assert.IsTrue(GardenArgs.TryGet(args, GardenArgs.Tab, out string value));
            Assert.AreEqual("1", value);
        }

        [Test]
        public void TryGet_ReturnsFalse_WhenTheNameIsMissing()
        {
            Assert.IsFalse(GardenArgs.TryGet(new[] { "app.exe" }, GardenArgs.AgeDays, out string value));
            Assert.IsNull(value);
        }

        [Test]
        public void TryGet_ReturnsFalse_WhenTheNameIsTheLastArgumentWithoutAValue()
        {
            Assert.IsFalse(GardenArgs.TryGet(new[] { "app.exe", GardenArgs.AgeDays }, GardenArgs.AgeDays, out _));
        }
    }
}
