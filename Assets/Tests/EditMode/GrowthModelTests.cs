using System;
using System.IO;
using NUnit.Framework;

namespace Garden.Growth.Tests
{
    public class GrowthModelTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        static readonly GrowthSettings Settings = GrowthSettings.Default;

        // ---------- Thời gian trôi qua ----------

        [Test]
        public void ElapsedDays_ReturnsZero_WhenClockWentBackwards()
        {
            Assert.AreEqual(0.0, GrowthModel.ElapsedDays(T0, T0.AddHours(-5), 365));
        }

        [Test]
        public void ElapsedDays_ReturnsExactDays_WhenTimePasses()
        {
            Assert.AreEqual(2.5, GrowthModel.ElapsedDays(T0, T0.AddDays(2.5), 365), 1e-9);
        }

        [Test]
        public void ElapsedDays_IsCappedAtMax_WhenAwayForYears()
        {
            Assert.AreEqual(365.0, GrowthModel.ElapsedDays(T0, T0.AddDays(5000), 365));
        }

        // ---------- Cập nhật trạng thái ----------

        [Test]
        public void Advance_AddsOfflineDaysToAge_AndUpdatesLastSeen()
        {
            GardenState state = GardenState.CreateNew(T0, 7);

            GardenState advanced = GrowthModel.Advance(state, T0.AddDays(3), Settings);

            Assert.AreEqual(3.0, advanced.mossAgeDays, 1e-9);
            Assert.AreEqual(T0.AddDays(3), advanced.LastSeenUtc);
        }

        [Test]
        public void Advance_DoesNotMutateTheOriginalState()
        {
            GardenState state = GardenState.CreateNew(T0, 7);

            GrowthModel.Advance(state, T0.AddDays(3), Settings);

            Assert.AreEqual(0.0, state.mossAgeDays);
            Assert.AreEqual(T0, state.LastSeenUtc);
        }

        [Test]
        public void Advance_KeepsAge_WhenClockWentBackwards_AndResyncsLastSeen()
        {
            GardenState state = GardenState.CreateNew(T0, 7).WithAge(4.0, T0);

            GardenState advanced = GrowthModel.Advance(state, T0.AddDays(-2), Settings);

            Assert.AreEqual(4.0, advanced.mossAgeDays);
            Assert.AreEqual(T0.AddDays(-2), advanced.LastSeenUtc);
        }

        [Test]
        public void Advance_AccumulatesAcrossSeveralSessions()
        {
            GardenState state = GardenState.CreateNew(T0, 7);
            state = GrowthModel.Advance(state, T0.AddDays(1), Settings);
            state = GrowthModel.Advance(state, T0.AddDays(1.5), Settings);
            state = GrowthModel.Advance(state, T0.AddDays(4), Settings);

            Assert.AreEqual(4.0, state.mossAgeDays, 1e-9);
        }

        // ---------- Độ phủ rêu ----------

        [Test]
        public void Coverage_StartsAtStartCoverage_WhenNewGarden()
        {
            Assert.AreEqual(Settings.startCoverage, GrowthModel.Coverage(0, Settings), 1e-6);
        }

        [Test]
        public void Coverage_ReachesMaxCoverage_AtFullCoverDays()
        {
            Assert.AreEqual(Settings.maxCoverage, GrowthModel.Coverage(Settings.daysToFullCover, Settings), 1e-6);
        }

        [Test]
        public void Coverage_StaysAtMax_WhenFarPastFullCover()
        {
            Assert.AreEqual(Settings.maxCoverage, GrowthModel.Coverage(10000, Settings), 1e-6);
        }

        [Test]
        public void Coverage_NeverDecreases_AsAgeGrows()
        {
            float previous = -1f;
            for (double age = 0; age <= Settings.daysToFullCover * 1.5; age += 0.25)
            {
                float coverage = GrowthModel.Coverage(age, Settings);
                Assert.GreaterOrEqual(coverage, previous);
                previous = coverage;
            }
        }

        [Test]
        public void Coverage_GrowsSlowlyAtFirst_ThenFaster()
        {
            float firstDay = GrowthModel.Coverage(1, Settings) - GrowthModel.Coverage(0, Settings);
            float midDay = GrowthModel.Coverage(Settings.daysToFullCover / 2 + 1, Settings) - GrowthModel.Coverage(Settings.daysToFullCover / 2, Settings);

            Assert.Less(firstDay, midDay);
        }

        [Test]
        public void Coverage_ClampsNegativeAge_ToStartCoverage()
        {
            Assert.AreEqual(Settings.startCoverage, GrowthModel.Coverage(-5, Settings), 1e-6);
        }

        [Test]
        public void Coverage_IsMax_WhenDaysToFullCoverIsNotPositive()
        {
            var broken = new GrowthSettings { daysToFullCover = 0f, startCoverage = 0.1f, maxCoverage = 0.8f, maxOfflineDays = 365 };

            Assert.AreEqual(0.8f, GrowthModel.Coverage(1, broken), 1e-6);
        }

        // ---------- Kiểm tra dữ liệu ----------

        [Test]
        public void IsValid_ReturnsTrue_ForNewState()
        {
            Assert.IsTrue(GardenState.CreateNew(T0, 1).IsValid());
        }

        [Test]
        public void IsValid_ReturnsFalse_ForNaNAge()
        {
            Assert.IsFalse(GardenState.CreateNew(T0, 1).WithAge(double.NaN, T0).IsValid());
        }

        [Test]
        public void IsValid_ReturnsFalse_ForNegativeAge()
        {
            Assert.IsFalse(GardenState.CreateNew(T0, 1).WithAge(-1, T0).IsValid());
        }

        [Test]
        public void IsValid_ReturnsFalse_ForUnknownVersion()
        {
            GardenState state = GardenState.CreateNew(T0, 1);
            state.version = 99;

            Assert.IsFalse(state.IsValid());
        }

        [Test]
        public void IsValid_ReturnsFalse_ForDefaultState_BecauseTicksAreOutOfRange()
        {
            Assert.IsFalse(default(GardenState).IsValid());
        }
    }

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
        public void SaveThenRead_RestoresTheSameState()
        {
            GardenState state = GardenState.CreateNew(T0, 1234).WithAge(6.25, T0.AddDays(6.25));

            GardenStateStore.Save(path, state);
            bool ok = GardenStateStore.TryRead(path, out GardenState loaded);

            Assert.IsTrue(ok);
            Assert.AreEqual(state.seed, loaded.seed);
            Assert.AreEqual(state.mossAgeDays, loaded.mossAgeDays, 1e-9);
            Assert.AreEqual(state.createdTicksUtc, loaded.createdTicksUtc);
            Assert.AreEqual(state.lastSeenTicksUtc, loaded.lastSeenTicksUtc);
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

            GardenStateStore.TryRead(path, out GardenState loaded);
            Assert.AreEqual(2, loaded.seed);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void TryRead_ReturnsFalse_WhenFileMissing()
        {
            Assert.IsFalse(GardenStateStore.TryRead(path, out _));
        }

        [Test]
        public void TryRead_ReturnsFalse_AndKeepsABackup_WhenFileIsCorrupt()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{ not json at all");

            bool ok = GardenStateStore.TryRead(path, out _);

            Assert.IsFalse(ok);
            Assert.IsTrue(File.Exists(path + ".bad"), "file hỏng phải được giữ lại để xem");
        }

        [Test]
        public void TryRead_ReturnsFalse_WhenJsonIsValidButStateIsNot()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"version\":1,\"seed\":1,\"createdTicksUtc\":1,\"lastSeenTicksUtc\":1,\"mossAgeDays\":-3}");

            Assert.IsFalse(GardenStateStore.TryRead(path, out _));
        }

        [Test]
        public void LoadOrCreate_CreatesAFreshState_WhenNothingSaved()
        {
            GardenState state = GardenStateStore.LoadOrCreate(path, T0, 55);

            Assert.AreEqual(55, state.seed);
            Assert.AreEqual(0.0, state.mossAgeDays);
            Assert.AreEqual(T0, state.LastSeenUtc);
        }

        [Test]
        public void LoadOrCreate_ReturnsTheSavedState_WhenPresent()
        {
            GardenStateStore.Save(path, GardenState.CreateNew(T0, 9).WithAge(3.0, T0));

            GardenState state = GardenStateStore.LoadOrCreate(path, T0.AddDays(1), 55);

            Assert.AreEqual(9, state.seed);
            Assert.AreEqual(3.0, state.mossAgeDays);
        }
    }
}
