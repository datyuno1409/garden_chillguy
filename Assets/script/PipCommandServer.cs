using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// Gắn cùng chỗ với PipWindow trong scene bể cá. Lắng nghe lệnh từ Control Panel và chuyển cho PipWindow thực hiện.
// Chỉ nhận lệnh từ chính máy này (127.0.0.1) và chỉ chấp nhận các lệnh trong PipProtocol.
public class PipCommandServer : MonoBehaviour
{
    [SerializeField] PipWindow pip;

    readonly ConcurrentQueue<string> commands = new ConcurrentQueue<string>();
    UdpClient udp;
    Thread listener;
    volatile bool running;
    volatile bool isVisible = true;

    void Awake()
    {
        if (pip == null) pip = GetComponent<PipWindow>();
    }

    void OnEnable()
    {
        try
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, PipProtocol.Port));
        }
        catch (SocketException e)
        {
            // Thường do một bể cá khác đang chạy và đã giữ cổng này
            Debug.LogError($"PipCommandServer: không mở được cổng {PipProtocol.Port} ({e.Message})");
            enabled = false;
            return;
        }

        running = true;
        UdpClient client = udp;
        listener = new Thread(() => Listen(client)) { IsBackground = true, Name = "PipCommandServer" };
        listener.Start();
    }

    void OnDisable()
    {
        running = false;
        udp?.Close();   // đóng socket để luồng nghe thoát ra
        udp = null;
    }

    // Chạy ở luồng riêng: chỉ nhận gói tin, không đụng vào đối tượng Unity
    void Listen(UdpClient client)
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = client.Receive(ref remote);
                string text = Encoding.UTF8.GetString(data).Trim().ToLowerInvariant();

                if (text == PipProtocol.Ping)
                {
                    string pong = isVisible ? PipProtocol.PongVisible : PipProtocol.PongHidden;
                    byte[] reply = Encoding.UTF8.GetBytes(pong);
                    client.Send(reply, reply.Length, remote);
                }
                else if (PipProtocol.IsCommand(text))
                {
                    commands.Enqueue(text);
                }
            }
            catch (SocketException)
            {
                if (!running) break;   // socket bị đóng khi tắt app
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    // Lệnh được thực hiện ở luồng chính vì chỉ luồng chính mới gọi được API cửa sổ và Unity
    void Update()
    {
        isVisible = pip != null && pip.IsVisible;

        while (commands.TryDequeue(out string command))
        {
            if (pip == null) continue;
            switch (command)
            {
                case PipProtocol.Show: pip.Show(); break;
                case PipProtocol.Hide: pip.Hide(); break;
                case PipProtocol.Quit: pip.Quit(); break;
            }
        }
    }
}
