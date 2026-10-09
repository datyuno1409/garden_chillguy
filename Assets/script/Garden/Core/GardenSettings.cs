using System;
using System.IO;
using UnityEngine;

namespace Garden.Core
{
    // Cài đặt do người chơi chọn ở bảng thiết kế: một phần riêng cho từng mô-đun (cùng cách với GardenState).
    // Control Panel ghi file này, cửa sổ vườn đọc và áp dụng ngay khi file đổi.
    // Mọi thay đổi trả về bản sao mới.
    [Serializable]
    public struct GardenSettings
    {
        public const int CurrentVersion = 1;

        public int version;
        public GardenSection[] sections;

        public static GardenSettings Default => new GardenSettings { version = CurrentVersion, sections = Array.Empty<GardenSection>() };

        public bool TryGetSection<T>(string id, out T value) => GardenSections.TryGet(sections, id, out value);

        // Lấy phần cài đặt của một mô-đun, chưa có thì dùng giá trị mặc định cho trước
        public T GetOrDefault<T>(string id, T fallback) => TryGetSection(id, out T value) ? value : fallback;

        public GardenSettings WithSection<T>(string id, T value)
        {
            GardenSettings copy = this;
            copy.sections = GardenSections.With(sections, id, value);
            return copy;
        }

        public bool IsValid() => version == CurrentVersion && GardenSections.AreValid(sections);
    }

    // Đọc/ghi file cài đặt. Thiếu file thì dùng mặc định; file hỏng hay do bản mới hơn tạo ra thì giữ bản sao rồi dùng mặc định.
    public static class GardenSettingsStore
    {
        public const string CorruptBackupSuffix = ".bad";
        public const string NewerBackupSuffix = ".newer";

        [Serializable] struct VersionProbe { public int version; }

        public static GardenSettings Load(string path)
        {
            if (!File.Exists(path)) return GardenSettings.Default;

            try
            {
                string json = File.ReadAllText(path);
                int version = JsonUtility.FromJson<VersionProbe>(json).version;

                if (version == GardenSettings.CurrentVersion)
                {
                    GardenSettings settings = JsonUtility.FromJson<GardenSettings>(json);
                    if (settings.IsValid()) return settings;
                }
                else if (version > GardenSettings.CurrentVersion)
                {
                    Debug.LogWarning("GardenSettingsStore: file cài đặt do bản app mới hơn tạo ra, dùng mặc định: " + path);
                    SafeFile.Backup(path, NewerBackupSuffix, overwrite: true);
                    return GardenSettings.Default;
                }
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("GardenSettingsStore: không đọc được file cài đặt (" + e.Message + "): " + path);
            }

            Debug.LogWarning("GardenSettingsStore: file cài đặt không hợp lệ, giữ bản sao và dùng mặc định: " + path);
            SafeFile.Backup(path, CorruptBackupSuffix, overwrite: true);
            return GardenSettings.Default;
        }

        public static void Save(string path, GardenSettings settings) => SafeFile.WriteAtomically(path, JsonUtility.ToJson(settings, true));
    }
}
