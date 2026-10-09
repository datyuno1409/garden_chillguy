using System;

namespace Garden.Core
{
    public static class GardenClock
    {
        public const double MaxOfflineDays = 365;   // tối đa tính bao nhiêu ngày cho một lần tắt app

        // Số ngày đã trôi qua. Đồng hồ bị chỉnh lùi thì không tính (không để mọi thứ "teo" đi hay tuổi âm).
        public static double ElapsedDays(DateTime fromUtc, DateTime toUtc, double maxDays = MaxOfflineDays)
        {
            double days = (toUtc - fromUtc).TotalDays;
            if (double.IsNaN(days) || days <= 0) return 0;
            return Math.Min(days, maxDays);
        }
    }
}
