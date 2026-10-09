// Tên sản phẩm của hai app. PipBuild đặt đúng tên này khi build, và Unity dùng nó làm tên cửa sổ,
// nên các chỗ khác (tìm cửa sổ ControlPanel, chặn chạy hai bản) dùng chung hằng số thay vì chép chuỗi.
public static class AppNames
{
    public const string Aquarium = "Aquarium";
    public const string ControlPanel = "ControlPanel";
}
