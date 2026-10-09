using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
#endif

// Mỗi app chỉ cho chạy MỘT bản. Hai cửa sổ vườn cùng chạy sẽ tranh một cổng lệnh (UDP), nên lệnh hiện/ẩn/tắt
// có thể đến nhầm bản. Bản chạy sau tự thoát; riêng cửa sổ vườn thì nhờ bản đang chạy hiện lên
// (người dùng bấm mở lại thường là vì đang không thấy cửa sổ).
public static class SingleInstance
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    static Mutex ownership;   // giữ suốt vòng đời tiến trình; Windows tự nhả khi tiến trình thoát

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Guard()
    {
        // Local\ = riêng từng phiên đăng nhập Windows
        ownership = new Mutex(true, @"Local\GardenChill." + Application.productName, out bool isFirst);
        if (isFirst) return;

        if (Application.productName == AppNames.Aquarium) AskRunningInstanceToShow();
        Debug.Log("SingleInstance: " + Application.productName + " đã chạy rồi, thoát bản này.");
        System.Diagnostics.Process.GetCurrentProcess().Kill();
    }

    static void AskRunningInstanceToShow()
    {
        try
        {
            byte[] show = Encoding.UTF8.GetBytes(PipProtocol.Show);
            using (var client = new UdpClient())
                client.Send(show, show.Length, "127.0.0.1", PipProtocol.Port);
        }
        catch (SocketException e)
        {
            Debug.LogWarning("SingleInstance: không gửi được lệnh hiện cửa sổ: " + e.Message);
        }
    }
#endif
}
