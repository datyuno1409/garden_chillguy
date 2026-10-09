using System;

namespace Garden.Core
{
    // Đọc tham số dòng lệnh dành cho dev (dùng chung cho cửa sổ vườn và Control Panel):
    //   -gardenAgeDays N           xem thử ở tuổi N ngày, không ghi file lưu
    //   -gardenStateFile <path>    dùng file lưu khác
    //   -gardenSettingsFile <path> dùng file cài đặt khác
    //   -gardenScreenshot <path>   chụp màn hình rồi thoát, để kiểm tra bản build tự động (xem DevScreenshot)
    //   -gardenScreenshotDelay N   chờ N giây trước khi chụp
    //   -gardenTab N               (Control Panel) mở sẵn tab số N
    public static class GardenArgs
    {
        public const string AgeDays = "-gardenAgeDays";
        public const string StateFile = "-gardenStateFile";
        public const string SettingsFile = "-gardenSettingsFile";
        public const string Screenshot = "-gardenScreenshot";
        public const string ScreenshotDelay = "-gardenScreenshotDelay";
        public const string Tab = "-gardenTab";

        // Giá trị đứng ngay sau một tham số
        public static bool TryGet(string name, out string value) => TryGet(Environment.GetCommandLineArgs(), name, out value);

        public static bool TryGet(string[] args, string name, out string value)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != name) continue;
                value = args[i + 1];
                return true;
            }
            value = null;
            return false;
        }

        // Đường dẫn file: tham số dòng lệnh nếu có, không thì vị trí chuẩn
        public static string SettingsFilePath() => TryGet(SettingsFile, out string custom) ? custom : GardenPaths.SettingsFile;
        public static string StateFilePath() => TryGet(StateFile, out string custom) ? custom : GardenPaths.StateFile;
    }
}
