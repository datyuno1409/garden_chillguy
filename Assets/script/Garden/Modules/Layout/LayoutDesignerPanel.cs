using System;
using Garden.Core;
using UnityEngine;

namespace Garden.Layout
{
    // Mục "Bố cục" trong bảng thiết kế: đổi hình các phiến đá, ẩn/hiện nấm, cỏ, lá.
    // Control Panel chỉ nhận kết quả khi người chơi thật sự bấm/chỉnh (GUI.changed).
    public sealed class LayoutDesignerPanel : IGardenDesignerPanel
    {
        const int MaxSeed = 100000;

        public string Title => "Bố cục";

        public GardenSettings Draw(GardenSettings settings, DateTime nowUtc)
        {
            LayoutSettings layout = settings.GetOrDefault(GardenLayout.ModuleId, default(LayoutSettings));

            foreach (LayoutSlotInfo info in LayoutCatalog.Slots)
            {
                SlotSetting choice = layout.Get(info.id);

                GUILayout.BeginHorizontal();
                GUILayout.Label(info.label, GUILayout.Width(150f));

                if (info.canHide)
                {
                    bool shown = GUILayout.Toggle(!choice.hidden, "Hiện", GUILayout.Width(60f));
                    choice.hidden = !shown;
                }

                if (info.canReshuffle && GUILayout.Button("Đổi hình", GUILayout.Width(80f)))
                {
                    choice.seed = UnityEngine.Random.Range(1, MaxSeed);
                    GUI.changed = true;
                }
                GUILayout.EndHorizontal();

                layout = layout.With(choice);
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("Đặt lại bố cục gốc"))
            {
                layout = default;
                GUI.changed = true;
            }

            return settings.WithSection(GardenLayout.ModuleId, layout);
        }
    }
}
