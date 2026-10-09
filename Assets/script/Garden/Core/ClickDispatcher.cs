using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden.Core
{
    // Chuyển một lần click vào vườn cho các mô-đun quan tâm. Mô-đun lỗi không chặn các mô-đun còn lại.
    public static class ClickDispatcher
    {
        // Trả về số mô-đun đã được báo (kể cả mô-đun bị lỗi giữa chừng)
        public static int Dispatch(IEnumerable<IGardenClickHandler> handlers, GardenClick click)
        {
            int notified = 0;
            foreach (IGardenClickHandler handler in handlers)
            {
                notified++;
                try
                {
                    handler.OnGardenClick(click);
                }
                catch (Exception e)
                {
                    Debug.LogError($"ClickDispatcher: {handler.GetType().Name} bị lỗi khi xử lý click: {e}");
                }
            }
            return notified;
        }
    }
}
