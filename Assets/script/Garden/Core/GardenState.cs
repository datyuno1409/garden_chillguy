using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden.Core
{
    // Phần dữ liệu của một mô-đun trong file lưu: tên mô-đun và dữ liệu của nó dưới dạng JSON.
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
        public int seed;                 // hạt giống của vườn: mỗi vườn một kiểu (ví dụ rêu loang chỗ nào)
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

        public bool TryGetSection<T>(string id, out T value)
        {
            value = default;
            if (sections == null) return false;

            foreach (GardenSection section in sections)
            {
                if (section.id != id) continue;
                try
                {
                    value = JsonUtility.FromJson<T>(section.json);
                    return true;
                }
                catch (ArgumentException e)
                {
                    Debug.LogWarning($"GardenState: phần '{id}' trong file lưu không đọc được, dùng giá trị mặc định ({e.Message})");
                    return false;
                }
            }
            return false;
        }

        public GardenState WithSection<T>(string id, T value)
        {
            var list = new List<GardenSection>(sections ?? Array.Empty<GardenSection>());
            var entry = new GardenSection { id = id, json = JsonUtility.ToJson(value) };

            int index = list.FindIndex(s => s.id == id);
            if (index >= 0) list[index] = entry;
            else list.Add(entry);

            GardenState copy = this;
            copy.sections = list.ToArray();
            return copy;
        }

        // File lưu có thể hỏng hoặc do tay sửa: không tin dữ liệu đọc từ ngoài vào
        public bool IsValid()
        {
            bool ticksOk = IsValidTicks(lastSeenTicksUtc) && IsValidTicks(createdTicksUtc);
            return version == CurrentVersion && ticksOk && SectionsAreValid();
        }

        public static bool IsValidTicks(long ticks) => ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks;

        bool SectionsAreValid()
        {
            if (sections == null) return true;

            var seen = new HashSet<string>();
            foreach (GardenSection section in sections)
            {
                bool ok = !string.IsNullOrEmpty(section.id) && section.json != null && seen.Add(section.id);
                if (!ok) return false;
            }
            return true;
        }
    }
}
