using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Garden.Core
{
    // Gắn vào gốc của khu vườn. Tìm mọi mô-đun nằm dưới nó, chạy GardenSession, cập nhật và lưu định kỳ.
    // Dòng lệnh dành cho dev:
    //   -gardenAgeDays N          xem thử mọi mô-đun ở tuổi N ngày, không ghi vào file lưu
    //   -gardenStateFile <path>   dùng file lưu khác (để thử nâng cấp file mà không đụng vào vườn thật)
    public sealed class GardenHost : MonoBehaviour
    {
        const string AgeArgument = "-gardenAgeDays";
        const string StateFileArgument = "-gardenStateFile";

        [SerializeField] float refreshSeconds = 30f;   // bao lâu cho các mô-đun chạy thời gian trôi qua một lần
        [SerializeField] float saveSeconds = 300f;     // bao lâu lưu một lần, phòng khi app bị tắt đột ngột

        GardenSession session;
        IGardenClickHandler[] clickHandlers = Array.Empty<IGardenClickHandler>();
        float untilRefresh;
        float untilSave;

        public GardenSession Session => session;

        void Start()
        {
            IGardenModule[] modules = GetComponentsInChildren<IGardenModule>(true);
            clickHandlers = GetComponentsInChildren<IGardenClickHandler>(true);

            bool hasPreviewAge = TryReadAgeArgument(out double previewAge);
            string stateFile = TryReadArgument(StateFileArgument, out string customPath) ? customPath : GardenPaths.StateFile;
            session = new GardenSession(stateFile, modules, () => DateTime.UtcNow);
            session.Start(UnityEngine.Random.Range(1, 100000), preview: hasPreviewAge);
            if (hasPreviewAge) session.PreviewAge(previewAge);

            untilRefresh = refreshSeconds;
            untilSave = saveSeconds;
        }

        void Update()
        {
            if (session == null || session.IsPreview) return;

            float dt = Time.unscaledDeltaTime;
            untilRefresh -= dt;
            untilSave -= dt;

            if (untilRefresh <= 0f)
            {
                session.Refresh();
                untilRefresh = refreshSeconds;
            }

            if (untilSave <= 0f)
            {
                TrySave();
                untilSave = saveSeconds;
            }
        }

        void OnApplicationPause(bool paused) { if (paused) TrySave(); }
        void OnApplicationQuit() { TrySave(); }

        // Người chơi click vào vườn: chuyển cho các mô-đun quan tâm
        // Chưa chạy Start (ví dụ thử ở Editor) thì tìm mô-đun tại chỗ
        public int DispatchClick(GardenClick click)
        {
            IGardenClickHandler[] handlers = clickHandlers.Length > 0 ? clickHandlers : GetComponentsInChildren<IGardenClickHandler>(true);
            return ClickDispatcher.Dispatch(handlers, click);
        }

        // Xem thử ở tuổi bất kỳ, dùng được cả ở Editor khi chưa chạy (menu Tools/Garden/Dev)
        public void PreviewAge(double ageDays)
        {
            if (session != null) session.PreviewAge(ageDays);
            else GardenSession.PreviewAll(GetComponentsInChildren<IGardenModule>(true), ageDays);
        }

        void TrySave()
        {
            if (session == null) return;
            try
            {
                session.Save();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Không lưu được lần này thì lần sau thử lại, không làm sập game
                Debug.LogWarning("GardenHost: không lưu được trạng thái vườn: " + e.Message);
            }
        }

        static bool TryReadAgeArgument(out double ageDays)
        {
            ageDays = 0;
            return TryReadArgument(AgeArgument, out string text)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out ageDays) && ageDays >= 0;
        }

        // Đọc giá trị đứng ngay sau một tham số dòng lệnh
        static bool TryReadArgument(string name, out string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != name) continue;
                value = args[i + 1];
                return true;
            }
            value = null;
            return false;
        }
    }
}
