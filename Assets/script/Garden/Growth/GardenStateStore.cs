using System;
using System.IO;
using UnityEngine;

namespace Garden.Growth
{
    // Đọc/ghi trạng thái vườn ra file JSON. Ghi qua file tạm rồi thay thế nên mất điện giữa chừng không làm hỏng file cũ.
    public static class GardenStateStore
    {
        public static GardenState LoadOrCreate(string path, DateTime nowUtc, int newSeed)
        {
            return TryRead(path, out GardenState state) ? state : GardenState.CreateNew(nowUtc, newSeed);
        }

        public static bool TryRead(string path, out GardenState state)
        {
            state = default;
            if (!File.Exists(path)) return false;

            try
            {
                state = JsonUtility.FromJson<GardenState>(File.ReadAllText(path));
                if (state.IsValid()) return true;
                Debug.LogWarning("GardenStateStore: dữ liệu vườn không hợp lệ, bỏ qua và tạo mới: " + path);
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("GardenStateStore: không đọc được file vườn (" + e.Message + "): " + path);
            }

            KeepBackupOfBadFile(path);
            state = default;
            return false;
        }

        public static void Save(string path, GardenState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(state, true));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        // Giữ lại file hỏng (đổi đuôi .bad) để còn xem được, thay vì ghi đè mất
        static void KeepBackupOfBadFile(string path)
        {
            try
            {
                File.Copy(path, path + ".bad", true);
            }
            catch (IOException e)
            {
                Debug.LogWarning("GardenStateStore: không sao lưu được file hỏng: " + e.Message);
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
