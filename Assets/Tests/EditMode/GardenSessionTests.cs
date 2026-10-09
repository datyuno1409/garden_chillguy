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
    // Mô-đun giả: cộng dồn số ngày đã trôi qua vào phần dữ liệu của chính nó
    sealed class FakeModule : IGardenModule, IAgePreviewable
    {
        [Serializable] public struct Data { public double total; }

        readonly string id;
        public double total;
        public int tickCount;
        public double lastPreview = -1;
        public GardenContext context;

        public FakeModule(string id) { this.id = id; }
        public string Id => id;

        public void Load(GardenState state, GardenContext ctx)
        {
            context = ctx;
            total = state.TryGetSection(id, out Data data) ? data.total : 0;
        }

        public GardenState Save(GardenState state) => state.WithSection(id, new Data { total = total });

        public void Tick(double elapsedDays)
        {
            total += elapsedDays;
            tickCount++;
        }

        public void PreviewAge(double ageDays) { lastPreview = ageDays; total = ageDays; }
    }

    sealed class ThrowingModule : IGardenModule
    {
        public string Id => "broken";
        public void Load(GardenState state, GardenContext context) => throw new InvalidOperationException("load boom");
        public GardenState Save(GardenState state) => throw new InvalidOperationException("save boom");
        public void Tick(double elapsedDays) => throw new InvalidOperationException("tick boom");
    }

    // Mô-đun giả đọc phần "moss" theo đúng định dạng của rêu, để thử việc nâng cấp file bản 1
    sealed class MossReaderModule : IGardenModule
    {
        [Serializable] public struct Data { public double ageDays; }
        public double age;
        public string Id => "moss";
        public void Load(GardenState state, GardenContext context) => age = state.TryGetSection(Id, out Data d) ? d.ageDays : -1;
        public GardenState Save(GardenState state) => state.WithSection(Id, new Data { ageDays = age });
        public void Tick(double elapsedDays) { age += elapsedDays; }
    }

    public class GardenSessionTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        string directory;
        string path;
        DateTime now;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "garden-session-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "state.json");
            now = T0;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        GardenSession NewSession(params IGardenModule[] modules) => new GardenSession(path, modules, () => now);

        // ---------- Một file, mỗi mô-đun một phần ----------

        [Test]
        public void Start_WritesOneFileWithASectionPerModule()
        {
            NewSession(new FakeModule("moss"), new FakeModule("vines")).Start(7);

            GardenState saved = GardenStateStore.Load(path, T0, 1).state;
            Assert.AreEqual(2, saved.sections.Length);
            Assert.AreEqual(7, saved.seed);
        }

        [Test]
        public void TwoModules_DoNotOverwriteEachOther()
        {
            var mossA = new FakeModule("moss");
            var vinesA = new FakeModule("vines");
            GardenSession first = NewSession(mossA, vinesA);
            first.Start(1);
            mossA.total = 5;
            vinesA.total = 9;
            first.Save();

            var mossB = new FakeModule("moss");
            var vinesB = new FakeModule("vines");
            NewSession(mossB, vinesB).Start(1);

            Assert.AreEqual(5, mossB.total);
            Assert.AreEqual(9, vinesB.total);
        }

        [Test]
        public void Modules_ReceiveTheGardenSeed()
        {
            var module = new FakeModule("moss");

            NewSession(module).Start(1234);

            Assert.AreEqual(1234, module.context.Seed);
        }

        [Test]
        public void SectionsOfUnknownModules_ArePreservedWhenSaving()
        {
            GardenStateStore.Save(path, GardenState.CreateNew(T0, 3).WithSection("future-module", new GardenStateTests_Sample { n = 99 }));

            GardenSession session = NewSession(new FakeModule("moss"));
            session.Start(1);
            session.Save();

            GardenState saved = GardenStateStore.Load(path, T0, 1).state;
            Assert.IsTrue(saved.TryGetSection("future-module", out GardenStateTests_Sample sample), "dữ liệu của mô-đun chưa nạp phải được giữ nguyên");
            Assert.AreEqual(99, sample.n);
        }

        // ---------- Thời gian ----------

        [Test]
        public void Start_TicksModulesForTheTimeTheAppWasClosed()
        {
            NewSession(new FakeModule("moss")).Start(1);

            now = T0.AddDays(3);
            var module = new FakeModule("moss");
            NewSession(module).Start(1);

            Assert.AreEqual(3.0, module.total, 1e-9);
        }

        [Test]
        public void Start_DoesNotTick_WhenClockWentBackwards()
        {
            NewSession(new FakeModule("moss")).Start(1);

            now = T0.AddDays(-2);
            var module = new FakeModule("moss");
            NewSession(module).Start(1);

            Assert.AreEqual(0.0, module.total);
            Assert.AreEqual(0, module.tickCount);
        }

        [Test]
        public void Refresh_TicksOnlyTheTimeSinceTheLastTick()
        {
            var module = new FakeModule("moss");
            GardenSession session = NewSession(module);
            session.Start(1);

            now = T0.AddDays(1);
            session.Refresh();
            now = T0.AddDays(1.5);
            session.Refresh();

            Assert.AreEqual(1.5, module.total, 1e-9);
        }

        [Test]
        public void Save_RunsPendingTimeFirst_SoNothingIsLostAcrossARestart()
        {
            var first = new FakeModule("moss");
            GardenSession session = NewSession(first);
            session.Start(1);

            now = T0.AddDays(2);
            session.Save();   // chưa gọi Refresh trước đó

            var second = new FakeModule("moss");
            NewSession(second).Start(1);

            Assert.AreEqual(2.0, second.total, 1e-9);
        }

        // ---------- Xem thử ----------

        [Test]
        public void PreviewStart_NeverWritesTheFile()
        {
            GardenSession session = NewSession(new FakeModule("moss"));

            session.Start(1, preview: true);
            session.Save();

            Assert.IsFalse(File.Exists(path));
        }

        [Test]
        public void PreviewAge_CallsPreviewableModules_AndStopsSaving()
        {
            var module = new FakeModule("moss");
            GardenSession session = NewSession(module);
            session.Start(1);
            File.Delete(path);

            session.PreviewAge(7);
            session.Save();

            Assert.AreEqual(7, module.lastPreview);
            Assert.IsFalse(File.Exists(path));
        }

        // ---------- An toàn ----------

        [Test]
        public void DuplicateModuleIds_Throw()
        {
            Assert.Throws<ArgumentException>(() => NewSession(new FakeModule("moss"), new FakeModule("moss")));
        }

        [Test]
        public void ABrokenModule_DoesNotStopTheOthers()
        {
            LogAssert.Expect(LogType.Error, new Regex("broken.*Load"));
            LogAssert.Expect(LogType.Error, new Regex("broken.*lưu"));

            var healthy = new FakeModule("moss");
            GardenSession session = NewSession(new ThrowingModule(), healthy);
            session.Start(1);

            GardenState saved = GardenStateStore.Load(path, T0, 1).state;
            Assert.IsTrue(saved.TryGetSection("moss", out FakeModule.Data _), "mô-đun lành vẫn phải được lưu");
        }

        // ---------- Nâng cấp file cũ ----------

        [Test]
        public void ALegacyVersion1File_KeepsTheMossAge_AndIsRewrittenAsCurrentVersion()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"version\":1,\"seed\":9,\"createdTicksUtc\":" + T0.Ticks + ",\"lastSeenTicksUtc\":" + T0.Ticks + ",\"mossAgeDays\":4.5}");

            var moss = new MossReaderModule();
            GardenSession session = NewSession(moss);
            session.Start(1);

            Assert.AreEqual(LoadOutcome.Migrated, session.LastLoadOutcome);
            Assert.AreEqual(4.5, moss.age, 1e-9, "vườn đang mọc rêu của người chơi không được mất khi nâng cấp định dạng");
            Assert.AreEqual(9, session.State.seed);
            Assert.AreEqual(GardenState.CurrentVersion, GardenStateStore.Load(path, T0, 1).state.version);
            Assert.IsTrue(File.Exists(path + GardenStateStore.MigratedBackupSuffix));
        }
    }

    public class GardenEventBusTests
    {
        struct Rain { public bool raining; }
        struct Other { }

        [Test]
        public void Publish_DeliversToSubscribers()
        {
            var bus = new GardenEventBus();
            var received = new List<bool>();
            bus.Subscribe<Rain>(e => received.Add(e.raining));

            bus.Publish(new Rain { raining = true });

            CollectionAssert.AreEqual(new[] { true }, received);
        }

        [Test]
        public void Publish_OnlyDeliversTheMatchingEventType()
        {
            var bus = new GardenEventBus();
            int count = 0;
            bus.Subscribe<Rain>(_ => count++);

            bus.Publish(new Other());

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var bus = new GardenEventBus();
            int count = 0;
            Action<Rain> handler = _ => count++;
            bus.Subscribe(handler);
            bus.Unsubscribe(handler);

            bus.Publish(new Rain());

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNothing()
        {
            Assert.DoesNotThrow(() => new GardenEventBus().Publish(new Rain()));
        }

        [Test]
        public void AFailingSubscriber_DoesNotStopTheOthers()
        {
            LogAssert.Expect(LogType.Error, new Regex("GardenEventBus.*Rain"));
            var bus = new GardenEventBus();
            int count = 0;
            bus.Subscribe<Rain>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<Rain>(_ => count++);

            bus.Publish(new Rain());

            Assert.AreEqual(1, count);
        }

        [Test]
        public void ASubscriberMayUnsubscribeWhileHandling()
        {
            var bus = new GardenEventBus();
            int count = 0;
            Action<Rain> once = null;
            once = _ => { count++; bus.Unsubscribe(once); };
            bus.Subscribe(once);

            bus.Publish(new Rain());
            bus.Publish(new Rain());

            Assert.AreEqual(1, count);
        }
    }

    public class ClickDispatcherTests
    {
        sealed class Recorder : IGardenClickHandler
        {
            public int count;
            public Vector3 lastPoint;
            public void OnGardenClick(GardenClick click) { count++; lastPoint = click.point; }
        }

        sealed class Failing : IGardenClickHandler
        {
            public void OnGardenClick(GardenClick click) => throw new InvalidOperationException("boom");
        }

        [Test]
        public void Dispatch_NotifiesEveryHandler_WithTheClick()
        {
            var a = new Recorder();
            var b = new Recorder();

            int notified = ClickDispatcher.Dispatch(new IGardenClickHandler[] { a, b }, new GardenClick(new Vector3(1, 2, 3), Vector3.up, null));

            Assert.AreEqual(2, notified);
            Assert.AreEqual(new Vector3(1, 2, 3), a.lastPoint);
            Assert.AreEqual(1, b.count);
        }

        [Test]
        public void Dispatch_WithNoHandlers_ReturnsZero()
        {
            Assert.AreEqual(0, ClickDispatcher.Dispatch(new IGardenClickHandler[0], new GardenClick(Vector3.zero, Vector3.up, null)));
        }

        [Test]
        public void AFailingHandler_DoesNotStopTheOthers()
        {
            LogAssert.Expect(LogType.Error, new Regex("ClickDispatcher.*Failing"));
            var healthy = new Recorder();

            ClickDispatcher.Dispatch(new IGardenClickHandler[] { new Failing(), healthy }, new GardenClick(Vector3.zero, Vector3.up, null));

            Assert.AreEqual(1, healthy.count);
        }
    }
}
