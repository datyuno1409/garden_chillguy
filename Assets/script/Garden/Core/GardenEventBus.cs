using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden.Core
{
    // Các mô-đun báo cho nhau bằng sự kiện thay vì gọi nhau trực tiếp. Ví dụ mưa báo "đang mưa", rêu nghe và mọc nhanh hơn.
    // Một người nghe bị lỗi không làm hỏng những người nghe khác.
    public sealed class GardenEventBus
    {
        readonly Dictionary<Type, List<Delegate>> handlers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (!handlers.TryGetValue(typeof(T), out List<Delegate> list))
            {
                list = new List<Delegate>();
                handlers[typeof(T)] = list;
            }
            list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handlers.TryGetValue(typeof(T), out List<Delegate> list)) list.Remove(handler);
        }

        public void Publish<T>(T gardenEvent)
        {
            if (!handlers.TryGetValue(typeof(T), out List<Delegate> list)) return;

            foreach (Delegate handler in list.ToArray())   // bản sao: người nghe có thể huỷ đăng ký ngay trong lúc xử lý
            {
                try
                {
                    ((Action<T>)handler)(gardenEvent);
                }
                catch (Exception e)
                {
                    Debug.LogError($"GardenEventBus: người nghe sự kiện {typeof(T).Name} bị lỗi: {e}");
                }
            }
        }
    }
}
