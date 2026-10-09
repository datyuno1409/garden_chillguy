using Garden.Core;
using UnityEngine;

namespace Garden.Moss
{
    // Mô-đun rêu: giữ tuổi rêu (lưu trong phần "moss" của file lưu), cho rêu mọc theo thời gian thật,
    // và đẩy độ phủ cùng hạt giống sang MossGrowth để vẽ lên đá. Gắn cùng chỗ với MossGrowth.
    public sealed class MossModule : MonoBehaviour, IGardenModule, IAgePreviewable
    {
        public const string ModuleId = "moss";

        [SerializeField] MossGrowth view;
        [SerializeField] MossSettings settings = MossSettings.Default;

        readonly MossSimulation simulation = new MossSimulation();
        float seed;

        public string Id => ModuleId;

        public void Configure(MossGrowth target) { view = target; }

        public void Load(GardenState state, GardenContext context)
        {
            simulation.SetAge(state.TryGetSection(ModuleId, out MossSection section) ? section.ageDays : 0);
            seed = (context.Seed % 1000) * 0.37f;   // mỗi vườn một kiểu loang
            Apply();
        }

        public GardenState Save(GardenState state)
        {
            return state.WithSection(ModuleId, new MossSection { ageDays = simulation.AgeDays });
        }

        public void Tick(double elapsedDays)
        {
            simulation.Tick(elapsedDays);
            Apply();
        }

        public void PreviewAge(double ageDays)
        {
            simulation.SetAge(ageDays);
            Apply();
        }

        void Apply()
        {
            if (view == null) view = GetComponent<MossGrowth>();
            if (view == null) return;

            view.Seed = seed;
            view.Coverage = simulation.Coverage(settings);
        }
    }
}
