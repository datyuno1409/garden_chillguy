using System;
using Garden.Core;
using UnityEngine;

namespace Garden.Rain
{
    // Mô-đun mưa: quyết định lúc nào có mưa (lịch xác định theo seed vườn, cộng mưa do người chơi gọi),
    // và làm cây cỏ mọc nhanh hơn trong lúc mưa bằng cách phát GrowthBoost (kể cả phần mưa rơi lúc đóng app).
    // Không giữ dữ liệu trong file lưu vì lịch mưa tính lại được. Phần hiển thị (hạt mưa, ánh sáng) do RainView lo.
    public sealed class RainModule : MonoBehaviour, IGardenModule, IGardenConfigurable
    {
        public const string ModuleId = "rain";

        [SerializeField] float growthBoostFactor = 3f;   // mưa 1 giờ thì thêm bấy nhiêu giờ cho cây mọc
        [SerializeField] float checkSeconds = 0.5f;      // bao lâu kiểm tra có mưa không
        [SerializeField] float rampUpSeconds = 15f;      // mưa nhỏ dần thành to trong bấy nhiêu giây
        [SerializeField] float rampDownSeconds = 25f;    // và tạnh dần trong bấy nhiêu giây

        RainSettings rainSettings = RainSettings.Default;
        Func<DateTime> clock = () => DateTime.UtcNow;
        GardenEventBus events;
        int seed;
        float untilCheck;

        public string Id => ModuleId;

        // Có mưa lúc này không (theo lịch)
        public bool IsRaining { get; private set; }

        // Độ lớn của mưa 0..1, lên xuống từ từ để hạt mưa và ánh sáng không đổi đột ngột
        public float Intensity { get; private set; }

        public void SetClock(Func<DateTime> newClock) { clock = newClock; }

        public void Load(GardenState state, GardenContext context)
        {
            seed = context.Seed;
            events = context.Events;
            Refresh();
        }

        public GardenState Save(GardenState state) => state;   // lịch mưa tính lại được từ seed nên không cần lưu

        public void Tick(TimeWindow window)
        {
            double rainDays = RainSchedule.RainDays(seed, window.fromUtc, window.toUtc, rainSettings);
            if (rainDays > 0) events?.Publish(new GrowthBoost(rainDays * growthBoostFactor, ModuleId));
        }

        public void ApplySettings(GardenSettings settings)
        {
            rainSettings = settings.GetOrDefault(ModuleId, RainSettings.Default);
            Refresh();   // người chơi bấm "Mưa ngay" thì mưa bắt đầu ngay, không đợi lần kiểm tra sau
        }

        void Update()
        {
            untilCheck -= Time.unscaledDeltaTime;
            if (untilCheck <= 0f)
            {
                Refresh();
                untilCheck = checkSeconds;
            }

            float target = IsRaining ? 1f : 0f;
            float seconds = IsRaining ? rampUpSeconds : rampDownSeconds;
            Intensity = Mathf.MoveTowards(Intensity, target, Time.unscaledDeltaTime / Mathf.Max(seconds, 0.01f));
        }

        void Refresh() { IsRaining = RainSchedule.IsRaining(seed, clock(), rainSettings); }
    }
}
