using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Garden.Core
{
    // Gắn vào gốc của khu vườn. Tìm mọi mô-đun nằm dưới nó, chạy GardenSession, cập nhật và lưu định kỳ,
    // và theo dõi file cài đặt (do Control Panel ghi) để áp dụng ngay cho các thành phần cần nó.
    // Tham số dòng lệnh dành cho dev: xem GardenArgs.
    public sealed class GardenHost : MonoBehaviour
    {
        [SerializeField] float refreshSeconds = 30f;        // bao lâu cho các mô-đun chạy thời gian trôi qua một lần
        [SerializeField] float saveSeconds = 300f;          // bao lâu lưu một lần, phòng khi app bị tắt đột ngột
        [SerializeField] float settingsPollSeconds = 1f;    // bao lâu kiểm tra file cài đặt có đổi không

        GardenSession session;
        IGardenClickHandler[] clickHandlers = Array.Empty<IGardenClickHandler>();
        IGardenConfigurable[] configurables = Array.Empty<IGardenConfigurable>();
        string settingsPath;
        DateTime lastSettingsWriteUtc = DateTime.MinValue;
        float untilRefresh;
        float untilSave;
        float untilSettingsPoll;

        public GardenSession Session => session;

        void Start()
        {
            IGardenModule[] modules = GetComponentsInChildren<IGardenModule>(true);
            clickHandlers = GetComponentsInChildren<IGardenClickHandler>(true);
            configurables = GetComponentsInChildren<IGardenConfigurable>(true);
            settingsPath = GardenArgs.SettingsFilePath();

            bool hasPreviewAge = TryReadAgeArgument(out double previewAge);
            session = new GardenSession(GardenArgs.StateFilePath(), modules, () => DateTime.UtcNow);
            session.Start(UnityEngine.Random.Range(1, 100000), preview: hasPreviewAge);
            if (hasPreviewAge) session.PreviewAge(previewAge);

            ReloadSettings();   // áp cài đặt người chơi đã chọn (sau khi các mô-đun nạp xong dữ liệu)

            untilRefresh = refreshSeconds;
            untilSave = saveSeconds;
            untilSettingsPoll = settingsPollSeconds;
        }

        void Update()
        {
            if (session == null) return;

            float dt = Time.unscaledDeltaTime;
            untilSettingsPoll -= dt;
            if (untilSettingsPoll <= 0f)
            {
                PollSettings();
                untilSettingsPoll = settingsPollSeconds;
            }

            if (session.IsPreview) return;

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

        // Người chơi click vào vườn: chuyển cho các mô-đun quan tâm.
        // Chưa chạy Start (ví dụ thử ở Editor) thì tìm mô-đun tại chỗ.
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

        // Chỉ đọc lại khi file thật sự đổi (so thời điểm ghi), nên kiểm tra mỗi giây gần như không tốn gì
        void PollSettings()
        {
            DateTime written = File.Exists(settingsPath) ? File.GetLastWriteTimeUtc(settingsPath) : DateTime.MinValue;
            if (written == lastSettingsWriteUtc) return;
            ReloadSettings();
        }

        void ReloadSettings()
        {
            lastSettingsWriteUtc = File.Exists(settingsPath) ? File.GetLastWriteTimeUtc(settingsPath) : DateTime.MinValue;
            ConfigurableApplier.Apply(configurables, GardenSettingsStore.Load(settingsPath));
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
            return GardenArgs.TryGet(GardenArgs.AgeDays, out string text)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out ageDays) && ageDays >= 0;
        }
    }
}
