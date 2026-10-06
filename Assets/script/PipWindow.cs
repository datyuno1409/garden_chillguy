using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

// Biến cửa sổ game thành khung PIP: không viền, khóa vị trí/kích thước, không tự tắt được.
// Chỉ chạy trong bản build Windows. Trong Unity Editor script này không làm gì (đỡ làm hỏng cửa sổ Editor).
public class PipWindow : MonoBehaviour
{
    public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    [Header("Kích thước và vị trí (pixel)")]
    [SerializeField] Vector2Int size = new Vector2Int(400, 400);
    [SerializeField] Corner corner = Corner.BottomRight;   // góc màn hình để ghim vào
    [SerializeField] Vector2Int margin = new Vector2Int(24, 24);   // cách mép bao nhiêu pixel

    [Header("Hành vi")]
    [SerializeField] bool alwaysOnTop = true;        // luôn nằm trên các app khác
    [SerializeField] bool hideFromTaskbar = true;    // ẩn khỏi thanh taskbar và Alt+Tab
    [SerializeField] int hiddenFrameRate = 5;        // khung hình/giây khi đang ẩn, để đỡ tốn máy
    [SerializeField] float lockCheckSeconds = 1f;    // bao lâu kiểm tra và ghim lại vị trí một lần

    public bool IsVisible { get; private set; } = true;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;

    const long WS_POPUP = 0x80000000L;
    const long WS_CAPTION = 0x00C00000L;       // thanh tiêu đề + viền mảnh
    const long WS_THICKFRAME = 0x00040000L;    // viền kéo giãn kích thước
    const long WS_SYSMENU = 0x00080000L;       // menu hệ thống và nút X
    const long WS_MINIMIZEBOX = 0x00020000L;   // nút thu nhỏ
    const long WS_MAXIMIZEBOX = 0x00010000L;   // nút phóng to
    const long FRAME_STYLES = WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;

    const long WS_EX_APPWINDOW = 0x00040000L;
    const long WS_EX_TOOLWINDOW = 0x00000080L;

    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_FRAMECHANGED = 0x0020;
    const uint SPI_GETWORKAREA = 0x0030;
    const int SW_HIDE = 0;
    const int SW_SHOWNOACTIVATE = 4;

    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, ref RECT rect, uint winIni);

    IntPtr window = IntPtr.Zero;
    int visibleFrameRate;
    bool allowQuit;

    void OnEnable() { Application.wantsToQuit += OnWantsToQuit; }
    void OnDisable() { Application.wantsToQuit -= OnWantsToQuit; }

    // Chặn Alt+F4 và mọi cách tắt khác. Chỉ Quit() mới cho phép thoát thật.
    bool OnWantsToQuit() => allowQuit;

    IEnumerator Start()
    {
        visibleFrameRate = Application.targetFrameRate;

        // Chờ Unity tạo xong cửa sổ rồi mới can thiệp
        for (int i = 0; i < 60 && window == IntPtr.Zero; i++)
        {
            window = FindUnityWindow();
            if (window == IntPtr.Zero) yield return null;
        }
        if (window == IntPtr.Zero)
        {
            Debug.LogError("PipWindow: không tìm thấy cửa sổ game.");
            yield break;
        }

        ApplyLock(true);

        // Unity đôi khi đặt lại kiểu cửa sổ (ví dụ khi đổi độ phân giải), nên kiểm tra và ghim lại định kỳ
        var wait = new WaitForSecondsRealtime(lockCheckSeconds);
        while (true)
        {
            yield return wait;
            if (IsLockBroken()) ApplyLock(true);
        }
    }

    // Tìm cửa sổ chính của game bằng tên lớp, chắc hơn GetActiveWindow() vì không phụ thuộc cửa sổ nào đang được focus
    static IntPtr FindUnityWindow()
    {
        IntPtr found = IntPtr.Zero;
        var text = new StringBuilder(64);
        EnumThreadWindows(GetCurrentThreadId(), (handle, _) =>
        {
            text.Length = 0;
            GetClassName(handle, text, text.Capacity);
            if (text.ToString() != "UnityWndClass") return true;
            found = handle;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    // Bỏ viền, ghim vị trí và kích thước
    void ApplyLock(bool frameChanged)
    {
        long style = ReadStyle(GWL_STYLE);
        style = (style & ~FRAME_STYLES) | WS_POPUP;
        SetWindowLongPtr(window, GWL_STYLE, new IntPtr(style));

        if (hideFromTaskbar)
        {
            long exStyle = (ReadStyle(GWL_EXSTYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
            SetWindowLongPtr(window, GWL_EXSTYLE, new IntPtr(exStyle));
        }

        RECT target = ComputeTargetRect();
        uint flags = SWP_NOACTIVATE | (frameChanged ? SWP_FRAMECHANGED : 0);
        IntPtr order = alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST;
        SetWindowPos(window, order, target.left, target.top, size.x, size.y, flags);
    }

    bool IsLockBroken()
    {
        if ((ReadStyle(GWL_STYLE) & FRAME_STYLES) != 0) return true;

        RECT target = ComputeTargetRect();
        GetWindowRect(window, out RECT now);
        return now.left != target.left || now.top != target.top
            || now.right - now.left != size.x || now.bottom - now.top != size.y;
    }

    long ReadStyle(int index) => GetWindowLongPtr(window, index).ToInt64() & 0xFFFFFFFFL;

    // Tính vị trí theo vùng làm việc (không đè lên thanh taskbar)
    RECT ComputeTargetRect()
    {
        var area = new RECT();
        SystemParametersInfo(SPI_GETWORKAREA, 0, ref area, 0);

        bool isLeft = corner == Corner.TopLeft || corner == Corner.BottomLeft;
        bool isTop = corner == Corner.TopLeft || corner == Corner.TopRight;
        int x = isLeft ? area.left + margin.x : area.right - size.x - margin.x;
        int y = isTop ? area.top + margin.y : area.bottom - size.y - margin.y;
        return new RECT { left = x, top = y, right = x + size.x, bottom = y + size.y };
    }

    // ---- Các hàm để Control Panel gọi ----

    public void Show()
    {
        if (window == IntPtr.Zero) return;
        ShowWindow(window, SW_SHOWNOACTIVATE);
        Application.targetFrameRate = visibleFrameRate;
        IsVisible = true;
    }

    public void Hide()
    {
        if (window == IntPtr.Zero) return;
        ShowWindow(window, SW_HIDE);
        Application.targetFrameRate = hiddenFrameRate;
        IsVisible = false;
    }

    public void Quit()
    {
        allowQuit = true;
        Application.Quit();
    }
#else
    // Trong Editor hoặc nền tảng khác: giữ các hàm để nút bấm không báo lỗi, nhưng không làm gì
    public void Show() { IsVisible = true; Debug.Log("PipWindow.Show (chỉ có tác dụng trong bản build Windows)"); }
    public void Hide() { IsVisible = false; Debug.Log("PipWindow.Hide (chỉ có tác dụng trong bản build Windows)"); }
    public void Quit() { Debug.Log("PipWindow.Quit (chỉ có tác dụng trong bản build Windows)"); }
#endif
}
