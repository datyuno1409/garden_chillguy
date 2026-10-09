using UnityEngine;

namespace Garden.Layout
{
    // Đánh dấu một vật (hoặc một nhóm vật) là vị trí tuỳ chỉnh được. id phải có trong LayoutCatalog.
    // (Phải nằm trong file riêng trùng tên class: Unity chỉ lưu được component vào scene khi tên file khớp tên class.)
    public sealed class LayoutSlot : MonoBehaviour
    {
        [SerializeField] string slotId;
        [SerializeField] int defaultSeed;

        public string Id => slotId;
        public int DefaultSeed => defaultSeed;

        // Hạt giống đã áp dụng gần nhất, để không dựng lại hình khi cài đặt khác (ví dụ mưa) đổi
        public int AppliedSeed { get; set; }

        public void Configure(string id, int seed)
        {
            slotId = id;
            defaultSeed = seed;
            AppliedSeed = seed;
        }
    }
}
