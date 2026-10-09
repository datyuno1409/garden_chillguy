using System;
using System.Collections.Generic;

namespace Garden.Rain
{
    // Cài đặt mưa do người chơi chọn ở bảng thiết kế
    [Serializable]
    public struct RainSettings
    {
        public bool enabled;                 // cho phép mưa tự nhiên
        public int frequency;                // 0 = ít, 1 = vừa, 2 = nhiều
        public long manualStartTicksUtc;     // mưa do người chơi gọi: từ lúc nào tới lúc nào
        public long manualUntilTicksUtc;

        public static RainSettings Default => new RainSettings { enabled = true, frequency = 1 };

        public bool HasManualRain => manualUntilTicksUtc > manualStartTicksUtc;

        public RainSettings WithManualRain(DateTime nowUtc, double minutes)
        {
            RainSettings copy = this;
            copy.manualStartTicksUtc = nowUtc.Ticks;
            copy.manualUntilTicksUtc = nowUtc.AddMinutes(minutes).Ticks;
            return copy;
        }

        public RainSettings WithoutManualRain()
        {
            RainSettings copy = this;
            copy.manualStartTicksUtc = 0;
            copy.manualUntilTicksUtc = 0;
            return copy;
        }
    }

    // Lịch mưa XÁC ĐỊNH theo hạt giống của vườn: cùng seed và cùng giờ thì luôn cho cùng kết quả.
    // Nhờ vậy không cần lưu lịch sử mưa: mở app sau nhiều ngày vẫn tính ra chính xác đã mưa lúc nào trong lúc đóng app.
    // Thời gian chia thành các ô 6 giờ; mỗi ô có thể có một trận mưa 20-90 phút ở vị trí ngẫu nhiên trong ô.
    // Tần suất cao hơn chỉ thêm các ô mưa (không làm mất ô mưa của tần suất thấp), nên đổi tần suất không xáo trộn lịch.
    public static class RainSchedule
    {
        public const int FrequencyCount = 3;

        static readonly double[] ChanceByFrequency = { 0.12, 0.25, 0.45 };
        const double CellMinutes = 360;
        const double MinDurationMinutes = 20;
        const double MaxDurationMinutes = 90;
        const long TicksPerMinute = TimeSpan.TicksPerMinute;
        const long CellTicks = TimeSpan.TicksPerHour * 6;
        static readonly long EpochTicks = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        public static double ChanceFor(int frequency)
        {
            int index = Math.Min(Math.Max(frequency, 0), FrequencyCount - 1);
            return ChanceByFrequency[index];
        }

        // Có mưa lúc này không (mưa tự nhiên hoặc mưa do người chơi gọi)
        public static bool IsRaining(int seed, DateTime nowUtc, RainSettings settings)
        {
            long now = nowUtc.Ticks;
            if (settings.HasManualRain && now >= settings.manualStartTicksUtc && now < settings.manualUntilTicksUtc) return true;
            if (!settings.enabled) return false;

            long cell = CellIndex(now);
            return TryGetNaturalRain(seed, cell, ChanceFor(settings.frequency), out long start, out long end) && now >= start && now < end;
        }

        // Tổng thời gian có mưa (tính bằng ngày) trong khoảng [from, to). Mưa tự nhiên và mưa do người chơi gọi
        // chồng lên nhau chỉ được tính một lần.
        public static double RainDays(int seed, DateTime fromUtc, DateTime toUtc, RainSettings settings)
        {
            long from = fromUtc.Ticks, to = toUtc.Ticks;
            if (to <= from) return 0;

            var intervals = new List<(long start, long end)>();

            if (settings.enabled)
            {
                double chance = ChanceFor(settings.frequency);
                long lastCell = CellIndex(to);
                for (long cell = CellIndex(from); cell <= lastCell; cell++)
                {
                    if (!TryGetNaturalRain(seed, cell, chance, out long start, out long end)) continue;
                    AddClipped(intervals, start, end, from, to);
                }
            }

            if (settings.HasManualRain) AddClipped(intervals, settings.manualStartTicksUtc, settings.manualUntilTicksUtc, from, to);

            return MergedLength(intervals) / (double)TimeSpan.TicksPerDay;
        }

        // Trận mưa tự nhiên của một ô thời gian (nếu có)
        public static bool TryGetNaturalRain(int seed, long cell, double chance, out long startTicks, out long endTicks)
        {
            startTicks = endTicks = 0;
            if (Unit(seed, cell, 1) >= chance) return false;

            double durationMinutes = MinDurationMinutes + Unit(seed, cell, 2) * (MaxDurationMinutes - MinDurationMinutes);
            double offsetMinutes = Unit(seed, cell, 3) * (CellMinutes - durationMinutes);

            long cellStart = EpochTicks + cell * CellTicks;
            startTicks = cellStart + (long)(offsetMinutes * TicksPerMinute);
            endTicks = startTicks + (long)(durationMinutes * TicksPerMinute);
            return true;
        }

        public static long CellIndex(long ticks) => FloorDiv(ticks - EpochTicks, CellTicks);

        static long FloorDiv(long a, long b)
        {
            long q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }

        static void AddClipped(List<(long start, long end)> intervals, long start, long end, long from, long to)
        {
            long clippedStart = Math.Max(start, from), clippedEnd = Math.Min(end, to);
            if (clippedEnd > clippedStart) intervals.Add((clippedStart, clippedEnd));
        }

        static long MergedLength(List<(long start, long end)> intervals)
        {
            intervals.Sort((a, b) => a.start.CompareTo(b.start));

            long total = 0, currentStart = 0, currentEnd = 0;
            bool open = false;
            foreach ((long start, long end) in intervals)
            {
                if (!open) { currentStart = start; currentEnd = end; open = true; }
                else if (start <= currentEnd) currentEnd = Math.Max(currentEnd, end);
                else { total += currentEnd - currentStart; currentStart = start; currentEnd = end; }
            }
            if (open) total += currentEnd - currentStart;
            return total;
        }

        // Số ngẫu nhiên xác định trong [0, 1) từ (seed, ô, muối). Tự cài hàm băm để kết quả giống nhau trên mọi nền tảng.
        static double Unit(int seed, long cell, uint salt)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= Mix((uint)cell ^ ((uint)(cell >> 32) * 0x85EBCA6Bu));
                h ^= salt * 0xC2B2AE35u;
                return Mix(h) / 4294967296.0;
            }
        }

        static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7feb352du;
                x ^= x >> 15;
                x *= 0x846ca68bu;
                x ^= x >> 16;
                return x;
            }
        }
    }
}
