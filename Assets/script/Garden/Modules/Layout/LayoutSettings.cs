using System;

namespace Garden.Layout
{
    // Lựa chọn của người chơi cho một vị trí. seed = 0 nghĩa là dùng hình dạng mặc định.
    [Serializable]
    public struct SlotSetting
    {
        public string id;
        public int seed;
        public bool hidden;
    }

    // Phần cài đặt của mô-đun bố cục (mục "layout" trong file cài đặt)
    [Serializable]
    public struct LayoutSettings
    {
        public SlotSetting[] slots;

        // Lựa chọn cho một vị trí; chưa chọn gì thì trả về mặc định (hình gốc, hiện)
        public SlotSetting Get(string id)
        {
            if (slots != null)
            {
                foreach (SlotSetting slot in slots)
                {
                    if (slot.id == id) return slot;
                }
            }
            return new SlotSetting { id = id };
        }

        // Trả về bản mới (không sửa bản cũ): thay lựa chọn cùng id nếu có, không thì thêm vào
        public LayoutSettings With(SlotSetting setting)
        {
            var list = new System.Collections.Generic.List<SlotSetting>(slots ?? Array.Empty<SlotSetting>());
            int index = list.FindIndex(s => s.id == setting.id);
            if (index >= 0) list[index] = setting;
            else list.Add(setting);
            return new LayoutSettings { slots = list.ToArray() };
        }
    }
}
