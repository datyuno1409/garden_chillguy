using System;
using Garden.Core;
using Garden.Moss;
using Garden.Shell;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class MossSimulationTests
    {
        static readonly MossSettings Settings = MossSettings.Default;

        [Test]
        public void Tick_AddsElapsedDaysToTheAge()
        {
            var moss = new MossSimulation();

            moss.Tick(1.5);
            moss.Tick(2.0);

            Assert.AreEqual(3.5, moss.AgeDays, 1e-9);
        }

        [Test]
        public void Tick_IgnoresNegativeAndNaN()
        {
            var moss = new MossSimulation();
            moss.SetAge(4);

            moss.Tick(-3);
            moss.Tick(double.NaN);

            Assert.AreEqual(4, moss.AgeDays);
        }

        [Test]
        public void SetAge_ClampsInvalidValuesToZero()
        {
            var moss = new MossSimulation();

            moss.SetAge(-5);
            Assert.AreEqual(0, moss.AgeDays);

            moss.SetAge(double.NaN);
            Assert.AreEqual(0, moss.AgeDays);
        }

        [Test]
        public void Coverage_StartsAtStartCoverage_WhenNewGarden()
        {
            Assert.AreEqual(Settings.startCoverage, new MossSimulation().Coverage(Settings), 1e-6);
        }

        [Test]
        public void Coverage_ReachesMaxCoverage_AtFullCoverDays()
        {
            Assert.AreEqual(Settings.maxCoverage, MossSimulation.CoverageAt(Settings.daysToFullCover, Settings), 1e-6);
        }

        [Test]
        public void Coverage_StaysAtMax_WhenFarPastFullCover()
        {
            Assert.AreEqual(Settings.maxCoverage, MossSimulation.CoverageAt(10000, Settings), 1e-6);
        }

        [Test]
        public void Coverage_NeverDecreases_AsAgeGrows()
        {
            float previous = -1f;
            for (double age = 0; age <= Settings.daysToFullCover * 1.5; age += 0.25)
            {
                float coverage = MossSimulation.CoverageAt(age, Settings);
                Assert.GreaterOrEqual(coverage, previous);
                previous = coverage;
            }
        }

        [Test]
        public void Coverage_GrowsSlowlyAtFirst_ThenFaster()
        {
            float firstDay = MossSimulation.CoverageAt(1, Settings) - MossSimulation.CoverageAt(0, Settings);
            float midDay = MossSimulation.CoverageAt(Settings.daysToFullCover / 2 + 1, Settings) - MossSimulation.CoverageAt(Settings.daysToFullCover / 2, Settings);

            Assert.Less(firstDay, midDay);
        }

        [Test]
        public void Coverage_IsMax_WhenDaysToFullCoverIsNotPositive()
        {
            var broken = new MossSettings { daysToFullCover = 0f, startCoverage = 0.1f, maxCoverage = 0.8f };

            Assert.AreEqual(0.8f, MossSimulation.CoverageAt(1, broken), 1e-6);
        }
    }

    public class MossModuleTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        GameObject host;
        MossModule module;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("moss-test");
            module = host.AddComponent<MossModule>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void Id_IsMoss() => Assert.AreEqual("moss", module.Id);

        [Test]
        public void Save_ThenLoad_RestoresTheAge()
        {
            var context = new GardenContext(1, new GardenEventBus());
            module.Load(GardenState.CreateNew(T0, 1), context);
            module.Tick(TimeWindow.FromDays(T0, 6.5));
            GardenState saved = module.Save(GardenState.CreateNew(T0, 1));

            var otherHost = new GameObject("moss-test-2");
            try
            {
                var other = otherHost.AddComponent<MossModule>();
                other.Load(saved, context);
                GardenState again = other.Save(GardenState.CreateNew(T0, 1));

                again.TryGetSection(MossModule.ModuleId, out MossSection section);
                Assert.AreEqual(6.5, section.ageDays, 1e-9);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherHost);
            }
        }

        [Test]
        public void Load_StartsAtZero_WhenNothingSaved()
        {
            module.Load(GardenState.CreateNew(T0, 1), new GardenContext(1, new GardenEventBus()));

            module.Save(GardenState.CreateNew(T0, 1)).TryGetSection(MossModule.ModuleId, out MossSection section);
            Assert.AreEqual(0, section.ageDays);
        }

        [Test]
        public void MossSection_ReadsAStateMigratedFromVersion1()
        {
            string v1 = "{\"version\":1,\"seed\":9,\"createdTicksUtc\":" + T0.Ticks + ",\"lastSeenTicksUtc\":" + T0.Ticks + ",\"mossAgeDays\":4.5}";
            GardenStateMigrator.TryParse(v1, out GardenState migrated);

            Assert.IsTrue(migrated.TryGetSection(MossModule.ModuleId, out MossSection section), "tên mục và tên field của rêu phải khớp với bước nâng cấp file");
            Assert.AreEqual(4.5, section.ageDays, 1e-9);
        }

        [Test]
        public void PreviewAge_DoesNotThrow_WithoutAView()
        {
            Assert.DoesNotThrow(() => module.PreviewAge(7));
        }
    }

    public class PipHitTestTests
    {
        // Cửa sổ 400x400, mép 6px, thanh tiêu đề cao 28px, vùng nút rộng 114px bên phải
        static readonly PipLayout Layout = new PipLayout(6f, 28f, 114f);
        const float W = 400f, H = 400f;

        static PipHit At(float x, float y) => PipHitTest.Classify(x, y, W, H, Layout);

        [Test]
        public void Center_IsGarden() => Assert.AreEqual(PipRegion.Garden, At(200, 200).region);

        [Test]
        public void OutsideTheWindow_IsOutside()
        {
            Assert.AreEqual(PipRegion.Outside, At(-1, 200).region);
            Assert.AreEqual(PipRegion.Outside, At(200, 400).region);
            Assert.AreEqual(PipRegion.Outside, At(400, 200).region);
        }

        [Test]
        public void TitleBar_LeftOfTheButtons_IsDragArea() => Assert.AreEqual(PipRegion.TitleBarDrag, At(100, 390).region);

        [Test]
        public void TitleBar_OverTheButtons_IsButtons() => Assert.AreEqual(PipRegion.TitleBarButtons, At(350, 390).region);

        [Test]
        public void Buttons_WinOverTheRightEdge_SoTheCloseButtonStaysClickable()
        {
            Assert.AreEqual(PipRegion.TitleBarButtons, At(399, 390).region);
            Assert.AreEqual(PipRegion.TitleBarButtons, At(350, 399).region);
        }

        [Test]
        public void Edges_AreResizeAreas()
        {
            Assert.AreEqual(PipEdge.Left, At(2, 200).edges);
            Assert.AreEqual(PipEdge.Right, At(397, 200).edges);
            Assert.AreEqual(PipEdge.Bottom, At(200, 2).edges);
            Assert.AreEqual(PipRegion.ResizeEdge, At(2, 200).region);
        }

        [Test]
        public void TopEdge_BeatsTheTitleBarDrag()
        {
            PipHit hit = At(100, 398);

            Assert.AreEqual(PipRegion.ResizeEdge, hit.region);
            Assert.AreEqual(PipEdge.Top, hit.edges);
        }

        [Test]
        public void Corners_CombineTwoEdges()
        {
            Assert.AreEqual(PipEdge.Left | PipEdge.Bottom, At(1, 1).edges);
            Assert.AreEqual(PipEdge.Left | PipEdge.Top, At(1, 398).edges);
            Assert.AreEqual(PipEdge.Right | PipEdge.Bottom, At(398, 1).edges);
        }

        [Test]
        public void Garden_IsTheOnlyRegionThatReceivesGardenClicks()
        {
            int gardenPixels = 0;
            for (int x = 0; x < 400; x += 4)
            {
                for (int y = 0; y < 400; y += 4)
                {
                    if (At(x, y).region == PipRegion.Garden) gardenPixels++;
                }
            }
            Assert.Greater(gardenPixels, 0);
            Assert.AreEqual(PipRegion.Garden, At(200, 100).region);
            Assert.AreNotEqual(PipRegion.Garden, At(100, 390).region);
            Assert.AreNotEqual(PipRegion.Garden, At(350, 390).region);
            Assert.AreNotEqual(PipRegion.Garden, At(2, 200).region);
        }

        [Test]
        public void WithoutATitleBar_TheTopIsGarden()
        {
            var noBar = new PipLayout(6f, 0f, 0f);

            Assert.AreEqual(PipRegion.Garden, PipHitTest.Classify(200, 380, W, H, noBar).region);
        }
    }
}
