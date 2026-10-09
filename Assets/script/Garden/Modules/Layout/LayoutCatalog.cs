using System.Collections.Generic;

namespace Garden.Layout
{
    // Một vị trí trong khu vườn mà người chơi được tuỳ chỉnh
    public readonly struct LayoutSlotInfo
    {
        public readonly string id;
        public readonly string label;
        public readonly bool canReshuffle;   // đổi hình dạng (ví dụ đổi phiến đá)
        public readonly bool canHide;        // ẩn đi

        public LayoutSlotInfo(string id, string label, bool canReshuffle, bool canHide)
        {
            this.id = id;
            this.label = label;
            this.canReshuffle = canReshuffle;
            this.canHide = canHide;
        }
    }

    // Danh sách các vị trí tuỳ chỉnh được. MỘT nguồn sự thật cho cả công cụ dựng cảnh (gắn id vào vật) và bảng thiết kế
    // (liệt kê ô chọn), nên không thể lệch nhau. Thêm vật tuỳ chỉnh mới: thêm một dòng ở đây và gắn LayoutSlot với id đó.
    public static class LayoutCatalog
    {
        public const string MainRock = "rock-main";
        public const string Mushrooms = "mushrooms";
        public const string Grass = "grass";
        public const string Leaves = "leaves";

        public const int SmallRockCount = 4;

        public static string SmallRock(int index) => "rock-" + (index + 1);   // index từ 0

        public static IReadOnlyList<LayoutSlotInfo> Slots { get; } = BuildSlots();

        public static bool TryGet(string id, out LayoutSlotInfo info)
        {
            foreach (LayoutSlotInfo slot in Slots)
            {
                if (slot.id != id) continue;
                info = slot;
                return true;
            }
            info = default;
            return false;
        }

        static IReadOnlyList<LayoutSlotInfo> BuildSlots()
        {
            var slots = new List<LayoutSlotInfo> { new LayoutSlotInfo(MainRock, "Đá chính", canReshuffle: true, canHide: false) };
            for (int i = 0; i < SmallRockCount; i++)
                slots.Add(new LayoutSlotInfo(SmallRock(i), "Đá nhỏ " + (i + 1), canReshuffle: true, canHide: true));

            slots.Add(new LayoutSlotInfo(Mushrooms, "Nấm", canReshuffle: false, canHide: true));
            slots.Add(new LayoutSlotInfo(Grass, "Cỏ nhỏ", canReshuffle: false, canHide: true));
            slots.Add(new LayoutSlotInfo(Leaves, "Lá tạm ở rìa khung", canReshuffle: false, canHide: true));
            return slots;
        }
    }
}
