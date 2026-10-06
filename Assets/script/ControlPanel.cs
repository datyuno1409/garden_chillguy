using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

// App riêng (scene ControlPanel): bật, tắt, ẩn, hiện cửa sổ bể cá. Cửa sổ bể cá tự nó không thao tác được.
public class ControlPanel : MonoBehaviour
{
    const int StatusUnknown = 0;
    const int StatusOff = 1;
    const int StatusVisible = 2;
    const int StatusHidden = 3;

    const int PollTimeoutMs = 300;
    const int ButtonHeight = 44;

    // Đường dẫn tới Aquarium.exe, tính từ thư mục chứa ControlPanel.exe
    [SerializeField] string aquariumExePath = "../Aquarium/Aquarium.exe";
    [SerializeField] int pollIntervalMs = 1000;

    volatile int status = StatusUnknown;   // được luồng hỏi trạng thái cập nhật
    volatile bool polling;
    Thread poller;
    string message = "";

    void Awake()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 30;
    }

    void OnEnable()
    {
        polling = true;
        poller = new Thread(PollStatus) { IsBackground = true, Name = "PipStatusPoller" };
        poller.Start();
    }

    void OnDisable() { polling = false; }

    // Hỏi bể cá mỗi giây xem còn chạy không và đang ẩn hay hiện
    void PollStatus()
    {
        var target = new IPEndPoint(IPAddress.Loopback, PipProtocol.Port);
        byte[] ping = Encoding.UTF8.GetBytes(PipProtocol.Ping);

        using (var client = new UdpClient())
        {
            client.Client.ReceiveTimeout = PollTimeoutMs;
            while (polling)
            {
                status = AskStatus(client, target, ping);
                Thread.Sleep(pollIntervalMs);
            }
        }
    }

    static int AskStatus(UdpClient client, IPEndPoint target, byte[] ping)
    {
        try
        {
            client.Send(ping, ping.Length, target);
            var from = new IPEndPoint(IPAddress.Any, 0);
            string reply = Encoding.UTF8.GetString(client.Receive(ref from));
            return reply == PipProtocol.PongVisible ? StatusVisible
                 : reply == PipProtocol.PongHidden ? StatusHidden
                 : StatusOff;
        }
        catch (SocketException)
        {
            return StatusOff;   // không ai trả lời nghĩa là bể cá chưa chạy
        }
    }

    static void SendCommand(string command)
    {
        byte[] data = Encoding.UTF8.GetBytes(command);
        using (var client = new UdpClient())
            client.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, PipProtocol.Port));
    }

    void LaunchAquarium()
    {
        string panelDir = Path.GetDirectoryName(Application.dataPath);
        string exe = Path.GetFullPath(Path.Combine(panelDir, aquariumExePath));
        if (!File.Exists(exe))
        {
            message = "Không tìm thấy Aquarium.exe:\n" + exe;
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                WorkingDirectory = Path.GetDirectoryName(exe),
                UseShellExecute = false,
            });
            message = "";
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            message = "Không bật được bể cá: " + e.Message;
        }
    }

    void OnGUI()
    {
        GUI.skin.label.fontSize = 16;
        GUI.skin.button.fontSize = 16;
        GUI.skin.label.wordWrap = true;

        GUILayout.BeginArea(new Rect(16, 16, Screen.width - 32, Screen.height - 32));
        GUILayout.Label("Bảng điều khiển bể cá");
        GUILayout.Label("Trạng thái: " + StatusText(status));
        GUILayout.Space(8);

        int current = status;
        bool running = current == StatusVisible || current == StatusHidden;
        DrawButton("Bật bể cá", current == StatusOff, LaunchAquarium);
        DrawButton("Hiện", current == StatusHidden, () => SendCommand(PipProtocol.Show));
        DrawButton("Ẩn", current == StatusVisible, () => SendCommand(PipProtocol.Hide));
        DrawButton("Tắt bể cá", running, () => SendCommand(PipProtocol.Quit));

        if (message.Length > 0) GUILayout.Label(message);
        GUILayout.EndArea();
    }

    static void DrawButton(string label, bool enabled, Action onClick)
    {
        bool wasEnabled = GUI.enabled;
        GUI.enabled = enabled;
        if (GUILayout.Button(label, GUILayout.Height(ButtonHeight))) onClick();
        GUI.enabled = wasEnabled;
    }

    static string StatusText(int value)
    {
        switch (value)
        {
            case StatusOff: return "Chưa chạy";
            case StatusVisible: return "Đang hiện";
            case StatusHidden: return "Đang ẩn";
            default: return "Đang kiểm tra...";
        }
    }
}
