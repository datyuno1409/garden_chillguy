using UnityEngine;

// Thanh tiêu đề hiện dần khi rê chuột vào cửa sổ PIP: giữ chuột trái trên thanh để kéo cửa sổ (PipWindow xử lý),
// và 3 nút Ẩn, Về Control Panel, Tắt. Rời chuột thì mờ dần.
// Vẽ bằng IMGUI nên không cần asset nào. Gắn cùng chỗ với PipWindow.
[RequireComponent(typeof(PipWindow))]
public class PipTitleBar : MonoBehaviour
{
    const float BarHeight = 28f;
    const float ButtonWidth = 38f;
    const float IconSize = 12f;
    const float LineThickness = 1.5f;
    const float FadeSpeed = 8f;   // alpha mỗi giây

    static readonly Color BarColor = new Color(0.08f, 0.1f, 0.09f, 0.82f);
    static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.18f);
    static readonly Color CloseHoverColor = new Color(0.85f, 0.2f, 0.2f, 0.9f);
    static readonly Color IconColor = new Color(1f, 1f, 1f, 0.95f);

    enum Icon { Hide, ControlPanel, Close }

    PipWindow pip;
    float alpha;

    void Awake()
    {
        pip = GetComponent<PipWindow>();
        pip.TitleBarHeight = BarHeight;                 // phần còn lại của thanh dùng để kéo cửa sổ
        pip.TitleBarButtonsWidth = ButtonWidth * 3f;    // vùng 3 nút bấm
    }

    void Update()
    {
        float target = pip.IsCursorOver ? 1f : 0f;
        alpha = Mathf.MoveTowards(alpha, target, FadeSpeed * Time.unscaledDeltaTime);
    }

    void OnGUI()
    {
        if (alpha <= 0f) return;

        Fill(new Rect(0f, 0f, Screen.width, BarHeight), BarColor);

        float closeX = Screen.width - ButtonWidth;
        float panelX = closeX - ButtonWidth;
        float hideX = panelX - ButtonWidth;

        if (IconButton(new Rect(hideX, 0f, ButtonWidth, BarHeight), Icon.Hide, HoverColor))
        {
            pip.Hide();
            pip.EnsureControlPanelRunning();   // PIP không có taskbar, cần Control Panel để hiện lại
        }
        if (IconButton(new Rect(panelX, 0f, ButtonWidth, BarHeight), Icon.ControlPanel, HoverColor))
            pip.OpenControlPanel();
        if (IconButton(new Rect(closeX, 0f, ButtonWidth, BarHeight), Icon.Close, CloseHoverColor))
            pip.Quit();

        GUI.color = Color.white;
    }

    bool IconButton(Rect rect, Icon icon, Color hoverColor)
    {
        if (Event.current.type == EventType.Repaint)
        {
            if (rect.Contains(Event.current.mousePosition)) Fill(rect, hoverColor);
            DrawIcon(rect, icon);
        }
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    void DrawIcon(Rect rect, Icon icon)
    {
        Vector2 center = rect.center;
        float half = IconSize * 0.5f;

        switch (icon)
        {
            case Icon.Hide:
                Fill(new Rect(center.x - half, center.y + half * 0.5f, IconSize, LineThickness), IconColor);
                break;

            case Icon.ControlPanel:
                var box = new Rect(center.x - half, center.y - half, IconSize, IconSize);
                Fill(new Rect(box.x, box.y, box.width, LineThickness), IconColor);
                Fill(new Rect(box.x, box.yMax - LineThickness, box.width, LineThickness), IconColor);
                Fill(new Rect(box.x, box.y, LineThickness, box.height), IconColor);
                Fill(new Rect(box.xMax - LineThickness, box.y, LineThickness, box.height), IconColor);
                break;

            case Icon.Close:
                var line = new Rect(center.x - half - 1f, center.y - LineThickness * 0.5f, IconSize + 2f, LineThickness);
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, center);
                Fill(line, IconColor);
                GUI.matrix = saved;
                GUIUtility.RotateAroundPivot(-45f, center);
                Fill(line, IconColor);
                GUI.matrix = saved;
                break;
        }
    }

    // Tô một hình chữ nhật, nhân thêm độ mờ hiện tại của thanh nút
    void Fill(Rect rect, Color color)
    {
        GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }
}
