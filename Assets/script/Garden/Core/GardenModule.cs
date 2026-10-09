using UnityEngine;

namespace Garden.Core
{
    // Thông tin chung cho mọi mô-đun khi vườn khởi động
    public sealed class GardenContext
    {
        public int Seed { get; }
        public GardenEventBus Events { get; }

        public GardenContext(int seed, GardenEventBus events)
        {
            Seed = seed;
            Events = events;
        }
    }

    // Một lần click của người chơi trúng vào vườn (đã loại các vùng của cửa sổ như thanh tiêu đề, mép)
    public readonly struct GardenClick
    {
        public readonly Vector3 point;
        public readonly Vector3 normal;
        public readonly Collider collider;

        public GardenClick(Vector3 point, Vector3 normal, Collider collider)
        {
            this.point = point;
            this.normal = normal;
            this.collider = collider;
        }
    }

    // Khuôn của một mô-đun sinh vật / cơ chế (rêu, cây leo, con vật, mưa...).
    // Mỗi mô-đun nằm trong thư mục và assembly riêng, không gọi thẳng mô-đun khác (nói chuyện qua GardenEventBus).
    // Dữ liệu của mô-đun nằm trong GardenState dưới một phần mang tên Id.
    public interface IGardenModule
    {
        string Id { get; }

        // Đọc phần dữ liệu của mình từ trạng thái đã lưu (không có thì dùng mặc định)
        void Load(GardenState state, GardenContext context);

        // Ghi phần dữ liệu của mình vào trạng thái, trả về trạng thái mới
        GardenState Save(GardenState state);

        // Thời gian thật đã trôi qua, cả lúc đang chạy lẫn lúc đã tắt app
        void Tick(TimeWindow window);
    }

    // Mô-đun có thể xem thử ở một độ tuổi bất kỳ (không ghi vào file lưu), để kiểm tra bằng mắt không phải đợi nhiều ngày
    public interface IAgePreviewable
    {
        void PreviewAge(double ageDays);
    }

    // Mô-đun phản ứng khi người chơi click vào vườn
    public interface IGardenClickHandler
    {
        void OnGardenClick(GardenClick click);
    }

    // Bảng điều khiển của một mô-đun trong bảng thiết kế của Control Panel.
    // Mỗi mô-đun mang theo bảng của riêng mình, nên thêm mô-đun mới thì bảng thiết kế tự có thêm một mục.
    // Draw vẽ các ô chọn bằng IMGUI và trả về cài đặt mới (không sửa bản đưa vào).
    public interface IGardenDesignerPanel
    {
        string Title { get; }
        GardenSettings Draw(GardenSettings settings, System.DateTime nowUtc);
    }

    // Thành phần nhận cài đặt do người chơi chọn ở bảng thiết kế (Control Panel ghi file, cửa sổ vườn đọc và áp dụng ngay).
    // Mỗi thành phần chỉ đọc phần cài đặt của mình trong GardenSettings.
    public interface IGardenConfigurable
    {
        void ApplySettings(GardenSettings settings);
    }
}
