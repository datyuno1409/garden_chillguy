using System;

namespace Garden.Moss
{
    [Serializable]
    public struct MossSettings
    {
        public float daysToFullCover;    // sau bao nhiêu ngày thì rêu phủ tới mức tối đa
        public float startCoverage;      // độ phủ lúc mới tạo vườn (vài đốm rêu nhỏ)
        public float maxCoverage;        // độ phủ tối đa (không phủ kín để còn thấy đá)
        public float hitRadius;          // bán kính vết rêu bị bóc khi click (mét)
        public float healDays;           // vết rêu bị bóc lành hẳn sau bao nhiêu ngày

        public static MossSettings Default => new MossSettings
        {
            daysToFullCover = 14f,
            startCoverage = 0.1f,
            maxCoverage = 0.5f,
            hitRadius = 0.45f,
            healDays = 2f,
        };
    }

    // Phần dữ liệu của rêu trong file lưu (mục "moss"). Tên field "ageDays" phải khớp với việc nâng cấp file bản 1
    // trong GardenStateMigrator; có test kiểm tra hai bên khớp nhau.
    // Thêm field mới (như hits) không cần đổi phiên bản file: file cũ thiếu field thì nhận giá trị mặc định (hits = null).
    [Serializable]
    public struct MossSection
    {
        public double ageDays;
        public MossHit[] hits;
    }

    // Logic thuần của rêu (không dùng Unity), nên kiểm thử được: tuổi rêu -> độ phủ rêu.
    public sealed class MossSimulation
    {
        const double MaxAgeDays = 1_000_000;

        public double AgeDays { get; private set; }

        public void SetAge(double ageDays)
        {
            AgeDays = double.IsNaN(ageDays) || ageDays < 0 ? 0 : Math.Min(ageDays, MaxAgeDays);
        }

        public void Tick(double elapsedDays)
        {
            if (double.IsNaN(elapsedDays) || elapsedDays <= 0) return;
            SetAge(AgeDays + elapsedDays);
        }

        public float Coverage(MossSettings settings) => CoverageAt(AgeDays, settings);

        // Đường cong mềm: ngày đầu mọc rất chậm (bào tử), giữa chừng nhanh nhất, gần đầy lại chậm dần
        public static float CoverageAt(double ageDays, MossSettings settings)
        {
            double t = settings.daysToFullCover > 0f ? ageDays / settings.daysToFullCover : 1.0;
            t = Math.Min(1.0, Math.Max(0.0, t));
            double eased = t * t * (3.0 - 2.0 * t);
            return (float)(settings.startCoverage + (settings.maxCoverage - settings.startCoverage) * eased);
        }
    }
}
