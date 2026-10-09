using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden.Core
{
    // Cách chung để một tài liệu JSON (file lưu, file cài đặt) chứa một phần riêng cho từng mô-đun.
    // Dùng chung cho GardenState và GardenSettings để hai nơi không chép lại cùng một đoạn.
    internal static class GardenSections
    {
        public static bool TryGet<T>(GardenSection[] sections, string id, out T value)
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
                    Debug.LogWarning($"Garden: phần '{id}' không đọc được, dùng giá trị mặc định ({e.Message})");
                    return false;
                }
            }
            return false;
        }

        // Trả về mảng mới (không sửa mảng cũ): thay phần cùng tên nếu có, không thì thêm vào cuối
        public static GardenSection[] With<T>(GardenSection[] sections, string id, T value)
        {
            var list = new List<GardenSection>(sections ?? Array.Empty<GardenSection>());
            var entry = new GardenSection { id = id, json = JsonUtility.ToJson(value) };

            int index = list.FindIndex(s => s.id == id);
            if (index >= 0) list[index] = entry;
            else list.Add(entry);
            return list.ToArray();
        }

        public static bool AreValid(GardenSection[] sections)
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
