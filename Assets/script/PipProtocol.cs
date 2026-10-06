// Quy ước lệnh giữa Control Panel và cửa sổ bể cá. Hai app nói chuyện qua UDP trên chính máy này (127.0.0.1).
public static class PipProtocol
{
    public const int Port = 47321;

    public const string Show = "show";
    public const string Hide = "hide";
    public const string Quit = "quit";
    public const string Ping = "ping";

    public const string PongVisible = "pong visible";
    public const string PongHidden = "pong hidden";

    public static bool IsCommand(string text) =>
        text == Show || text == Hide || text == Quit;
}
