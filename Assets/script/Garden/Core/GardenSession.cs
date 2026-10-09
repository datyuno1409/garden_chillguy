using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden.Core
{
    // Điều phối một "lần chơi": đọc file lưu (nâng cấp nếu là bản cũ), cho các mô-đun nạp dữ liệu và chạy phần thời gian
    // đã trôi qua kể cả lúc tắt app, rồi gom dữ liệu của từng mô-đun và ghi MỘT file duy nhất.
    // Đây là nơi duy nhất sở hữu file lưu, nên các mô-đun không ghi đè lên nhau.
    // Là lớp thuần (không phải MonoBehaviour) để kiểm thử được với đồng hồ giả và mô-đun giả.
    public sealed class GardenSession
    {
        readonly string path;
        readonly IReadOnlyList<IGardenModule> modules;
        readonly Func<DateTime> clock;
        readonly double maxOfflineDays;

        GardenState state;
        DateTime lastTickUtc;
        bool isPreview;

        public GardenEventBus Events { get; } = new GardenEventBus();
        public GardenState State => state;
        public LoadOutcome LastLoadOutcome { get; private set; }
        public bool IsPreview => isPreview;

        public GardenSession(string path, IReadOnlyList<IGardenModule> modules, Func<DateTime> clock,
            double maxOfflineDays = GardenClock.MaxOfflineDays)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            this.modules = modules ?? throw new ArgumentNullException(nameof(modules));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.maxOfflineDays = maxOfflineDays;

            EnsureUniqueIds(modules);
        }

        // preview = true: chỉ xem thử, tuyệt đối không ghi file lưu
        public void Start(int newSeed, bool preview = false)
        {
            isPreview = preview;
            DateTime now = clock();

            LoadResult loaded = GardenStateStore.Load(path, now, newSeed);
            state = loaded.state;
            LastLoadOutcome = loaded.outcome;

            var context = new GardenContext(state.seed, Events);
            foreach (IGardenModule module in modules) Guarded(module, "Load", () => module.Load(state, context));

            double offline = GardenClock.ElapsedDays(state.LastSeenUtc, now, maxOfflineDays);
            if (offline > 0) TickAll(offline);

            lastTickUtc = now;
            Save();
        }

        // Gọi định kỳ: cho các mô-đun chạy phần thời gian trôi qua kể từ lần trước
        public void Refresh()
        {
            DateTime now = clock();
            double elapsed = GardenClock.ElapsedDays(lastTickUtc, now, maxOfflineDays);
            if (elapsed > 0) TickAll(elapsed);
            lastTickUtc = now;
        }

        public void Save()
        {
            if (isPreview) return;

            Refresh();   // chạy nốt thời gian chưa tính, để "lần cuối còn chạy" khớp với những gì mô-đun đã biết

            GardenState next = state;
            foreach (IGardenModule module in modules)
            {
                try
                {
                    next = module.Save(next);
                }
                catch (Exception e)
                {
                    Debug.LogError($"GardenSession: mô-đun '{module.Id}' bị lỗi khi lưu, giữ dữ liệu cũ của nó: {e}");
                }
            }

            state = next.WithLastSeen(lastTickUtc);
            GardenStateStore.Save(path, state);
        }

        // Xem thử mọi mô-đun ở một độ tuổi bất kỳ và ngừng ghi file lưu
        public void PreviewAge(double ageDays)
        {
            isPreview = true;
            PreviewAll(modules, ageDays);
        }

        public static void PreviewAll(IEnumerable<IGardenModule> modules, double ageDays)
        {
            foreach (IGardenModule module in modules)
            {
                if (module is IAgePreviewable previewable) Guarded(module, "PreviewAge", () => previewable.PreviewAge(ageDays));
            }
        }

        void TickAll(double elapsedDays)
        {
            foreach (IGardenModule module in modules) Guarded(module, "Tick", () => module.Tick(elapsedDays));
        }

        // Một mô-đun lỗi không được kéo sập cả vườn: ghi lỗi kèm tên mô-đun rồi chạy tiếp các mô-đun khác
        static void Guarded(IGardenModule module, string phase, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"GardenSession: mô-đun '{module.Id}' bị lỗi ở bước {phase}: {e}");
            }
        }

        static void EnsureUniqueIds(IReadOnlyList<IGardenModule> modules)
        {
            var seen = new HashSet<string>();
            foreach (IGardenModule module in modules)
            {
                if (string.IsNullOrEmpty(module.Id)) throw new ArgumentException("Mô-đun phải có Id: " + module.GetType().Name);
                if (!seen.Add(module.Id)) throw new ArgumentException("Hai mô-đun trùng Id '" + module.Id + "'");
            }
        }
    }
}
