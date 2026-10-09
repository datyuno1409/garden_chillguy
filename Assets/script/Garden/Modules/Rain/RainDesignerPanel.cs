using System;
using Garden.Core;
using UnityEngine;

namespace Garden.Rain
{
    // Mục "Mưa" trong bảng thiết kế: bật/tắt mưa tự nhiên, tần suất, và gọi mưa ngay.
    // Control Panel chỉ nhận kết quả khi người chơi thật sự bấm/chỉnh (GUI.changed), nên vẽ hoài không ghi file.
    public sealed class RainDesignerPanel : IGardenDesignerPanel
    {
        const double ManualRainMinutes = 30;
        static readonly string[] FrequencyLabels = { "Ít", "Vừa", "Nhiều" };

        public string Title => "Mưa";

        public GardenSettings Draw(GardenSettings settings, DateTime nowUtc)
        {
            RainSettings rain = settings.GetOrDefault(RainModule.ModuleId, RainSettings.Default);

            rain.enabled = GUILayout.Toggle(rain.enabled, "Cho phép mưa tự nhiên");

            GUI.enabled = rain.enabled;
            GUILayout.Label("Tần suất mưa");
            rain.frequency = GUILayout.Toolbar(Mathf.Clamp(rain.frequency, 0, RainSchedule.FrequencyCount - 1), FrequencyLabels);
            GUI.enabled = true;

            GUILayout.Space(6);
            bool manualActive = rain.HasManualRain && nowUtc.Ticks < rain.manualUntilTicksUtc;
            if (manualActive)
            {
                double remaining = (new DateTime(rain.manualUntilTicksUtc, DateTimeKind.Utc) - nowUtc).TotalMinutes;
                GUILayout.Label($"Đang mưa do bạn gọi, còn khoảng {Math.Ceiling(remaining)} phút");
                if (GUILayout.Button("Tạnh mưa"))
                {
                    rain = rain.WithoutManualRain();
                    GUI.changed = true;
                }
            }
            else if (GUILayout.Button($"Mưa ngay ({ManualRainMinutes:0} phút)"))
            {
                rain = rain.WithManualRain(nowUtc, ManualRainMinutes);
                GUI.changed = true;
            }

            return settings.WithSection(RainModule.ModuleId, rain);
        }
    }
}
