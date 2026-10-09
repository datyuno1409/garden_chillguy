using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden.Core
{
    // Đưa cài đặt cho mọi thành phần cần nó. Một thành phần lỗi không làm hỏng các thành phần còn lại.
    public static class ConfigurableApplier
    {
        public static void Apply(IEnumerable<IGardenConfigurable> targets, GardenSettings settings)
        {
            foreach (IGardenConfigurable target in targets)
            {
                try
                {
                    target.ApplySettings(settings);
                }
                catch (Exception e)
                {
                    Debug.LogError($"ConfigurableApplier: {target.GetType().Name} bị lỗi khi nhận cài đặt: {e}");
                }
            }
        }
    }
}
