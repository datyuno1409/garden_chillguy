using System;
using Garden.Core;
using Garden.Layout;
using Garden.Moss;
using Garden.Rain;
using UnityEngine;

// Bảng thiết kế trong Control Panel: vẽ bảng của từng mô-đun, và ghi cài đặt người chơi chọn ra file chung
// để cửa sổ vườn đọc và áp dụng ngay. Chỉ nhận kết quả của một bảng khi người chơi thật sự chỉnh (GUI.changed),
// nên chỉ mở bảng ra thì không ghi gì. Việc ghi file do DebouncedSettingsWriter lo.
// Thêm mô-đun mới: viết IGardenDesignerPanel trong thư mục mô-đun rồi thêm MỘT dòng vào DefaultPanels.
public sealed class DesignerBoard
{
    readonly IGardenDesignerPanel[] panels;
    readonly DebouncedSettingsWriter writer;

    GardenSettings settings;
    Vector2 scroll;
    GUIStyle titleStyle;

    public static IGardenDesignerPanel[] DefaultPanels() => new IGardenDesignerPanel[]
    {
        new LayoutDesignerPanel(),
        new RainDesignerPanel(),
        new MossDesignerPanel(),
    };

    public DesignerBoard(string settingsPath, IGardenDesignerPanel[] designerPanels)
    {
        panels = designerPanels;
        settings = GardenSettingsStore.Load(settingsPath);
        writer = new DebouncedSettingsWriter(changed => GardenSettingsStore.Save(settingsPath, changed));
    }

    public void Draw()
    {
        titleStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 18 };

        scroll = GUILayout.BeginScrollView(scroll);
        foreach (IGardenDesignerPanel panel in panels) DrawPanel(panel);
        GUILayout.EndScrollView();
    }

    void DrawPanel(IGardenDesignerPanel panel)
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(panel.Title, titleStyle);

        bool changedBefore = GUI.changed;
        GUI.changed = false;
        GardenSettings next = panel.Draw(settings, DateTime.UtcNow);
        if (GUI.changed)
        {
            settings = next;
            writer.MarkChanged(settings, Time.unscaledTime);
        }
        GUI.changed = changedBefore;

        GUILayout.EndVertical();
        GUILayout.Space(6f);
    }

    // Gọi mỗi khung hình
    public void Update() => writer.Update(Time.unscaledTime);

    // Không để mất lần chỉnh cuối khi đóng
    public void Flush() => writer.Flush(Time.unscaledTime);
}
