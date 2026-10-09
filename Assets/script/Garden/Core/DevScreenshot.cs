using UnityEngine;

namespace Garden.Core
{
    // Dev: chạy app với "-gardenScreenshot <path>" thì sau vài giây tự chụp màn hình ra file rồi (xin) thoát,
    // để kiểm tra giao diện / cảnh của BẢN BUILD mà không cần bấm tay. Không có tham số thì không làm gì và không tốn gì.
    // "-gardenScreenshotDelay N" đổi số giây chờ (mặc định 3). Cửa sổ vườn chặn việc tự thoát nên cần tắt từ bên ngoài.
    public sealed class DevScreenshot : MonoBehaviour
    {
        const float DefaultDelaySeconds = 3f;
        const float QuitAfterSeconds = 1.5f;

        string path;
        float delay = DefaultDelaySeconds;
        float quitAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (!GardenArgs.TryGet(GardenArgs.Screenshot, out string requested)) return;

            var go = new GameObject("DevScreenshot");
            DontDestroyOnLoad(go);
            var shot = go.AddComponent<DevScreenshot>();
            shot.path = requested;
            if (GardenArgs.TryGet(GardenArgs.ScreenshotDelay, out string text) && float.TryParse(text, out float seconds)) shot.delay = seconds;
        }

        void Update()
        {
            if (quitAt < 0f && Time.unscaledTime > delay)
            {
                ScreenCapture.CaptureScreenshot(path);
                quitAt = Time.unscaledTime + QuitAfterSeconds;
            }
            else if (quitAt > 0f && Time.unscaledTime > quitAt)
            {
                quitAt = float.MaxValue;
                Application.Quit();
            }
        }
    }
}
