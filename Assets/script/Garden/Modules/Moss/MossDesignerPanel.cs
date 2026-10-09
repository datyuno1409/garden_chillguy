using System;
using Garden.Core;
using UnityEngine;

namespace Garden.Moss
{
    // Mục "Rêu" trong bảng thiết kế: tốc độ mọc và độ phủ tối đa
    public sealed class MossDesignerPanel : IGardenDesignerPanel
    {
        static readonly string[] SpeedLabels = { "Chậm (28 ngày)", "Vừa (14 ngày)", "Nhanh (7 ngày)" };
        static readonly float[] SpeedDays = { 28f, 14f, 7f };

        public string Title => "Rêu";

        public GardenSettings Draw(GardenSettings settings, DateTime nowUtc)
        {
            MossSettings defaults = MossSettings.Default;
            MossDesign design = settings.GetOrDefault(MossModule.ModuleId,
                new MossDesign { daysToFullCover = defaults.daysToFullCover, maxCoverage = defaults.maxCoverage });

            GUILayout.Label("Tốc độ rêu mọc");
            int speed = GUILayout.Toolbar(NearestSpeed(design.daysToFullCover), SpeedLabels);
            design.daysToFullCover = SpeedDays[speed];

            GUILayout.Label($"Độ phủ tối đa: {design.maxCoverage:P0}");
            design.maxCoverage = GUILayout.HorizontalSlider(design.maxCoverage, 0.2f, 0.8f);

            return settings.WithSection(MossModule.ModuleId, design);
        }

        static int NearestSpeed(float days)
        {
            int best = 0;
            for (int i = 1; i < SpeedDays.Length; i++)
            {
                if (Mathf.Abs(SpeedDays[i] - days) < Mathf.Abs(SpeedDays[best] - days)) best = i;
            }
            return best;
        }
    }
}
