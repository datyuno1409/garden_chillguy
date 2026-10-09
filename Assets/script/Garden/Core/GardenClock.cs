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

        // Khoảng thời gian đã trôi qua từ lần trước tới giờ. Không có thời gian trôi qua (hoặc đồng hồ chỉnh lùi) thì trả false.
        // Quá lâu (hơn maxDays) thì chỉ tính maxDays gần nhất.
        public static bool TryWindow(DateTime fromUtc, DateTime toUtc, double maxDays, out TimeWindow window)
        {
            window = default;
            double days = ElapsedDays(fromUtc, toUtc, maxDays);
            if (days <= 0) return false;

            // Không bị cắt thì giữ đúng thời điểm bắt đầu (tránh sai số khi tính ngược từ cuối)
            DateTime start = (toUtc - fromUtc).TotalDays <= maxDays ? fromUtc : toUtc.AddDays(-maxDays);
            window = new TimeWindow(start, toUtc);
            return true;
        }
    }
}
