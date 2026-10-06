using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

// Biến cửa sổ game thành khung PIP: không thanh tiêu đề, luôn nổi, không tự tắt được.
// Kéo mép để đổi kích thước, giữ chuột phải để di chuyển (chuột trái dành cho thao tác trong game).
// Chỉ chạy trong bản build Windows. Trong Unity Editor script này không làm gì (đỡ làm hỏng cửa sổ Editor).
public class PipWindow : MonoBehaviour
{
    public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    [Header("Kích thước và vị trí ban đầu (pixel)")]
    [SerializeField] Vector2Int size = new Vector2Int(400, 400);
    [SerializeField] Corner corner = Corner.BottomRight;   // góc màn hình để ghim vào
    [SerializeField] Vector2Int margin = new Vector2Int(24, 24);   // cách mép bao nhiêu pixel

    [Header("Hành vi")]
    [SerializeField] bool alwaysOnTop = true;        // luôn nằm trên các app khác
    [SerializeField] bool hideFromTaskbar = true;    // ẩn khỏi thanh taskbar và Alt+Tab
    [SerializeField] int hiddenFrameRate = 5;        // khung hình/giây khi đang ẩn, để đỡ tốn máy
    [SerializeField] float lockCheckSeconds = 1f;    // bao lâu kiểm tra và bỏ lại thanh tiêu đề nếu Unity đặt lại kiểu cửa sổ

    public bool IsVisible { get; private set; } = true;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;

    const long WS_POPUP = 0x80000000L;
    const long WS_CAPTION = 0x00C00000L;       // thanh tiêu đề + viền mảnh
    const long WS_THICKFRAME = 0x00040000L;    // viền kéo giãn kích thước (giữ lại để kéo mép đổi cỡ)
    const long WS_SYSMENU = 0x00080000L;       // menu hệ thống và nút X
    const long WS_MINIMIZEBOX = 0x00020000L;   // nút thu nhỏ
    const long WS_MAXIMIZEBOX = 0x00010000L;   // nút phóng to
    const long REMOVED_STYLES = WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;

    // Control Panel là app Unity riêng: tìm theo lớp cửa sổ và tên sản phẩm (xem PipBuild), file exe nằm cạnh thư mục Aquarium
    const string ControlPanelClass = "UnityWndClass";
    const string ControlPanelTitle = "ControlPanel";
    const string ControlPanelExePath = "../ControlPanel/ControlPanel.exe";
    const int SW_RESTORE = 9;

    const int VK_RBUTTON = 0x02;
    const int DragFrameRate = 60;              // tăng khung hình khi đang kéo cho mượt
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_NOZORDER = 0x0004;

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

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x, y; }

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
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string windowName);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int virtualKey);

    IntPtr window = IntPtr.Zero;
    int visibleFrameRate;
    bool allowQuit;

    bool dragging;
    POINT dragCursorStart;
    Vector2Int dragWindowStart;

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

        ApplyStyle();
        PlaceAtCorner();

        // Unity đôi khi đặt lại kiểu cửa sổ (ví dụ khi đổi độ phân giải), nên kiểm tra và bỏ lại thanh tiêu đề định kỳ.
        // Chỉ sửa kiểu cửa sổ, không đụng vị trí và kích thước vì người dùng được tự kéo.
        var wait = new WaitForSecondsRealtime(lockCheckSeconds);
        while (true)
        {
            yield return wait;
            if (HasRemovedStyles()) ApplyStyle();
        }
    }

    // Giữ chuột phải để kéo cửa sổ đi chỗ khác: cửa sổ bám theo con trỏ cho tới khi thả chuột.
    // Dùng toạ độ con trỏ trên màn hình (không dùng Input.mousePosition vì nó đổi theo khi cửa sổ di chuyển).
    void Update()
    {
        if (window == IntPtr.Zero || !IsVisible) return;

        if (!dragging)
        {
            if (Input.GetMouseButtonDown(1)) BeginDrag();
            return;
        }

        if ((GetAsyncKeyState(VK_RBUTTON) & 0x8000) == 0)
        {
            EndDrag();
            return;
        }

        GetCursorPos(out POINT cursor);
        int x = dragWindowStart.x + cursor.x - dragCursorStart.x;
        int y = dragWindowStart.y + cursor.y - dragCursorStart.y;
        SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    void BeginDrag()
    {
        GetCursorPos(out dragCursorStart);
        GetWindowRect(window, out RECT rect);
        dragWindowStart = new Vector2Int(rect.left, rect.top);
        dragging = true;
        Application.targetFrameRate = DragFrameRate;
    }

    void EndDrag()
    {
        dragging = false;
        Application.targetFrameRate = visibleFrameRate;
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

    // Bỏ thanh tiêu đề và các nút hệ thống, giữ viền mỏng để kéo đổi kích thước, bật luôn nổi và ẩn khỏi taskbar.
    // Giữ nguyên vị trí và kích thước hiện tại.
    void ApplyStyle()
    {
        long style = ReadStyle(GWL_STYLE);
        style = (style & ~REMOVED_STYLES) | WS_POPUP | WS_THICKFRAME;
        SetWindowLongPtr(window, GWL_STYLE, new IntPtr(style));

        if (hideFromTaskbar)
        {
            long exStyle = (ReadStyle(GWL_EXSTYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
            SetWindowLongPtr(window, GWL_EXSTYLE, new IntPtr(exStyle));
        }

        IntPtr order = alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST;
        SetWindowPos(window, order, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE);
    }

    // Đặt cửa sổ vào góc màn hình với kích thước ban đầu (chỉ làm một lần lúc mở)
    void PlaceAtCorner()
    {
        RECT target = ComputeTargetRect();
        IntPtr order = alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST;
        SetWindowPos(window, order, target.left, target.top, size.x, size.y, SWP_NOACTIVATE);
    }

    bool HasRemovedStyles() => (ReadStyle(GWL_STYLE) & REMOVED_STYLES) != 0;

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
        dragging = false;
        ShowWindow(window, SW_HIDE);
        Application.targetFrameRate = hiddenFrameRate;
        IsVisible = false;
    }

    public void Quit()
    {
        allowQuit = true;
        Application.Quit();
    }

    // Con trỏ đang nằm trong cửa sổ PIP không (tính theo toạ độ màn hình nên đúng cả khi cửa sổ không có focus)
    public bool IsCursorOver
    {
        get
        {
            if (window == IntPtr.Zero || !IsVisible) return false;
            GetCursorPos(out POINT cursor);
            GetWindowRect(window, out RECT rect);
            return cursor.x >= rect.left && cursor.x < rect.right && cursor.y >= rect.top && cursor.y < rect.bottom;
        }
    }

    // Đưa Control Panel lên trước; chưa chạy thì bật nó
    public void OpenControlPanel()
    {
        IntPtr panel = FindWindow(ControlPanelClass, ControlPanelTitle);
        if (panel == IntPtr.Zero)
        {
            LaunchControlPanel();
            return;
        }
        ShowWindow(panel, SW_RESTORE);
        SetForegroundWindow(panel);
    }

    // Chỉ bật Control Panel nếu chưa chạy, không cướp focus nếu nó đã mở sẵn
    public void EnsureControlPanelRunning()
    {
        if (FindWindow(ControlPanelClass, ControlPanelTitle) == IntPtr.Zero) LaunchControlPanel();
    }

    static void LaunchControlPanel()
    {
        string aquariumDir = Path.GetDirectoryName(Application.dataPath);
        string exe = Path.GetFullPath(Path.Combine(aquariumDir, ControlPanelExePath));
        if (!File.Exists(exe))
        {
            Debug.LogWarning("PipWindow: không tìm thấy ControlPanel.exe: " + exe);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
            {
                WorkingDirectory = Path.GetDirectoryName(exe),
                UseShellExecute = false,
            });
        }
        catch (Exception e)
        {
            Debug.LogError("PipWindow: không bật được Control Panel: " + e.Message);
        }
    }
#else
    // Trong Editor hoặc nền tảng khác: giữ các hàm để nút bấm không báo lỗi, nhưng không làm gì
    public void Show() { IsVisible = true; Debug.Log("PipWindow.Show (chỉ có tác dụng trong bản build Windows)"); }
    public void Hide() { IsVisible = false; Debug.Log("PipWindow.Hide (chỉ có tác dụng trong bản build Windows)"); }
    public void Quit() { Debug.Log("PipWindow.Quit (chỉ có tác dụng trong bản build Windows)"); }
    public void OpenControlPanel() { Debug.Log("PipWindow.OpenControlPanel (chỉ có tác dụng trong bản build Windows)"); }
    public void EnsureControlPanelRunning() { }

    // Trong Editor: cho phép xem thử thanh nút ở Game view bằng cách rê chuột vào
    public bool IsCursorOver
    {
        get
        {
            Vector3 mouse = Input.mousePosition;
            return mouse.x >= 0 && mouse.x < Screen.width && mouse.y >= 0 && mouse.y < Screen.height;
        }
    }
#endif
}
