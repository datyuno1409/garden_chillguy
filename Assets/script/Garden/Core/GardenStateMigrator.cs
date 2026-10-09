using System;
using UnityEngine;

namespace Garden.Core
{
    public enum LoadOutcome
    {
        Missing,              // chưa có file lưu
        Loaded,               // đọc bình thường
        Migrated,             // file bản cũ, đã nâng cấp lên bản hiện tại
        Corrupt,              // file hỏng hoặc dữ liệu không hợp lệ
        NewerThanSupported,   // file do bản app mới hơn tạo ra, bản này không hiểu
    }

    // Đọc JSON của mọi phiên bản file lưu và đưa về GardenState hiện tại.
    // Khi đổi định dạng: tăng GardenState.CurrentVersion, thêm một nhánh nâng cấp ở đây và test cho nó.
    // Không bao giờ coi file bản cũ là "hỏng" (làm mất vườn của người chơi).
    public static class GardenStateMigrator
    {
        [Serializable] internal struct VersionProbe { public int version; }

        // Bản 1: chỉ có tuổi rêu, chưa chia phần theo mô-đun
        [Serializable]
        internal struct LegacyV1
        {
            public int version;
            public int seed;
            public long createdTicksUtc;
            public long lastSeenTicksUtc;
            public double mossAgeDays;
        }

        // Phải khớp với MossSection trong mô-đun Moss (tên field "ageDays"); có test kiểm tra hai bên khớp nhau
        [Serializable] internal struct LegacyMossSection { public double ageDays; }

        public const string MossSectionId = "moss";

        public static LoadOutcome TryParse(string json, out GardenState state)
        {
            state = default;

            VersionProbe probe;
            try
            {
                probe = JsonUtility.FromJson<VersionProbe>(json);
            }
            catch (ArgumentException)
            {
                return LoadOutcome.Corrupt;
            }

            if (probe.version == GardenState.CurrentVersion) return ParseCurrent(json, out state);
            if (probe.version == 1) return ParseLegacyV1(json, out state);
            return probe.version > GardenState.CurrentVersion ? LoadOutcome.NewerThanSupported : LoadOutcome.Corrupt;
        }

        static LoadOutcome ParseCurrent(string json, out GardenState state)
        {
            try
            {
                state = JsonUtility.FromJson<GardenState>(json);
            }
            catch (ArgumentException)
            {
                state = default;
                return LoadOutcome.Corrupt;
            }
            return state.IsValid() ? LoadOutcome.Loaded : LoadOutcome.Corrupt;
        }

        static LoadOutcome ParseLegacyV1(string json, out GardenState state)
        {
            state = default;
            LegacyV1 old;
            try
            {
                old = JsonUtility.FromJson<LegacyV1>(json);
            }
            catch (ArgumentException)
            {
                return LoadOutcome.Corrupt;
            }

            bool valid = GardenState.IsValidTicks(old.createdTicksUtc) && GardenState.IsValidTicks(old.lastSeenTicksUtc)
                && !double.IsNaN(old.mossAgeDays) && old.mossAgeDays >= 0 && old.mossAgeDays <= 1_000_000;
            if (!valid) return LoadOutcome.Corrupt;

            GardenState migrated = new GardenState
            {
                version = GardenState.CurrentVersion,
                seed = old.seed,
                createdTicksUtc = old.createdTicksUtc,
                lastSeenTicksUtc = old.lastSeenTicksUtc,
                sections = Array.Empty<GardenSection>(),
            }.WithSection(MossSectionId, new LegacyMossSection { ageDays = old.mossAgeDays });

            state = migrated;
            return LoadOutcome.Migrated;
        }
    }
}
