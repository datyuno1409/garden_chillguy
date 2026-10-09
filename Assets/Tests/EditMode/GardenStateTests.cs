using System;
using System.IO;
using Garden.Core;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class GardenStateTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        [Serializable] struct Sample { public int number; public string text; }

        // ---------- Các phần dữ liệu theo mô-đun ----------

        [Test]
        public void CreateNew_HasCurrentVersionAndNoSections()
        {
            GardenState state = GardenState.CreateNew(T0, 42);

            Assert.AreEqual(GardenState.CurrentVersion, state.version);
            Assert.AreEqual(42, state.seed);
            Assert.AreEqual(0, state.sections.Length);
            Assert.IsTrue(state.IsValid());
        }

        [Test]
        public void WithSection_ThenTryGetSection_ReturnsTheStoredValue()
        {
            GardenState state = GardenState.CreateNew(T0, 1).WithSection("a", new Sample { number = 7, text = "xin chào" });

            Assert.IsTrue(state.TryGetSection("a", out Sample value));
            Assert.AreEqual(7, value.number);
            Assert.AreEqual("xin chào", value.text);
        }

        [Test]
        public void WithSection_ReplacesTheSameId_InsteadOfAddingADuplicate()
        {
            GardenState state = GardenState.CreateNew(T0, 1)
                .WithSection("a", new Sample { number = 1 })
                .WithSection("a", new Sample { number = 2 });

            Assert.AreEqual(1, state.sections.Length);
            state.TryGetSection("a", out Sample value);
            Assert.AreEqual(2, value.number);
        }

        [Test]
        public void WithSection_KeepsOtherModulesSectionsUntouched()
        {
            GardenState state = GardenState.CreateNew(T0, 1)
                .WithSection("moss", new Sample { number = 10 })
                .WithSection("vines", new Sample { number = 20 })
                .WithSection("moss", new Sample { number = 11 });

            state.TryGetSection("vines", out Sample vines);
            Assert.AreEqual(20, vines.number);
            Assert.AreEqual(2, state.sections.Length);
        }

        [Test]
        public void WithSection_DoesNotMutateTheOriginalState()
        {
            GardenState original = GardenState.CreateNew(T0, 1);

            original.WithSection("a", new Sample { number = 1 });

            Assert.AreEqual(0, original.sections.Length);
        }

        [Test]
        public void WithLastSeen_DoesNotMutateTheOriginalState()
        {
            GardenState original = GardenState.CreateNew(T0, 1);

            GardenState moved = original.WithLastSeen(T0.AddDays(2));

            Assert.AreEqual(T0, original.LastSeenUtc);
            Assert.AreEqual(T0.AddDays(2), moved.LastSeenUtc);
        }

        [Test]
        public void TryGetSection_ReturnsFalse_WhenSectionMissing()
        {
            Assert.IsFalse(GardenState.CreateNew(T0, 1).TryGetSection("nothing", out Sample _));
        }

        [Test]
        public void TryGetSection_ReturnsFalse_WhenSectionJsonIsGarbage()
        {
            GardenState state = GardenState.CreateNew(T0, 1);
            state.sections = new[] { new GardenSection { id = "a", json = "{{ not json" } };

            Assert.IsFalse(state.TryGetSection("a", out Sample _));
        }

        // ---------- Kiểm tra dữ liệu ----------

        [Test]
        public void IsValid_ReturnsFalse_ForDefaultState() => Assert.IsFalse(default(GardenState).IsValid());

        [Test]
        public void IsValid_ReturnsFalse_ForUnknownVersion()
        {
            GardenState state = GardenState.CreateNew(T0, 1);
            state.version = 99;

            Assert.IsFalse(state.IsValid());
        }

        [Test]
        public void IsValid_ReturnsFalse_ForDuplicateSectionIds()
        {
            GardenState state = GardenState.CreateNew(T0, 1);
            state.sections = new[]
            {
                new GardenSection { id = "a", json = "{}" },
                new GardenSection { id = "a", json = "{}" },
            };

            Assert.IsFalse(state.IsValid());
        }

        [Test]
        public void IsValid_ReturnsFalse_ForEmptySectionId()
        {
            GardenState state = GardenState.CreateNew(T0, 1);
            state.sections = new[] { new GardenSection { id = "", json = "{}" } };

            Assert.IsFalse(state.IsValid());
        }

        // ---------- Thời gian ----------

        [Test]
        public void ElapsedDays_ReturnsZero_WhenClockWentBackwards()
        {
            Assert.AreEqual(0.0, GardenClock.ElapsedDays(T0, T0.AddHours(-5)));
        }

        [Test]
        public void ElapsedDays_ReturnsExactDays_WhenTimePasses()
        {
            Assert.AreEqual(2.5, GardenClock.ElapsedDays(T0, T0.AddDays(2.5)), 1e-9);
        }

        [Test]
        public void ElapsedDays_IsCappedAtMax_WhenAwayForYears()
        {
            Assert.AreEqual(365.0, GardenClock.ElapsedDays(T0, T0.AddDays(5000), 365));
        }
    }

    // Nâng cấp file lưu: tuyệt đối không được làm mất vườn của người chơi khi đổi định dạng
    public class GardenStateMigratorTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        static string V1Json(double age, int seed = 77, long? lastSeen = null) =>
            "{\"version\":1,\"seed\":" + seed + ",\"createdTicksUtc\":" + T0.Ticks + ",\"lastSeenTicksUtc\":" + (lastSeen ?? T0.AddDays(3).Ticks)
            + ",\"mossAgeDays\":" + age.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}";

        [Test]
        public void TryParse_ReadsTheCurrentVersion_AsLoaded()
        {
            string json = JsonUtility.ToJson(GardenState.CreateNew(T0, 5).WithSection("a", new GardenStateTests_Sample { n = 1 }));

            LoadOutcome outcome = GardenStateMigrator.TryParse(json, out GardenState state);

            Assert.AreEqual(LoadOutcome.Loaded, outcome);
            Assert.AreEqual(5, state.seed);
        }

        [Test]
        public void TryParse_UpgradesVersion1_KeepingSeedAgeAndTimes()
        {
            LoadOutcome outcome = GardenStateMigrator.TryParse(V1Json(6.25), out GardenState state);

            Assert.AreEqual(LoadOutcome.Migrated, outcome);
            Assert.AreEqual(GardenState.CurrentVersion, state.version);
            Assert.AreEqual(77, state.seed);
            Assert.AreEqual(T0.Ticks, state.createdTicksUtc);
            Assert.AreEqual(T0.AddDays(3), state.LastSeenUtc);
            Assert.IsTrue(state.TryGetSection(GardenStateMigrator.MossSectionId, out GardenStateTests_Age moss));
            Assert.AreEqual(6.25, moss.ageDays, 1e-9);
            Assert.IsTrue(state.IsValid());
        }

        [Test]
        public void TryParse_RejectsVersion1WithNegativeAge_AsCorrupt()
        {
            Assert.AreEqual(LoadOutcome.Corrupt, GardenStateMigrator.TryParse(V1Json(-1), out _));
        }

        [Test]
        public void TryParse_ReturnsNewerThanSupported_ForAFutureVersion()
        {
            Assert.AreEqual(LoadOutcome.NewerThanSupported,
                GardenStateMigrator.TryParse("{\"version\":" + (GardenState.CurrentVersion + 1) + "}", out _));
        }

        [Test]
        public void TryParse_ReturnsCorrupt_ForGarbage()
        {
            Assert.AreEqual(LoadOutcome.Corrupt, GardenStateMigrator.TryParse("{ not json at all", out _));
        }

        [Test]
        public void TryParse_ReturnsCorrupt_ForMissingVersion()
        {
            Assert.AreEqual(LoadOutcome.Corrupt, GardenStateMigrator.TryParse("{\"seed\":1}", out _));
        }
    }

    [Serializable] public struct GardenStateTests_Sample { public int n; }
    [Serializable] public struct GardenStateTests_Age { public double ageDays; }

    public class GardenStateStoreTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        string directory;
        string path;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "garden-tests-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "state.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void SaveThenLoad_RestoresTheSameState()
        {
            GardenState state = GardenState.CreateNew(T0, 1234).WithSection("a", new GardenStateTests_Sample { n = 9 });

            GardenStateStore.Save(path, state);
            LoadResult loaded = GardenStateStore.Load(path, T0, 1);

            Assert.AreEqual(LoadOutcome.Loaded, loaded.outcome);
            Assert.AreEqual(1234, loaded.state.seed);
            Assert.IsTrue(loaded.state.TryGetSection("a", out GardenStateTests_Sample sample));
            Assert.AreEqual(9, sample.n);
        }

        [Test]
        public void Save_CreatesTheFolder_WhenMissing()
        {
            GardenStateStore.Save(path, GardenState.CreateNew(T0, 1));

            Assert.IsTrue(File.Exists(path));
        }

        [Test]
        public void Save_OverwritesAnExistingFile_AndLeavesNoTempFile()
        {
            GardenStateStore.Save(path, GardenState.CreateNew(T0, 1));
            GardenStateStore.Save(path, GardenState.CreateNew(T0, 2));

            Assert.AreEqual(2, GardenStateStore.Load(path, T0, 9).state.seed);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void Load_ReturnsAFreshState_WhenFileMissing()
        {
            LoadResult result = GardenStateStore.Load(path, T0, 55);

            Assert.AreEqual(LoadOutcome.Missing, result.outcome);
            Assert.AreEqual(55, result.state.seed);
            Assert.AreEqual(T0, result.state.LastSeenUtc);
        }

        [Test]
        public void Load_KeepsABackup_AndStartsFresh_WhenFileIsCorrupt()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{ not json at all");

            LoadResult result = GardenStateStore.Load(path, T0, 55);

            Assert.AreEqual(LoadOutcome.Corrupt, result.outcome);
            Assert.AreEqual(55, result.state.seed);
            Assert.AreEqual("{ not json at all", File.ReadAllText(path + GardenStateStore.CorruptBackupSuffix));
        }

        [Test]
        public void Load_MigratesAVersion1File_AndKeepsTheOriginalAsBackup()
        {
            Directory.CreateDirectory(directory);
            string v1 = "{\"version\":1,\"seed\":9,\"createdTicksUtc\":" + T0.Ticks + ",\"lastSeenTicksUtc\":" + T0.Ticks + ",\"mossAgeDays\":4.5}";
            File.WriteAllText(path, v1);

            LoadResult result = GardenStateStore.Load(path, T0.AddDays(1), 55);

            Assert.AreEqual(LoadOutcome.Migrated, result.outcome);
            Assert.AreEqual(9, result.state.seed, "không được đổi hạt giống: vườn phải giữ nguyên kiểu loang");
            Assert.IsTrue(result.state.TryGetSection("moss", out GardenStateTests_Age moss));
            Assert.AreEqual(4.5, moss.ageDays, 1e-9);
            Assert.AreEqual(v1, File.ReadAllText(path + GardenStateStore.MigratedBackupSuffix));
        }

        [Test]
        public void Load_DoesNotOverwriteAnExistingMigrationBackup()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"version\":1,\"seed\":9,\"createdTicksUtc\":" + T0.Ticks + ",\"lastSeenTicksUtc\":" + T0.Ticks + ",\"mossAgeDays\":4.5}");
            File.WriteAllText(path + GardenStateStore.MigratedBackupSuffix, "ORIGINAL BACKUP");

            GardenStateStore.Load(path, T0, 1);

            Assert.AreEqual("ORIGINAL BACKUP", File.ReadAllText(path + GardenStateStore.MigratedBackupSuffix));
        }

        [Test]
        public void Load_KeepsABackup_WhenFileIsFromANewerApp()
        {
            Directory.CreateDirectory(directory);
            string newer = "{\"version\":" + (GardenState.CurrentVersion + 5) + ",\"stuff\":1}";
            File.WriteAllText(path, newer);

            LoadResult result = GardenStateStore.Load(path, T0, 3);

            Assert.AreEqual(LoadOutcome.NewerThanSupported, result.outcome);
            Assert.AreEqual(newer, File.ReadAllText(path + GardenStateStore.NewerBackupSuffix));
        }
    }
}
