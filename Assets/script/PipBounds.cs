using System.Globalization;
using UnityEngine;

// Vị trí và kích thước cửa sổ PIP, lưu bằng PlayerPrefs để lần sau mở lại đúng chỗ cũ.
public readonly struct PipBounds
{
    public const int MinSize = 160;          // nhỏ hơn mức này thì khó nhìn và khó kéo lại
    const int MinVisiblePixels = 80;         // phải còn ít nhất chừng này trong màn hình để người dùng còn nắm được cửa sổ
    const string PrefsKey = "pip.bounds";    // lưu dạng "x,y,rộng,cao"

    public readonly RectInt rect;

    public PipBounds(RectInt rect) { this.rect = rect; }

    public static bool TryLoad(out PipBounds bounds)
    {
        bounds = default;
        string[] parts = PlayerPrefs.GetString(PrefsKey, "").Split(',');
        if (parts.Length != 4) return false;

        var values = new int[4];
        for (int i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i])) return false;
        }
        if (values[2] < MinSize || values[3] < MinSize) return false;

        bounds = new PipBounds(new RectInt(values[0], values[1], values[2], values[3]));
        return true;
    }

    public void Save()
    {
        string text = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", rect.x, rect.y, rect.width, rect.height);
        PlayerPrefs.SetString(PrefsKey, text);
        PlayerPrefs.Save();
    }

    // Còn nằm trong vùng hiển thị của các màn hình không (ví dụ màn hình phụ đã bị rút ra thì không còn)
    public bool IsReachable(RectInt virtualScreen)
    {
        int visibleWidth = Mathf.Min(rect.xMax, virtualScreen.xMax) - Mathf.Max(rect.xMin, virtualScreen.xMin);
        int visibleHeight = Mathf.Min(rect.yMax, virtualScreen.yMax) - Mathf.Max(rect.yMin, virtualScreen.yMin);
        return visibleWidth >= MinVisiblePixels && visibleHeight >= MinVisiblePixels;
    }
}
