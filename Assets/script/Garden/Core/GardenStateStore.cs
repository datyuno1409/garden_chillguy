using System;
using System.IO;
using UnityEngine;

namespace Garden.Core
{
    public readonly struct LoadResult
    {
        public readonly GardenState state;
        public readonly LoadOutcome outcome;

        public LoadResult(GardenState state, LoadOutcome outcome)
        {
            this.state = state;
            this.outcome = outcome;
        }
    }

    // Đọc/ghi trạng thái vườn ra file JSON.
    // Khi gặp file bản cũ / hỏng / bản mới hơn: luôn giữ lại một bản sao lưu trước khi tạo vườn mới hay ghi đè.
    public static class GardenStateStore
    {
        public const string MigratedBackupSuffix = ".pre-v2.bak";
        public const string CorruptBackupSuffix = ".bad";
        public const string NewerBackupSuffix = ".newer";

        public static LoadResult Load(string path, DateTime nowUtc, int newSeed)
        {
            LoadResult fresh = new LoadResult(GardenState.CreateNew(nowUtc, newSeed), LoadOutcome.Missing);
            if (!File.Exists(path)) return fresh;

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("GardenStateStore: không đọc được file vườn (" + e.Message + "): " + path);
                return new LoadResult(fresh.state, LoadOutcome.Corrupt);
            }

            LoadOutcome outcome = GardenStateMigrator.TryParse(json, out GardenState state);
            switch (outcome)
            {
                case LoadOutcome.Loaded:
                    return new LoadResult(state, outcome);

                case LoadOutcome.Migrated:
                    SafeFile.Backup(path, MigratedBackupSuffix, overwrite: false);
                    return new LoadResult(state, outcome);

                case LoadOutcome.NewerThanSupported:
                    Debug.LogWarning("GardenStateStore: file vườn do bản app mới hơn tạo ra, bản này không hiểu. Giữ bản sao và tạo vườn mới: " + path);
                    SafeFile.Backup(path, NewerBackupSuffix, overwrite: true);
                    return new LoadResult(fresh.state, outcome);

                default:
                    Debug.LogWarning("GardenStateStore: dữ liệu vườn không hợp lệ, giữ bản sao và tạo vườn mới: " + path);
                    SafeFile.Backup(path, CorruptBackupSuffix, overwrite: true);
                    return new LoadResult(fresh.state, LoadOutcome.Corrupt);
            }
        }

        public static void Save(string path, GardenState state) => SafeFile.WriteAtomically(path, JsonUtility.ToJson(state, true));
    }

    // Vị trí các file dữ liệu: dùng thư mục chung (không theo tên sản phẩm) để Control Panel và cửa sổ vườn cùng thấy.
    // Trong Unity Editor dùng file riêng để thử nghiệm không làm hỏng vườn thật.
    public static class GardenPaths
    {
        public static string StateFile => InDataFolder("garden_state");
        public static string SettingsFile => InDataFolder("garden_settings");

        static string InDataFolder(string baseName)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string suffix = Application.isEditor ? ".editor.json" : ".json";
            return Path.Combine(root, "GardenChill", baseName + suffix);
        }
    }
}
