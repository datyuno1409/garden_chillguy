using System;

namespace Garden.Growth
{
    [Serializable]
    public struct GrowthSettings
    {
        public float daysToFullCover;    // sau bao nhiêu ngày thì rêu phủ tới mức tối đa
        public float startCoverage;      // độ phủ lúc mới tạo vườn (vài đốm rêu nhỏ)
        public float maxCoverage;        // độ phủ tối đa (không phủ kín để còn thấy đá)
        public double maxOfflineDays;    // tối đa bao nhiêu ngày được tính cho một lần tắt app

        public static GrowthSettings Default => new GrowthSettings
        {
            daysToFullCover = 14f,
            startCoverage = 0.1f,
            maxCoverage = 0.5f,
            maxOfflineDays = 365,
        };
    }

    // Logic thuần (không dùng Unity), nên kiểm thử được: thời gian trôi qua -> tuổi của rêu -> độ phủ rêu.
    public static class GrowthModel
    {
        // Số ngày đã trôi qua. Đồng hồ bị chỉnh lùi thì không tính (không để rêu "teo" đi hay tuổi âm).
        public static double ElapsedDays(DateTime lastSeenUtc, DateTime nowUtc, double maxDays)
        {
            double days = (nowUtc - lastSeenUtc).TotalDays;
            if (double.IsNaN(days) || days <= 0) return 0;
            return Math.Min(days, maxDays);
        }

        public static GardenState Advance(GardenState state, DateTime nowUtc, GrowthSettings settings)
        {
            double elapsed = ElapsedDays(state.LastSeenUtc, nowUtc, settings.maxOfflineDays);
            return state.WithAge(state.mossAgeDays + elapsed, nowUtc);
        }

        // Đường cong mềm: ngày đầu mọc rất chậm (bào tử), giữa chừng nhanh nhất, gần đầy lại chậm dần
        public static float Coverage(double ageDays, GrowthSettings settings)
        {
            double t = settings.daysToFullCover > 0f ? ageDays / settings.daysToFullCover : 1.0;
            t = Math.Min(1.0, Math.Max(0.0, t));
            double eased = t * t * (3.0 - 2.0 * t);
            return (float)(settings.startCoverage + (settings.maxCoverage - settings.startCoverage) * eased);
        }
    }
}
