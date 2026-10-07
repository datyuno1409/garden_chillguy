using System;
using Garden.Growth;
using UnityEngine;

// Nối thời gian thật với độ phủ rêu: đọc trạng thái vườn, cộng thời gian đã trôi qua (kể cả lúc tắt app),
// đặt độ phủ rêu, và lưu lại định kỳ. Dòng lệnh "-gardenAgeDays N" để xem thử rêu ở tuổi N ngày (không lưu).
public class GardenGrowthSystem : MonoBehaviour
{
    const string AgeArgument = "-gardenAgeDays";

    [SerializeField] MossGrowth moss;
    [SerializeField] GrowthSettings settings = GrowthSettings.Default;
    [SerializeField] float refreshSeconds = 30f;   // bao lâu cập nhật độ phủ một lần (rêu mọc quá chậm để cần nhanh hơn)
    [SerializeField] float saveSeconds = 300f;     // bao lâu lưu một lần, phòng khi app bị tắt đột ngột

    GardenState state;
    float untilRefresh;
    float untilSave;
    bool isPreview;   // đang xem thử tuổi tuỳ ý: không lưu để không làm hỏng vườn thật

    public GardenState State => state;

    public void Configure(MossGrowth target) { moss = target; }

    void Start()
    {
        DateTime now = DateTime.UtcNow;
        state = GardenStateStore.LoadOrCreate(GardenPaths.StateFile, now, UnityEngine.Random.Range(1, 100000));
        state = GrowthModel.Advance(state, now, settings);

        if (TryReadAgeArgument(out double previewAge)) PreviewAge(previewAge);
        else Apply();

        if (!isPreview) Save();
        untilRefresh = refreshSeconds;
        untilSave = saveSeconds;
    }

    void Update()
    {
        if (isPreview) return;

        float dt = Time.unscaledDeltaTime;
        untilRefresh -= dt;
        untilSave -= dt;

        if (untilRefresh <= 0f)
        {
            state = GrowthModel.Advance(state, DateTime.UtcNow, settings);
            Apply();
            untilRefresh = refreshSeconds;
        }

        if (untilSave <= 0f)
        {
            Save();
            untilSave = saveSeconds;
        }
    }

    void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
    void OnApplicationQuit() { SaveNow(); }

    // Xem thử rêu ở tuổi bất kỳ mà không ghi vào file lưu
    public void PreviewAge(double ageDays)
    {
        isPreview = true;
        state = state.WithAge(ageDays, DateTime.UtcNow);
        Apply();
    }

    void Apply()
    {
        if (moss == null) return;
        moss.Seed = (state.seed % 1000) * 0.37f;
        moss.Coverage = GrowthModel.Coverage(state.mossAgeDays, settings);
    }

    void SaveNow()
    {
        if (isPreview) return;
        state = GrowthModel.Advance(state, DateTime.UtcNow, settings);
        Save();
    }

    void Save()
    {
        try
        {
            GardenStateStore.Save(GardenPaths.StateFile, state);
        }
        catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
        {
            // Không lưu được lần này thì lần sau thử lại, không làm sập game
            Debug.LogWarning("GardenGrowthSystem: không lưu được trạng thái vườn: " + e.Message);
        }
    }

    static bool TryReadAgeArgument(out double ageDays)
    {
        ageDays = 0;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != AgeArgument) continue;
            return double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out ageDays) && ageDays >= 0;
        }
        return false;
    }
}
