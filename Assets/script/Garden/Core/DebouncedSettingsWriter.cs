using System;
using System.IO;
using UnityEngine;

namespace Garden.Core
{
    // Quyết định KHI NÀO ghi cài đặt người chơi vừa chỉnh ra file: gộp các lần chỉnh liên tiếp (kéo thanh trượt)
    // thành một lần ghi sau khi yên một lúc, không ghi khi chẳng có gì đổi, ghi ngay khi đóng (Flush), và thử lại
    // nếu ghi lỗi mà không ghi dồn dập. Thời gian và việc ghi file được đưa vào từ ngoài nên kiểm thử được.
    public sealed class DebouncedSettingsWriter
    {
        public const float DefaultDelaySeconds = 0.25f;
        public const float RetryDelaySeconds = 2f;

        readonly Action<GardenSettings> write;
        readonly float delay;

        GardenSettings pending;
        bool dirty;
        float writeAt;

        public bool HasPendingChange => dirty;

        public DebouncedSettingsWriter(Action<GardenSettings> write, float delaySeconds = DefaultDelaySeconds)
        {
            this.write = write ?? throw new ArgumentNullException(nameof(write));
            delay = delaySeconds;
        }

        // Người chơi vừa chỉnh: nhớ cài đặt mới nhất và hẹn ghi sau một lúc
        public void MarkChanged(GardenSettings settings, float now)
        {
            pending = settings;
            dirty = true;
            writeAt = now + delay;
        }

        // Gọi mỗi khung hình
        public void Update(float now)
        {
            if (dirty && now >= writeAt) TryWrite(now);
        }

        // Ghi ngay nếu còn thay đổi chưa ghi (khi đóng Control Panel)
        public void Flush(float now)
        {
            if (dirty) TryWrite(now);
        }

        void TryWrite(float now)
        {
            try
            {
                write(pending);
                dirty = false;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Giữ nguyên thay đổi chưa ghi và thử lại sau, thay vì ghi dồn dập mỗi khung hình
                Debug.LogWarning("DebouncedSettingsWriter: không ghi được cài đặt, sẽ thử lại: " + e.Message);
                writeAt = now + RetryDelaySeconds;
            }
        }
    }
}
