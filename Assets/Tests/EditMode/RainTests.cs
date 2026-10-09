using System;
using System.Collections.Generic;
using Garden.Core;
using Garden.Moss;
using Garden.Rain;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class RainScheduleTests
    {
        const int Seed = 4242;
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        static readonly RainSettings Normal = RainSettings.Default;

        // Tìm một ô thời gian có mưa tự nhiên với tần suất đã cho
        static long FirstRainyCell(int seed, int frequency)
        {
            long cell = RainSchedule.CellIndex(T0.Ticks);
            for (long c = cell; c < cell + 2000; c++)
            {
                if (RainSchedule.TryGetNaturalRain(seed, c, RainSchedule.ChanceFor(frequency), out _, out _)) return c;
            }
            throw new InvalidOperationException("không tìm thấy ô mưa");
        }

        // ---------- Lịch xác định ----------

        [Test]
        public void SameInputs_AlwaysGiveTheSameRain()
        {
            double a = RainSchedule.RainDays(Seed, T0, T0.AddDays(60), Normal);
            double b = RainSchedule.RainDays(Seed, T0, T0.AddDays(60), Normal);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void DifferentSeeds_GiveDifferentWeather()
        {
            double a = RainSchedule.RainDays(1, T0, T0.AddDays(200), Normal);
            double b = RainSchedule.RainDays(2, T0, T0.AddDays(200), Normal);

            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void RainDays_IsAdditiveAcrossAdjacentWindows()
        {
            DateTime mid = T0.AddDays(13.37);

            double whole = RainSchedule.RainDays(Seed, T0, T0.AddDays(40), Normal);
            double parts = RainSchedule.RainDays(Seed, T0, mid, Normal) + RainSchedule.RainDays(Seed, mid, T0.AddDays(40), Normal);

            Assert.AreEqual(whole, parts, 1e-9, "tính theo từng đoạn hay cả khoảng đều phải ra cùng một kết quả (offline hay online không khác nhau)");
        }

        [Test]
        public void RainDays_NeverExceedsTheWindow()
        {
            var heavy = new RainSettings { enabled = true, frequency = 2 };

            for (int hours = 1; hours <= 100; hours += 7)
            {
                double rain = RainSchedule.RainDays(Seed, T0, T0.AddHours(hours), heavy);
                Assert.LessOrEqual(rain, hours / 24.0 + 1e-9);
            }
        }

        [Test]
        public void RainDays_IsZero_ForAnEmptyOrBackwardsWindow()
        {
            Assert.AreEqual(0.0, RainSchedule.RainDays(Seed, T0, T0, Normal));
            Assert.AreEqual(0.0, RainSchedule.RainDays(Seed, T0.AddDays(2), T0, Normal));
        }

        [Test]
        public void ARainyDay_ExistsWithinAYear()
        {
            Assert.Greater(RainSchedule.RainDays(Seed, T0, T0.AddDays(365), Normal), 0.0);
        }

        [Test]
        public void ADisabledGarden_HasNoNaturalRain()
        {
            var off = new RainSettings { enabled = false, frequency = 2 };

            Assert.AreEqual(0.0, RainSchedule.RainDays(Seed, T0, T0.AddDays(365), off));
        }

        // ---------- Tần suất ----------

        [Test]
        public void HigherFrequency_RainsMore()
        {
            double low = RainSchedule.RainDays(Seed, T0, T0.AddDays(400), new RainSettings { enabled = true, frequency = 0 });
            double high = RainSchedule.RainDays(Seed, T0, T0.AddDays(400), new RainSettings { enabled = true, frequency = 2 });

            Assert.Greater(high, low);
        }

        [Test]
        public void HigherFrequency_OnlyAddsRain_NeverMovesTheExistingShowers()
        {
            long checkedCells = 0;
            long start = RainSchedule.CellIndex(T0.Ticks);
            for (long cell = start; cell < start + 800; cell++)
            {
                if (!RainSchedule.TryGetNaturalRain(Seed, cell, RainSchedule.ChanceFor(0), out long lowStart, out long lowEnd)) continue;

                Assert.IsTrue(RainSchedule.TryGetNaturalRain(Seed, cell, RainSchedule.ChanceFor(2), out long highStart, out long highEnd));
                Assert.AreEqual(lowStart, highStart);
                Assert.AreEqual(lowEnd, highEnd);
                checkedCells++;
            }
            Assert.Greater(checkedCells, 0);
        }

        [Test]
        public void TheShareOfRainyCells_MatchesTheChance()
        {
            long start = RainSchedule.CellIndex(T0.Ticks);
            int rainy = 0;
            const int cells = 2000;
            for (long cell = start; cell < start + cells; cell++)
            {
                if (RainSchedule.TryGetNaturalRain(Seed, cell, 0.25, out _, out _)) rainy++;
            }

            Assert.That(rainy / (double)cells, Is.EqualTo(0.25).Within(0.04));
        }

        [Test]
        public void ChanceFor_ClampsOutOfRangeFrequencies()
        {
            Assert.AreEqual(RainSchedule.ChanceFor(0), RainSchedule.ChanceFor(-5));
            Assert.AreEqual(RainSchedule.ChanceFor(RainSchedule.FrequencyCount - 1), RainSchedule.ChanceFor(99));
        }

        // ---------- Hình dạng một trận mưa ----------

        [Test]
        public void EachShower_LastsTwentyToNinetyMinutes_AndStaysInsideItsCell()
        {
            long start = RainSchedule.CellIndex(T0.Ticks);
            for (long cell = start; cell < start + 600; cell++)
            {
                if (!RainSchedule.TryGetNaturalRain(Seed, cell, 0.45, out long from, out long to)) continue;

                double minutes = (to - from) / (double)TimeSpan.TicksPerMinute;
                Assert.GreaterOrEqual(minutes, 20 - 1e-6);
                Assert.LessOrEqual(minutes, 90 + 1e-6);

                long cellStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks + cell * TimeSpan.TicksPerHour * 6;
                Assert.GreaterOrEqual(from, cellStart);
                Assert.LessOrEqual(to, cellStart + TimeSpan.TicksPerHour * 6);
            }
        }

        [Test]
        public void IsRaining_IsTrueInsideAShower_AndFalseJustOutsideIt()
        {
            long cell = FirstRainyCell(Seed, 1);
            RainSchedule.TryGetNaturalRain(Seed, cell, RainSchedule.ChanceFor(1), out long from, out long to);

            Assert.IsTrue(RainSchedule.IsRaining(Seed, new DateTime((from + to) / 2, DateTimeKind.Utc), Normal));

            // Hết trận mưa thì tạnh (trừ khi đúng lúc đó trận mưa của ô kế tiếp bắt đầu)
            bool nextStartsAlready = RainSchedule.TryGetNaturalRain(Seed, cell + 1, RainSchedule.ChanceFor(1), out long nextStart, out _) && nextStart <= to;
            if (!nextStartsAlready) Assert.IsFalse(RainSchedule.IsRaining(Seed, new DateTime(to, DateTimeKind.Utc), Normal));
        }

        [Test]
        public void IsRaining_AgreesWithRainDays_WhenSampledEveryMinute()
        {
            int rainyMinutes = 0;
            const int days = 30;
            for (int minute = 0; minute < days * 24 * 60; minute++)
            {
                if (RainSchedule.IsRaining(Seed, T0.AddMinutes(minute + 0.5), Normal)) rainyMinutes++;
            }

            double integrated = RainSchedule.RainDays(Seed, T0, T0.AddDays(days), Normal) * 24 * 60;
            Assert.AreEqual(integrated, rainyMinutes, 15.0, "lấy mẫu từng phút và tích phân phải khớp nhau (lệch tối đa vài phút do làm tròn đầu/cuối mỗi trận)");
        }

        [Test]
        public void CellIndex_RoundsDownForTimesBeforeTheEpoch()
        {
            long epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

            Assert.AreEqual(0, RainSchedule.CellIndex(epoch));
            Assert.AreEqual(-1, RainSchedule.CellIndex(epoch - 1));
            Assert.AreEqual(-1, RainSchedule.CellIndex(epoch - TimeSpan.TicksPerHour * 6));
            Assert.AreEqual(-2, RainSchedule.CellIndex(epoch - TimeSpan.TicksPerHour * 6 - 1));
        }

        // ---------- Mưa do người chơi gọi ----------

        [Test]
        public void ManualRain_CountsEvenWhenNaturalRainIsOff()
        {
            RainSettings off = new RainSettings { enabled = false, frequency = 1 }.WithManualRain(T0, 30);

            Assert.AreEqual(30.0 / (24 * 60), RainSchedule.RainDays(Seed, T0.AddHours(-1), T0.AddHours(2), off), 1e-9);
            Assert.IsTrue(RainSchedule.IsRaining(Seed, T0.AddMinutes(10), off));
            Assert.IsFalse(RainSchedule.IsRaining(Seed, T0.AddMinutes(31), off));
        }

        [Test]
        public void ManualRainOverlappingANaturalShower_IsNotCountedTwice()
        {
            long cell = FirstRainyCell(Seed, 1);
            RainSchedule.TryGetNaturalRain(Seed, cell, RainSchedule.ChanceFor(1), out long from, out long to);

            var settings = Normal;
            settings.manualStartTicksUtc = from;
            settings.manualUntilTicksUtc = to;   // đúng bằng trận mưa tự nhiên

            double days = RainSchedule.RainDays(Seed, new DateTime(from - TimeSpan.TicksPerHour, DateTimeKind.Utc),
                new DateTime(to + TimeSpan.TicksPerHour, DateTimeKind.Utc), settings);

            Assert.AreEqual((to - from) / (double)TimeSpan.TicksPerDay, days, 1e-9);
        }

        [Test]
        public void WithManualRain_SetsTheWindow_AndWithoutManualRainClearsIt()
        {
            RainSettings withRain = Normal.WithManualRain(T0, 30);

            Assert.IsTrue(withRain.HasManualRain);
            Assert.AreEqual(T0.AddMinutes(30).Ticks, withRain.manualUntilTicksUtc);
            Assert.IsFalse(withRain.WithoutManualRain().HasManualRain);
            Assert.IsFalse(Normal.HasManualRain, "bản gốc không bị đổi");
        }
    }

    public class RainModuleTests
    {
        const int Seed = 4242;
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        GameObject rainObject;
        GameObject mossObject;
        RainModule rain;
        MossModule moss;
        GardenEventBus bus;
        GardenContext context;

        [SetUp]
        public void SetUp()
        {
            rainObject = new GameObject("rain-test");
            rain = rainObject.AddComponent<RainModule>();
            mossObject = new GameObject("moss-test");
            moss = mossObject.AddComponent<MossModule>();

            bus = new GardenEventBus();
            context = new GardenContext(Seed, bus);
            rain.Load(GardenState.CreateNew(T0, Seed), context);
            moss.Load(GardenState.CreateNew(T0, Seed), context);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(rainObject);
            UnityEngine.Object.DestroyImmediate(mossObject);
        }

        static (long from, long to) AShower()
        {
            long start = RainSchedule.CellIndex(T0.Ticks);
            for (long cell = start; cell < start + 2000; cell++)
            {
                if (RainSchedule.TryGetNaturalRain(Seed, cell, RainSchedule.ChanceFor(1), out long from, out long to)) return (from, to);
            }
            throw new InvalidOperationException("không tìm thấy trận mưa");
        }

        static TimeWindow Around((long from, long to) shower) =>
            new TimeWindow(new DateTime(shower.from - TimeSpan.TicksPerHour, DateTimeKind.Utc), new DateTime(shower.to + TimeSpan.TicksPerHour, DateTimeKind.Utc));

        double MossAge()
        {
            moss.Save(GardenState.CreateNew(T0, Seed)).TryGetSection(MossModule.ModuleId, out MossSection section);
            return section.ageDays;
        }

        [Test]
        public void Id_IsRain() => Assert.AreEqual("rain", rain.Id);

        [Test]
        public void Save_ReturnsTheStateUnchanged_BecauseTheScheduleIsRecomputable()
        {
            GardenState state = GardenState.CreateNew(T0, Seed);

            Assert.AreEqual(0, rain.Save(state).sections.Length);
        }

        [Test]
        public void Tick_PublishesAGrowthBoost_ForTheRainInTheWindow()
        {
            var received = new List<GrowthBoost>();
            bus.Subscribe<GrowthBoost>(received.Add);
            (long from, long to) shower = AShower();

            rain.Tick(Around(shower));

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("rain", received[0].source);
            double showerDays = (shower.to - shower.from) / (double)TimeSpan.TicksPerDay;
            Assert.AreEqual(showerDays * 3.0, received[0].extraDays, 1e-9, "mưa 1 giờ thì thêm 3 giờ mọc");
        }

        [Test]
        public void Tick_PublishesNothing_WhenItDidNotRain()
        {
            rain.ApplySettings(GardenSettings.Default.WithSection(RainModule.ModuleId, new RainSettings { enabled = false, frequency = 1 }));
            var received = new List<GrowthBoost>();
            bus.Subscribe<GrowthBoost>(received.Add);

            rain.Tick(TimeWindow.FromDays(T0, 90));

            Assert.AreEqual(0, received.Count);
        }

        [Test]
        public void ManualRain_BoostsGrowth_EvenWithNaturalRainOff()
        {
            DateTime now = T0.AddHours(3);
            rain.SetClock(() => now);
            var settings = new RainSettings { enabled = false, frequency = 1 }.WithManualRain(T0, 30);
            rain.ApplySettings(GardenSettings.Default.WithSection(RainModule.ModuleId, settings));
            var received = new List<GrowthBoost>();
            bus.Subscribe<GrowthBoost>(received.Add);

            rain.Tick(new TimeWindow(T0, T0.AddHours(1)));

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(30.0 / (24 * 60) * 3.0, received[0].extraDays, 1e-9);
        }

        [Test]
        public void ApplySettings_StartsTheRainImmediately_WhenManualRainIsCalled()
        {
            rain.SetClock(() => T0);
            rain.ApplySettings(GardenSettings.Default.WithSection(RainModule.ModuleId,
                new RainSettings { enabled = false, frequency = 1 }.WithManualRain(T0.AddMinutes(-1), 30)));

            Assert.IsTrue(rain.IsRaining);
        }

        [Test]
        public void IsNotRaining_WhenEverythingIsOff()
        {
            rain.SetClock(() => T0);
            rain.ApplySettings(GardenSettings.Default.WithSection(RainModule.ModuleId, new RainSettings { enabled = false }));

            Assert.IsFalse(rain.IsRaining);
        }

        // ---------- Mưa làm rêu mọc nhanh hơn, mà hai mô-đun không biết nhau ----------

        [Test]
        public void RainMakesMossGrowFaster_ThroughTheEventBus()
        {
            (long from, long to) shower = AShower();
            double showerDays = (shower.to - shower.from) / (double)TimeSpan.TicksPerDay;

            rain.Tick(Around(shower));   // chỉ mô-đun mưa được cập nhật: rêu tự nghe sự kiện

            Assert.AreEqual(showerDays * 3.0, MossAge(), 1e-9);
        }

        // Dựng một cặp mô-đun mới (bus riêng), cho chạy theo thứ tự cho trước rồi trả về tuổi rêu
        static double MossAgeAfter(bool rainFirst, TimeWindow window)
        {
            var rainHost = new GameObject("rain-order");
            var mossHost = new GameObject("moss-order");
            try
            {
                var orderRain = rainHost.AddComponent<RainModule>();
                var orderMoss = mossHost.AddComponent<MossModule>();
                var orderContext = new GardenContext(Seed, new GardenEventBus());
                orderRain.Load(GardenState.CreateNew(T0, Seed), orderContext);
                orderMoss.Load(GardenState.CreateNew(T0, Seed), orderContext);

                if (rainFirst) { orderRain.Tick(window); orderMoss.Tick(window); }
                else { orderMoss.Tick(window); orderRain.Tick(window); }

                orderMoss.Save(GardenState.CreateNew(T0, Seed)).TryGetSection(MossModule.ModuleId, out MossSection section);
                return section.ageDays;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rainHost);
                UnityEngine.Object.DestroyImmediate(mossHost);
            }
        }

        [Test]
        public void TheOrderOfTheUpdates_DoesNotMatter()
        {
            (long from, long to) shower = AShower();
            TimeWindow window = Around(shower);
            double showerDays = (shower.to - shower.from) / (double)TimeSpan.TicksPerDay;

            double rainFirst = MossAgeAfter(true, window);
            double mossFirst = MossAgeAfter(false, window);

            Assert.AreEqual(rainFirst, mossFirst, 1e-9);
            Assert.AreEqual(window.Days + showerDays * 3.0, rainFirst, 1e-9,
                "tuổi rêu = thời gian trôi qua + phần mưa cộng thêm, dù mô-đun nào chạy trước");
        }

        [Test]
        public void ReloadingTheMossModule_DoesNotSubscribeItTwice()
        {
            moss.Load(GardenState.CreateNew(T0, Seed), context);
            moss.Load(GardenState.CreateNew(T0, Seed), context);

            bus.Publish(new GrowthBoost(2, "test"));

            Assert.AreEqual(2.0, MossAge(), 1e-9, "nạp lại nhiều lần không được làm sự kiện bị tính hai lần");
        }
    }
}
