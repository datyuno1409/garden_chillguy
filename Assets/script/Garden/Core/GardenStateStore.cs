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

    // Đọc/ghi trạng thái vườn ra file JSON. Ghi qua file tạm rồi thay thế nên mất điện giữa chừng không làm hỏng file cũ.
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
                    Backup(path, MigratedBackupSuffix, overwrite: false);
                    return new LoadResult(state, outcome);

                case LoadOutcome.NewerThanSupported:
                    Debug.LogWarning("GardenStateStore: file vườn do bản app mới hơn tạo ra, bản này không hiểu. Giữ bản sao và tạo vườn mới: " + path);
                    Backup(path, NewerBackupSuffix, overwrite: true);
                    return new LoadResult(fresh.state, outcome);

                default:
                    Debug.LogWarning("GardenStateStore: dữ liệu vườn không hợp lệ, giữ bản sao và tạo vườn mới: " + path);
                    Backup(path, CorruptBackupSuffix, overwrite: true);
                    return new LoadResult(fresh.state, LoadOutcome.Corrupt);
            }
        }

        public static void Save(string path, GardenState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(state, true));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        static void Backup(string path, string suffix, bool overwrite)
        {
            string backup = path + suffix;
            if (!overwrite && File.Exists(backup)) return;

            try
            {
                File.Copy(path, backup, overwrite);
            }
            catch (IOException e)
            {
                Debug.LogWarning("GardenStateStore: không sao lưu được file vườn: " + e.Message);
            }
        }
    }

    // Vị trí file trạng thái: dùng thư mục chung (không theo tên sản phẩm) để Control Panel và cửa sổ vườn cùng thấy.
    // Trong Unity Editor dùng file riêng để thử nghiệm không làm hỏng vườn thật.
    public static class GardenPaths
    {
        public static string StateFile
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string name = Application.isEditor ? "garden_state.editor.json" : "garden_state.json";
                return Path.Combine(root, "GardenChill", name);
            }
        }
    }
}
