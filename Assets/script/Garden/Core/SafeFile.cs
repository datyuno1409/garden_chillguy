using System;
using System.IO;
using UnityEngine;

namespace Garden.Core
{
    // Ghi file an toàn và sao lưu, dùng chung cho file lưu và file cài đặt
    internal static class SafeFile
    {
        // Ghi qua file tạm rồi thay thế: mất điện giữa chừng không làm hỏng file cũ, và người đọc không thấy file ghi dở
        public static void WriteAtomically(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            string temp = path + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        public static void Backup(string path, string suffix, bool overwrite)
        {
            string backup = path + suffix;
            if (!overwrite && File.Exists(backup)) return;

            try
            {
                File.Copy(path, backup, overwrite);
            }
            catch (IOException e)
            {
                Debug.LogWarning("Garden: không sao lưu được file: " + e.Message);
            }
        }
    }
}
