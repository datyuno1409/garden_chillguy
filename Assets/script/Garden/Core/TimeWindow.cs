using System;

namespace Garden.Core
{
    // Một khoảng thời gian thật đã trôi qua giữa hai lần cập nhật (kể cả lúc tắt app).
    // Mô-đun nhận khoảng này (thay vì chỉ số ngày) để tính được những thứ phụ thuộc vào GIỜ NÀO đã trôi qua,
    // ví dụ mưa rơi vào lúc nào trong lúc app đóng.
    public readonly struct TimeWindow
    {
        public readonly DateTime fromUtc;
        public readonly DateTime toUtc;

        public TimeWindow(DateTime fromUtc, DateTime toUtc)
        {
            this.fromUtc = fromUtc;
            this.toUtc = toUtc;
        }

        public double Days => Math.Max(0, (toUtc - fromUtc).TotalDays);

        public static TimeWindow FromDays(DateTime fromUtc, double days) => new TimeWindow(fromUtc, fromUtc.AddDays(days));
    }
}
