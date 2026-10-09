using Garden.Core;
using UnityEngine;

namespace Garden.Moss
{
    // Phần cài đặt của rêu do người chơi chọn ở bảng thiết kế (ghi đè giá trị mặc định lưu trong cảnh)
    [System.Serializable]
    public struct MossDesign
    {
        public float daysToFullCover;   // sau bao nhiêu ngày thì rêu phủ tới mức tối đa
        public float maxCoverage;       // độ phủ tối đa
    }

    // Mô-đun rêu: giữ tuổi rêu và các vết bị bóc (lưu trong phần "moss" của file lưu), cho rêu mọc và vết lành theo thời gian thật,
    // nhận click của người chơi để bóc rêu, và đẩy kết quả sang MossGrowth để vẽ lên đá. Gắn cùng chỗ với MossGrowth.
    // Click chỉ có tác dụng khi trúng vật nằm dưới object này (các tảng đá của nhóm rêu).
    // Nghe GrowthBoost (ví dụ mưa) để mọc và lành nhanh hơn mà không cần biết mô-đun nào gửi.
    public sealed class MossModule : MonoBehaviour, IGardenModule, IAgePreviewable, IGardenClickHandler, IGardenConfigurable
    {
        public const string ModuleId = "moss";

        [SerializeField] MossGrowth view;
        [SerializeField] MossSettings settings = MossSettings.Default;

        readonly MossSimulation simulation = new MossSimulation();
        readonly MossDamage damage = new MossDamage();
        GardenContext context;
        GardenEventBus subscribedBus;
        MossDesign? design;
        float seed;

        public string Id => ModuleId;
        public MossDamage Damage => damage;

        // Cài đặt đang dùng: giá trị người chơi chọn ở bảng thiết kế nếu có, không thì giá trị trong cảnh
        MossSettings Active
        {
            get
            {
                MossSettings active = settings;
                if (design.HasValue)
                {
                    active.daysToFullCover = design.Value.daysToFullCover;
                    active.maxCoverage = design.Value.maxCoverage;
                }
                return active;
            }
        }

        public void Configure(MossGrowth target) { view = target; }

        public void Load(GardenState state, GardenContext gardenContext)
        {
            context = gardenContext;
            Subscribe(gardenContext.Events);

            bool found = state.TryGetSection(ModuleId, out MossSection section);
            simulation.SetAge(found ? section.ageDays : 0);
            damage.Load(found ? section.hits : null);

            seed = (gardenContext.Seed % 1000) * 0.37f;   // mỗi vườn một kiểu loang
            Apply();
        }

        public GardenState Save(GardenState state)
        {
            return state.WithSection(ModuleId, new MossSection { ageDays = simulation.AgeDays, hits = damage.ToArray() });
        }

        public void Tick(TimeWindow window) => Grow(window.Days);

        public void PreviewAge(double ageDays)
        {
            simulation.SetAge(ageDays);
            Apply();
        }

        public void ApplySettings(GardenSettings gardenSettings)
        {
            design = gardenSettings.TryGetSection(ModuleId, out MossDesign chosen) ? chosen : (MossDesign?)null;
            Apply();
        }

        // Người chơi click vào vườn: nếu trúng đá của nhóm rêu thì bóc rêu quanh điểm đó và báo cho mô-đun khác biết
        public void OnGardenClick(GardenClick click)
        {
            if (click.collider == null || !click.collider.transform.IsChildOf(transform)) return;

            damage.AddHit(click.point, settings.hitRadius);
            Apply();
            context?.Events.Publish(new GardenDisturbance(click.point, 1f, ModuleId));
        }

        void OnDisable() { Unsubscribe(); }

        void OnGrowthBoost(GrowthBoost boost) => Grow(boost.extraDays);

        // Thời gian trôi qua (hoặc thời gian cộng thêm do môi trường) làm rêu già đi và vết bóc lành dần
        void Grow(double days)
        {
            simulation.Tick(days);
            damage.Tick(days, Active.healDays);
            Apply();
        }

        void Subscribe(GardenEventBus bus)
        {
            if (subscribedBus == bus) return;
            Unsubscribe();
            bus.Subscribe<GrowthBoost>(OnGrowthBoost);
            subscribedBus = bus;
        }

        void Unsubscribe()
        {
            subscribedBus?.Unsubscribe<GrowthBoost>(OnGrowthBoost);
            subscribedBus = null;
        }

        void Apply()
        {
            if (view == null) view = GetComponent<MossGrowth>();
            if (view == null) return;

            view.Seed = seed;
            view.Coverage = simulation.Coverage(Active);
            view.SetHits(damage.Hits);
        }
    }
}
