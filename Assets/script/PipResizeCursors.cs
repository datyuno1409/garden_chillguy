using System;
using UnityEngine;

// Mép hoặc góc nào của cửa sổ PIP đang được chỉ vào
[Flags]
public enum PipEdge { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

// Con trỏ mũi tên hai đầu cho việc đổi kích thước. Tự vẽ lúc chạy nên không cần asset.
public static class PipResizeCursors
{
    const int CursorSize = 24;
    const float ShaftHalfLength = 9f;
    const float ShaftHalfWidth = 1.2f;
    const float HeadLength = 5f;
    const float HeadSlope = 0.9f;   // đầu mũi tên xoè ra bao nhiêu theo độ dài

    static Texture2D horizontal, vertical, diagonalDown, diagonalUp;

    // Đổi con trỏ theo mép đang chỉ vào. None thì trả về con trỏ mặc định.
    public static void Apply(PipEdge edges)
    {
        Texture2D texture = TextureFor(edges);
        Cursor.SetCursor(texture, new Vector2(CursorSize / 2f, CursorSize / 2f), CursorMode.Auto);
    }

    static Texture2D TextureFor(PipEdge edges)
    {
        bool left = (edges & PipEdge.Left) != 0, right = (edges & PipEdge.Right) != 0;
        bool top = (edges & PipEdge.Top) != 0, bottom = (edges & PipEdge.Bottom) != 0;

        if (edges == PipEdge.None) return null;
        if ((left || right) && (top || bottom))
        {
            bool isDown = (left && top) || (right && bottom);   // góc trên-trái hoặc dưới-phải: nét chéo "\"
            return isDown ? (diagonalDown ??= Build(-45f)) : (diagonalUp ??= Build(45f));
        }
        return (top || bottom) ? (vertical ??= Build(90f)) : (horizontal ??= Build(0f));
    }

    // Mũi tên hai đầu màu trắng viền đen, xoay theo góc (độ)
    static Texture2D Build(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
        var filled = new bool[CursorSize * CursorSize];

        float center = (CursorSize - 1) / 2f;
        for (int y = 0; y < CursorSize; y++)
        {
            for (int x = 0; x < CursorSize; x++)
            {
                float dx = x - center, dy = y - center;
                float along = dx * cos + dy * sin;     // dọc theo thân mũi tên
                float across = -dx * sin + dy * cos;   // vuông góc với thân
                filled[y * CursorSize + x] = InArrow(along, across);
            }
        }

        var pixels = new Color32[CursorSize * CursorSize];
        for (int y = 0; y < CursorSize; y++)
        {
            for (int x = 0; x < CursorSize; x++)
            {
                int i = y * CursorSize + x;
                if (filled[i]) pixels[i] = new Color32(255, 255, 255, 255);
                else if (HasFilledNeighbour(filled, x, y)) pixels[i] = new Color32(0, 0, 0, 255);
            }
        }

        var texture = new Texture2D(CursorSize, CursorSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    static bool InArrow(float along, float across)
    {
        float distance = Mathf.Abs(along);
        if (distance <= ShaftHalfLength && Mathf.Abs(across) <= ShaftHalfWidth) return true;

        float intoHead = distance - (ShaftHalfLength - HeadLength);   // 0 ở gốc đầu mũi tên, tăng dần tới đầu nhọn
        float tipEnd = ShaftHalfLength + 2f;
        return intoHead >= 0f && distance <= tipEnd && Mathf.Abs(across) <= (tipEnd - distance) * HeadSlope + 0.2f;
    }

    static bool HasFilledNeighbour(bool[] filled, int x, int y)
    {
        for (int ny = y - 1; ny <= y + 1; ny++)
        {
            for (int nx = x - 1; nx <= x + 1; nx++)
            {
                if (nx < 0 || ny < 0 || nx >= CursorSize || ny >= CursorSize) continue;
                if (filled[ny * CursorSize + nx]) return true;
            }
        }
        return false;
    }
}
