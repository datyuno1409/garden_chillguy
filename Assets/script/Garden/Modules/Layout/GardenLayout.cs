using Garden.Core;
using UnityEngine;

namespace Garden.Layout
{
    // Áp dụng bố cục người chơi chọn ở bảng thiết kế lên các LayoutSlot nằm dưới object này: ẩn/hiện và đổi hình.
    // Không có lựa chọn thì giữ nguyên bố cục gốc. Chỉ làm những việc mà LayoutCatalog cho phép cho từng vị trí.
    public sealed class GardenLayout : MonoBehaviour, IGardenConfigurable
    {
        public const string ModuleId = "layout";

        public void ApplySettings(GardenSettings settings)
        {
            LayoutSettings layout = settings.GetOrDefault(ModuleId, default(LayoutSettings));

            foreach (LayoutSlot slot in GetComponentsInChildren<LayoutSlot>(true))
            {
                if (!LayoutCatalog.TryGet(slot.Id, out LayoutSlotInfo info)) continue;   // vị trí lạ: bỏ qua cho an toàn

                SlotSetting choice = layout.Get(slot.Id);
                slot.gameObject.SetActive(!(choice.hidden && info.canHide));

                int seed = choice.seed != 0 && info.canReshuffle ? choice.seed : slot.DefaultSeed;
                if (seed == slot.AppliedSeed) continue;

                slot.AppliedSeed = seed;
                ILayoutVariant variant = slot.GetComponent<ILayoutVariant>();
                variant?.SetVariantSeed(seed);
            }
        }
    }
}
