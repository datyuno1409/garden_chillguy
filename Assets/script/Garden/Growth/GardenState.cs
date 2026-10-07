using System;

namespace Garden.Growth
{
    // Trạng thái vườn được lưu giữa các lần chạy. Mọi thay đổi trả về bản sao mới, không sửa bản gốc.
    // Là struct công khai (không readonly) chỉ vì JsonUtility cần ghi vào field khi đọc file.
    [Serializable]
    public struct GardenState
    {
        public const int CurrentVersion = 1;
        const double MaxAgeDays = 1_000_000;

        public int version;
        public int seed;                // quyết định rêu loang theo kiểu nào, mỗi vườn một kiểu
        public long createdTicksUtc;
        public long lastSeenTicksUtc;   // lần cuối ứng dụng còn chạy, để tính thời gian đã trôi qua khi mở lại
        public double mossAgeDays;      // tuổi của rêu (ngày), chỉ tăng

        public DateTime LastSeenUtc => new DateTime(lastSeenTicksUtc, DateTimeKind.Utc);

        public static GardenState CreateNew(DateTime nowUtc, int seed) => new GardenState
        {
            version = CurrentVersion,
            seed = seed,
            createdTicksUtc = nowUtc.Ticks,
            lastSeenTicksUtc = nowUtc.Ticks,
            mossAgeDays = 0,
        };

        public GardenState WithAge(double ageDays, DateTime nowUtc)
        {
            GardenState copy = this;
            copy.mossAgeDays = ageDays;
            copy.lastSeenTicksUtc = nowUtc.Ticks;
            return copy;
        }

        // File lưu có thể hỏng hoặc do tay sửa: không tin dữ liệu đọc từ ngoài vào
        public bool IsValid()
        {
            bool ticksOk = lastSeenTicksUtc >= DateTime.MinValue.Ticks && lastSeenTicksUtc <= DateTime.MaxValue.Ticks
                && createdTicksUtc >= DateTime.MinValue.Ticks && createdTicksUtc <= DateTime.MaxValue.Ticks;
            bool ageOk = !double.IsNaN(mossAgeDays) && mossAgeDays >= 0 && mossAgeDays <= MaxAgeDays;
            return version == CurrentVersion && ticksOk && ageOk;
        }
    }
}
