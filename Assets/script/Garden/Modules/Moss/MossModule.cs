using Garden.Core;
using UnityEngine;

namespace Garden.Moss
{
    // Mô-đun rêu: giữ tuổi rêu và các vết bị bóc (lưu trong phần "moss" của file lưu), cho rêu mọc và vết lành theo thời gian thật,
    // nhận click của người chơi để bóc rêu, và đẩy kết quả sang MossGrowth để vẽ lên đá. Gắn cùng chỗ với MossGrowth.
    // Click chỉ có tác dụng khi trúng vật nằm dưới object này (các tảng đá của nhóm rêu).
    public sealed class MossModule : MonoBehaviour, IGardenModule, IAgePreviewable, IGardenClickHandler
    {
        public const string ModuleId = "moss";

        [SerializeField] MossGrowth view;
        [SerializeField] MossSettings settings = MossSettings.Default;

        readonly MossSimulation simulation = new MossSimulation();
        readonly MossDamage damage = new MossDamage();
        GardenContext context;
        float seed;

        public string Id => ModuleId;
        public MossDamage Damage => damage;

        public void Configure(MossGrowth target) { view = target; }

        public void Load(GardenState state, GardenContext gardenContext)
        {
            context = gardenContext;

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

        public void Tick(double elapsedDays)
        {
            simulation.Tick(elapsedDays);
            damage.Tick(elapsedDays, settings.healDays);
            Apply();
        }

        public void PreviewAge(double ageDays)
        {
            simulation.SetAge(ageDays);
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

        void Apply()
        {
            if (view == null) view = GetComponent<MossGrowth>();
            if (view == null) return;

            view.Seed = seed;
            view.Coverage = simulation.Coverage(settings);
            view.SetHits(damage.Hits);
        }
    }
}
