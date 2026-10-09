using System;
using System.Collections.Generic;
using Garden.Core;
using Garden.Moss;
using NUnit.Framework;
using UnityEngine;

namespace Garden.Tests
{
    public class MossDamageTests
    {
        const float Radius = 0.5f;
        static readonly Vector3 Origin = Vector3.zero;

        [Test]
        public void AddHit_CreatesAFullStrengthHit()
        {
            var damage = new MossDamage();

            damage.AddHit(new Vector3(1, 2, 3), Radius);

            Assert.AreEqual(1, damage.Hits.Count);
            Assert.AreEqual(new Vector3(1, 2, 3), damage.Hits[0].Position);
            Assert.AreEqual(Radius, damage.Hits[0].radius);
            Assert.AreEqual(1f, damage.Hits[0].strength);
        }

        [Test]
        public void AddHit_FarFromExistingHits_CreatesASeparateHit()
        {
            var damage = new MossDamage();

            damage.AddHit(Origin, Radius);
            damage.AddHit(new Vector3(3, 0, 0), Radius);

            Assert.AreEqual(2, damage.Hits.Count);
        }

        [Test]
        public void AddHit_NearAnExistingHit_MergesWidensAndResetsTheDamage()
        {
            var damage = new MossDamage();
            damage.AddHit(Origin, Radius);
            damage.Tick(1, 2f);   // đã lành được nửa chừng

            damage.AddHit(new Vector3(0.05f, 0, 0), Radius);

            Assert.AreEqual(1, damage.Hits.Count);
            Assert.AreEqual(Radius * 1.2f, damage.Hits[0].radius, 1e-5);
            Assert.AreEqual(1f, damage.Hits[0].strength, "click lại thì hỏng trở lại như mới");
            Assert.AreEqual(Origin, damage.Hits[0].Position, "vết giữ nguyên vị trí gốc");
        }

        [Test]
        public void RepeatedClicks_NeverWidenBeyondTwiceTheOriginalRadius()
        {
            var damage = new MossDamage();

            for (int i = 0; i < 30; i++) damage.AddHit(Origin, Radius);

            Assert.AreEqual(1, damage.Hits.Count);
            Assert.AreEqual(Radius * 2f, damage.Hits[0].radius, 1e-5);
        }

        [Test]
        public void AddHit_IgnoresInvalidRadius()
        {
            var damage = new MossDamage();

            damage.AddHit(Origin, 0f);
            damage.AddHit(Origin, -1f);
            damage.AddHit(Origin, float.NaN);
            damage.AddHit(Origin, float.PositiveInfinity);

            Assert.AreEqual(0, damage.Hits.Count);
        }

        [Test]
        public void WhenFull_TheWeakestHitIsReplaced()
        {
            var saved = new List<MossHit>();
            for (int i = 0; i < MossDamage.Capacity; i++)
                saved.Add(new MossHit(new Vector3(i * 5f, 0, 0), Radius, Radius * 2, i == 5 ? 0.2f : 0.9f));
            var damage = new MossDamage();
            damage.Load(saved.ToArray());

            damage.AddHit(new Vector3(500, 0, 0), Radius);

            Assert.AreEqual(MossDamage.Capacity, damage.Hits.Count);
            Assert.IsFalse(Contains(damage, new Vector3(25f, 0, 0)), "vết yếu nhất phải bị thay");
            Assert.IsTrue(Contains(damage, new Vector3(500, 0, 0)));
        }

        [Test]
        public void TheCountNeverExceedsCapacity()
        {
            var damage = new MossDamage();

            for (int i = 0; i < MossDamage.Capacity * 3; i++) damage.AddHit(new Vector3(i * 10f, 0, 0), Radius);

            Assert.AreEqual(MossDamage.Capacity, damage.Hits.Count);
        }

        // ---------- Rêu mọc lại ----------

        [Test]
        public void Tick_HealsLinearly()
        {
            var damage = new MossDamage();
            damage.AddHit(Origin, Radius);

            damage.Tick(1, 2f);

            Assert.AreEqual(0.5f, damage.Hits[0].strength, 1e-5);
        }

        [Test]
        public void Tick_RemovesAHitOnceFullyHealed()
        {
            var damage = new MossDamage();
            damage.AddHit(Origin, Radius);

            damage.Tick(2, 2f);

            Assert.AreEqual(0, damage.Hits.Count);
        }

        [Test]
        public void Tick_WithNoHealDays_ClearsEverything()
        {
            var damage = new MossDamage();
            damage.AddHit(Origin, Radius);

            damage.Tick(0.001, 0f);

            Assert.AreEqual(0, damage.Hits.Count);
        }

        [Test]
        public void Tick_IgnoresNegativeAndNaNTime()
        {
            var damage = new MossDamage();
            damage.AddHit(Origin, Radius);

            damage.Tick(-3, 2f);
            damage.Tick(double.NaN, 2f);

            Assert.AreEqual(1f, damage.Hits[0].strength);
        }

        // ---------- Lưu và nạp ----------

        [Test]
        public void ToArrayThenLoad_RoundTrips()
        {
            var original = new MossDamage();
            original.AddHit(new Vector3(1, 2, 3), Radius);
            original.AddHit(new Vector3(9, 9, 9), 0.7f);
            original.Tick(0.5, 2f);

            var restored = new MossDamage();
            restored.Load(original.ToArray());

            Assert.AreEqual(2, restored.Hits.Count);
            Assert.AreEqual(original.Hits[1].Position, restored.Hits[1].Position);
            Assert.AreEqual(original.Hits[1].strength, restored.Hits[1].strength, 1e-6);
            Assert.AreEqual(original.Hits[1].maxRadius, restored.Hits[1].maxRadius, 1e-6);
        }

        [Test]
        public void Load_Null_ClearsEverything()
        {
            var damage = new MossDamage();
            damage.AddHit(Origin, Radius);

            damage.Load(null);

            Assert.AreEqual(0, damage.Hits.Count);
        }

        [Test]
        public void Load_DropsInvalidHits_AndClampsStrength()
        {
            var damage = new MossDamage();
            damage.Load(new[]
            {
                new MossHit(new Vector3(float.NaN, 0, 0), Radius, Radius * 2, 1f),   // vị trí hỏng
                new MossHit(Origin, 0f, 1f, 1f),                                      // bán kính không hợp lệ
                new MossHit(Origin, Radius, Radius * 2, 0f),                          // đã lành
                new MossHit(Origin, Radius, Radius * 2, float.NaN),                   // mức hỏng hỏng
                new MossHit(new Vector3(5, 0, 0), Radius, Radius * 2, 7f),            // quá 1: phải kẹp lại
            });

            Assert.AreEqual(1, damage.Hits.Count);
            Assert.AreEqual(1f, damage.Hits[0].strength);
        }

        [Test]
        public void Load_TruncatesToCapacity()
        {
            var many = new MossHit[MossDamage.Capacity + 10];
            for (int i = 0; i < many.Length; i++) many[i] = new MossHit(new Vector3(i, 0, 0), Radius, Radius * 2, 1f);

            var damage = new MossDamage();
            damage.Load(many);

            Assert.AreEqual(MossDamage.Capacity, damage.Hits.Count);
        }

        [Test]
        public void Load_FillsAMissingMaxRadius()
        {
            var damage = new MossDamage();
            damage.Load(new[] { new MossHit(Origin, Radius, 0f, 1f) });   // file cũ không có maxRadius

            Assert.AreEqual(Radius * 2f, damage.Hits[0].maxRadius, 1e-5);
        }

        static bool Contains(MossDamage damage, Vector3 position)
        {
            foreach (MossHit hit in damage.Hits)
            {
                if (Vector3.Distance(hit.Position, position) < 1e-3f) return true;
            }
            return false;
        }
    }

    public class MossModuleClickTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        GameObject root;
        GameObject rock;
        GameObject stranger;
        MossModule module;
        GardenEventBus bus;
        GardenContext context;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("moss-root");
            module = root.AddComponent<MossModule>();
            rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.transform.SetParent(root.transform, false);
            stranger = GameObject.CreatePrimitive(PrimitiveType.Cube);   // vật khác nằm ngoài nhóm rêu

            bus = new GardenEventBus();
            context = new GardenContext(1, bus);
            module.Load(GardenState.CreateNew(T0, 1), context);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(stranger);
        }

        GardenClick ClickOn(GameObject target, Vector3 point) => new GardenClick(point, Vector3.up, target.GetComponent<Collider>());

        [Test]
        public void ClickOnARock_AddsAHit()
        {
            module.OnGardenClick(ClickOn(rock, new Vector3(0, 0.5f, 0)));

            Assert.AreEqual(1, module.Damage.Hits.Count);
            Assert.AreEqual(new Vector3(0, 0.5f, 0), module.Damage.Hits[0].Position);
        }

        [Test]
        public void ClickOnARock_PublishesADisturbance()
        {
            var received = new List<GardenDisturbance>();
            bus.Subscribe<GardenDisturbance>(received.Add);

            module.OnGardenClick(ClickOn(rock, new Vector3(0, 0.5f, 0)));

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(MossModule.ModuleId, received[0].source);
            Assert.AreEqual(new Vector3(0, 0.5f, 0), received[0].point);
        }

        [Test]
        public void ClickOnSomethingElse_IsIgnored()
        {
            var received = new List<GardenDisturbance>();
            bus.Subscribe<GardenDisturbance>(received.Add);

            module.OnGardenClick(ClickOn(stranger, Vector3.zero));

            Assert.AreEqual(0, module.Damage.Hits.Count);
            Assert.AreEqual(0, received.Count);
        }

        [Test]
        public void ClickWithoutACollider_IsIgnored()
        {
            module.OnGardenClick(new GardenClick(Vector3.zero, Vector3.up, null));

            Assert.AreEqual(0, module.Damage.Hits.Count);
        }

        [Test]
        public void Hits_SurviveSaveAndLoad()
        {
            module.OnGardenClick(ClickOn(rock, new Vector3(0, 0.5f, 0)));
            GardenState saved = module.Save(GardenState.CreateNew(T0, 1));

            var otherRoot = new GameObject("moss-root-2");
            try
            {
                var other = otherRoot.AddComponent<MossModule>();
                other.Load(saved, context);

                Assert.AreEqual(1, other.Damage.Hits.Count);
                Assert.AreEqual(new Vector3(0, 0.5f, 0), other.Damage.Hits[0].Position);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherRoot);
            }
        }

        [Test]
        public void Hits_HealWithTime_AndAreGoneFromTheNextSave()
        {
            module.OnGardenClick(ClickOn(rock, new Vector3(0, 0.5f, 0)));

            module.Tick(MossSettings.Default.healDays + 1);

            GardenState saved = module.Save(GardenState.CreateNew(T0, 1));
            saved.TryGetSection(MossModule.ModuleId, out MossSection section);
            Assert.AreEqual(0, section.hits.Length);
        }

        [Test]
        public void AnOldSectionWithoutHits_LoadsFine()
        {
            GardenState state = GardenState.CreateNew(T0, 1);
            state.sections = new[] { new GardenSection { id = MossModule.ModuleId, json = "{\"ageDays\":3.5}" } };

            Assert.DoesNotThrow(() => module.Load(state, context));
            Assert.AreEqual(0, module.Damage.Hits.Count);
        }

        [Test]
        public void ClickBeforeLoad_DoesNotThrow()
        {
            var freshRoot = new GameObject("moss-fresh");
            try
            {
                var fresh = freshRoot.AddComponent<MossModule>();
                var child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                child.transform.SetParent(freshRoot.transform, false);

                Assert.DoesNotThrow(() => fresh.OnGardenClick(ClickOn(child, Vector3.zero)));
                Assert.AreEqual(1, fresh.Damage.Hits.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(freshRoot);
            }
        }

        [Test]
        public void DefaultSettings_HaveAUsableHitRadiusAndHealTime()
        {
            Assert.Greater(MossSettings.Default.hitRadius, 0f);
            Assert.Greater(MossSettings.Default.healDays, 0f);
        }
    }
}
