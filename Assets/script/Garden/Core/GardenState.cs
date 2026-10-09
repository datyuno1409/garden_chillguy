using System;
using UnityEngine;

namespace Garden.Core
{
    // Phần dữ liệu của một mô-đun trong file lưu / file cài đặt: tên mô-đun và dữ liệu của nó dưới dạng JSON.
    // Mỗi mô-đun tự quyết định định dạng bên trong; Core chỉ giữ nguyên và không đoán.
    [Serializable]
    public struct GardenSection
    {
        public string id;
        public string json;
    }

    // Trạng thái cả khu vườn: thông tin chung + một phần riêng cho từng mô-đun (rêu, cây leo, con vật...).
    // Các mô-đun không ghi đè lên nhau vì mỗi mô-đun chỉ đụng tới phần của mình.
    // Mọi thay đổi trả về bản sao mới. Là struct công khai (không readonly) chỉ vì JsonUtility cần ghi vào field khi đọc file.
    [Serializable]
    public struct GardenState
    {
        public const int CurrentVersion = 2;

        public int version;
        public int seed;                 // hạt giống của vườn: mỗi vườn một kiểu (ví dụ rêu loang chỗ nào, lịch mưa)
        public long createdTicksUtc;
        public long lastSeenTicksUtc;    // lần cuối ứng dụng còn chạy, để tính thời gian đã trôi qua khi mở lại
        public GardenSection[] sections;

        public DateTime LastSeenUtc => new DateTime(lastSeenTicksUtc, DateTimeKind.Utc);

        public static GardenState CreateNew(DateTime nowUtc, int seed) => new GardenState
        {
            version = CurrentVersion,
            seed = seed,
            createdTicksUtc = nowUtc.Ticks,
            lastSeenTicksUtc = nowUtc.Ticks,
            sections = Array.Empty<GardenSection>(),
        };

        public GardenState WithLastSeen(DateTime nowUtc)
        {
            GardenState copy = this;
            copy.lastSeenTicksUtc = nowUtc.Ticks;
            return copy;
        }

        public bool TryGetSection<T>(string id, out T value) => GardenSections.TryGet(sections, id, out value);

        public GardenState WithSection<T>(string id, T value)
        {
            GardenState copy = this;
            copy.sections = GardenSections.With(sections, id, value);
            return copy;
        }

        // File lưu có thể hỏng hoặc do tay sửa: không tin dữ liệu đọc từ ngoài vào
        public bool IsValid()
        {
            bool ticksOk = IsValidTicks(lastSeenTicksUtc) && IsValidTicks(createdTicksUtc);
            return version == CurrentVersion && ticksOk && GardenSections.AreValid(sections);
        }

        public static bool IsValidTicks(long ticks) => ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks;
    }
}
