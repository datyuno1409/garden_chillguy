using System;
using System.Collections.Generic;
using Garden.Core;
using Garden.Layout;
using Garden.Moss;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class LayoutCatalogTests
    {
        [Test]
        public void EverySlot_HasAUniqueNonEmptyIdAndALabel()
        {
            var seen = new HashSet<string>();
            foreach (LayoutSlotInfo slot in LayoutCatalog.Slots)
            {
                Assert.IsFalse(string.IsNullOrEmpty(slot.id));
                Assert.IsFalse(string.IsNullOrEmpty(slot.label));
                Assert.IsTrue(seen.Add(slot.id), "trùng id: " + slot.id);
            }
        }

        [Test]
        public void TheMainRock_CanBeReshuffledButNeverHidden()
        {
            Assert.IsTrue(LayoutCatalog.TryGet(LayoutCatalog.MainRock, out LayoutSlotInfo info));
            Assert.IsTrue(info.canReshuffle);
            Assert.IsFalse(info.canHide);
        }

        [Test]
        public void EverySmallRockIdTheBuilderUses_IsInTheCatalog()
        {
            for (int i = 0; i < LayoutCatalog.SmallRockCount; i++)
                Assert.IsTrue(LayoutCatalog.TryGet(LayoutCatalog.SmallRock(i), out _), LayoutCatalog.SmallRock(i));
        }

        [Test]
        public void TryGet_ReturnsFalse_ForAnUnknownId() => Assert.IsFalse(LayoutCatalog.TryGet("nothing", out _));

        [Test]
        public void TheGroupSlots_CanOnlyBeHidden()
        {
            foreach (string id in new[] { LayoutCatalog.Mushrooms, LayoutCatalog.Grass, LayoutCatalog.Leaves })
            {
                LayoutCatalog.TryGet(id, out LayoutSlotInfo info);
                Assert.IsTrue(info.canHide, id);
                Assert.IsFalse(info.canReshuffle, id);
            }
        }
    }

    public class LayoutSettingsTests
    {
        [Test]
        public void Get_ReturnsTheDefault_WhenNothingWasChosen()
        {
            SlotSetting setting = default(LayoutSettings).Get("rock-1");

            Assert.AreEqual("rock-1", setting.id);
            Assert.AreEqual(0, setting.seed);
            Assert.IsFalse(setting.hidden);
        }

        [Test]
        public void With_AddsThenReplacesTheSameId()
        {
            LayoutSettings layout = default(LayoutSettings)
                .With(new SlotSetting { id = "a", seed = 5 })
                .With(new SlotSetting { id = "a", seed = 9, hidden = true });

            Assert.AreEqual(1, layout.slots.Length);
            Assert.AreEqual(9, layout.Get("a").seed);
            Assert.IsTrue(layout.Get("a").hidden);
        }

        [Test]
        public void With_DoesNotMutateTheOriginal()
        {
            LayoutSettings original = default(LayoutSettings).With(new SlotSetting { id = "a", seed = 1 });

            original.With(new SlotSetting { id = "b", seed = 2 });

            Assert.AreEqual(1, original.slots.Length);
        }

        [Test]
        public void TheSettingsSurviveTheJsonRoundTrip()
        {
            GardenSettings settings = GardenSettings.Default.WithSection(GardenLayout.ModuleId,
                default(LayoutSettings).With(new SlotSetting { id = "rock-2", seed = 321, hidden = true }));

            settings.TryGetSection(GardenLayout.ModuleId, out LayoutSettings restored);

            Assert.AreEqual(321, restored.Get("rock-2").seed);
            Assert.IsTrue(restored.Get("rock-2").hidden);
        }
    }

    public class GardenLayoutTests
    {
        sealed class FakeVariant : MonoBehaviour, ILayoutVariant
        {
            public readonly List<int> seeds = new List<int>();
            public void SetVariantSeed(int seed) { seeds.Add(seed); }
        }

        GameObject root;
        GardenLayout layout;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("layout-root");
            layout = root.AddComponent<GardenLayout>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

        (GameObject go, LayoutSlot slot, FakeVariant variant) AddSlot(string id, int defaultSeed)
        {
            var go = new GameObject(id);
            go.transform.SetParent(root.transform, false);
            LayoutSlot slot = go.AddComponent<LayoutSlot>();
            slot.Configure(id, defaultSeed);
            return (go, slot, go.AddComponent<FakeVariant>());
        }

        static GardenSettings With(params SlotSetting[] choices)
        {
            LayoutSettings layoutSettings = default;
            foreach (SlotSetting choice in choices) layoutSettings = layoutSettings.With(choice);
            return GardenSettings.Default.WithSection(GardenLayout.ModuleId, layoutSettings);
        }

        [Test]
        public void NoChoices_KeepsTheOriginalLayout()
        {
            var small = AddSlot("rock-1", 7);

            layout.ApplySettings(GardenSettings.Default);

            Assert.IsTrue(small.go.activeSelf);
            Assert.AreEqual(0, small.variant.seeds.Count);
        }

        [Test]
        public void AHideableSlot_CanBeHiddenAndShownAgain()
        {
            var small = AddSlot("rock-1", 7);

            layout.ApplySettings(With(new SlotSetting { id = "rock-1", hidden = true }));
            Assert.IsFalse(small.go.activeSelf);

            layout.ApplySettings(With(new SlotSetting { id = "rock-1", hidden = false }));
            Assert.IsTrue(small.go.activeSelf);
        }

        [Test]
        public void TheMainRock_CannotBeHidden()
        {
            var main = AddSlot(LayoutCatalog.MainRock, 3);

            layout.ApplySettings(With(new SlotSetting { id = LayoutCatalog.MainRock, hidden = true }));

            Assert.IsTrue(main.go.activeSelf);
        }

        [Test]
        public void AReshuffledRock_ReceivesTheChosenSeed()
        {
            var main = AddSlot(LayoutCatalog.MainRock, 3);

            layout.ApplySettings(With(new SlotSetting { id = LayoutCatalog.MainRock, seed = 555 }));

            CollectionAssert.AreEqual(new[] { 555 }, main.variant.seeds);
        }

        [Test]
        public void GoingBackToSeedZero_RestoresTheDefaultShape()
        {
            var main = AddSlot(LayoutCatalog.MainRock, 3);
            layout.ApplySettings(With(new SlotSetting { id = LayoutCatalog.MainRock, seed = 555 }));

            layout.ApplySettings(With(new SlotSetting { id = LayoutCatalog.MainRock, seed = 0 }));

            CollectionAssert.AreEqual(new[] { 555, 3 }, main.variant.seeds);
        }

        [Test]
        public void AnUnchangedSeed_IsNotRebuiltAgain()
        {
            var main = AddSlot(LayoutCatalog.MainRock, 3);
            GardenSettings choice = With(new SlotSetting { id = LayoutCatalog.MainRock, seed = 555 });

            layout.ApplySettings(choice);
            layout.ApplySettings(choice);   // ví dụ người chơi chỉnh mưa: bố cục không đổi
            layout.ApplySettings(GardenSettings.Default.WithSection("rain", new SlotSetting()).WithSection(GardenLayout.ModuleId,
                default(LayoutSettings).With(new SlotSetting { id = LayoutCatalog.MainRock, seed = 555 })));

            Assert.AreEqual(1, main.variant.seeds.Count, "dựng lại hình đá tốn kém nên chỉ làm khi hạt giống thật sự đổi");
        }

        [Test]
        public void ASlotThatCannotBeReshuffled_IgnoresASeed()
        {
            var group = AddSlot(LayoutCatalog.Mushrooms, 0);

            layout.ApplySettings(With(new SlotSetting { id = LayoutCatalog.Mushrooms, seed = 99 }));

            Assert.AreEqual(0, group.variant.seeds.Count);
        }

        [Test]
        public void AnUnknownSlot_IsLeftAlone()
        {
            var stranger = AddSlot("not-in-catalog", 1);

            layout.ApplySettings(With(new SlotSetting { id = "not-in-catalog", hidden = true, seed = 5 }));

            Assert.IsTrue(stranger.go.activeSelf);
            Assert.AreEqual(0, stranger.variant.seeds.Count);
        }

        [Test]
        public void HiddenSlots_AreStillFoundWhenShownAgain()
        {
            var small = AddSlot("rock-2", 1);
            layout.ApplySettings(With(new SlotSetting { id = "rock-2", hidden = true }));

            layout.ApplySettings(GardenSettings.Default);   // đặt lại bố cục gốc

            Assert.IsTrue(small.go.activeSelf);
        }
    }

    public class MossDesignTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        GameObject host;
        MossModule module;
        MossGrowth view;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("moss-design");
            view = host.AddComponent<MossGrowth>();
            module = host.AddComponent<MossModule>();
            module.Configure(view);
            module.Load(GardenState.CreateNew(T0, 1), new GardenContext(1, new GardenEventBus()));
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void WithoutDesignSettings_TheSceneDefaultsApply()
        {
            module.PreviewAge(1000);

            Assert.AreEqual(MossSettings.Default.maxCoverage, view.Coverage, 1e-5);
        }

        [Test]
        public void ADesignedMaxCoverage_ReplacesTheDefault()
        {
            module.PreviewAge(1000);

            module.ApplySettings(GardenSettings.Default.WithSection(MossModule.ModuleId, new MossDesign { daysToFullCover = 14f, maxCoverage = 0.8f }));

            Assert.AreEqual(0.8f, view.Coverage, 1e-5);
        }

        [Test]
        public void AFasterGrowthSpeed_CoversMoreAtTheSameAge()
        {
            module.PreviewAge(7);
            float normal = view.Coverage;

            module.ApplySettings(GardenSettings.Default.WithSection(MossModule.ModuleId, new MossDesign { daysToFullCover = 7f, maxCoverage = 0.5f }));

            Assert.Greater(view.Coverage, normal);
        }

        [Test]
        public void RemovingTheDesignSettings_BringsTheDefaultsBack()
        {
            module.PreviewAge(1000);
            module.ApplySettings(GardenSettings.Default.WithSection(MossModule.ModuleId, new MossDesign { daysToFullCover = 14f, maxCoverage = 0.8f }));

            module.ApplySettings(GardenSettings.Default);

            Assert.AreEqual(MossSettings.Default.maxCoverage, view.Coverage, 1e-5);
        }
    }
}
