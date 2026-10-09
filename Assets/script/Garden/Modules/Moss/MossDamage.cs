using System;
using System.Collections.Generic;
using UnityEngine;

namespace Garden.Moss
{
    // Một vết rêu bị bóc: vị trí thế giới, bán kính (hiện tại và tối đa), và mức hỏng
    // (1 = vừa bị phá, giảm dần về 0 khi rêu mọc lại).
    [Serializable]
    public struct MossHit
    {
        public float x;
        public float y;
        public float z;
        public float radius;
        public float maxRadius;   // click lặp thì vết rộng ra, nhưng không quá mức này (lưu cùng vết để không rộng mãi qua các lần mở app)
        public float strength;

        public Vector3 Position => new Vector3(x, y, z);

        public MossHit(Vector3 position, float radius, float maxRadius, float strength)
        {
            x = position.x;
            y = position.y;
            z = position.z;
            this.radius = radius;
            this.maxRadius = maxRadius;
            this.strength = strength;
        }
    }

    // Logic thuần của việc phá rêu: các vết bị bóc, vết mới gộp vào vết cũ ở gần, và rêu tự mọc lại theo thời gian.
    // Số vết tối đa bằng Capacity (shader nhận một mảng cỡ này); đầy thì vết yếu nhất bị thay.
    public sealed class MossDamage
    {
        public const int Capacity = 16;   // phải khớp MOSS_MAX_HITS trong shader GhibliRockMoss

        const float MergeDistanceFraction = 0.35f;   // click gần vết cũ trong khoảng này (so với bán kính) thì gộp
        const float MergeGrowth = 1.2f;              // mỗi lần click lặp, vết rộng thêm chừng này
        const float MaxRadiusFactor = 2f;            // nhưng không rộng quá bấy nhiêu lần bán kính ban đầu

        readonly List<MossHit> hits = new List<MossHit>();

        public IReadOnlyList<MossHit> Hits => hits;

        public void AddHit(Vector3 point, float radius)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius)) return;

            for (int i = 0; i < hits.Count; i++)
            {
                MossHit existing = hits[i];
                if (Vector3.Distance(existing.Position, point) > existing.radius * MergeDistanceFraction) continue;

                float grown = Mathf.Min(existing.radius * MergeGrowth, existing.maxRadius);
                hits[i] = new MossHit(existing.Position, grown, existing.maxRadius, 1f);   // click lại: rộng ra và hỏng trở lại như mới
                return;
            }

            if (hits.Count >= Capacity) hits.RemoveAt(IndexOfWeakest());
            hits.Add(new MossHit(point, radius, radius * MaxRadiusFactor, 1f));
        }

        // Rêu mọc lại: mức hỏng giảm đều, hết hỏng thì vết biến mất. healDays <= 0 nghĩa là lành ngay.
        public void Tick(double elapsedDays, float healDays)
        {
            if (double.IsNaN(elapsedDays) || elapsedDays <= 0 || hits.Count == 0) return;

            float heal = healDays > 0f ? (float)(elapsedDays / healDays) : float.PositiveInfinity;
            for (int i = hits.Count - 1; i >= 0; i--)
            {
                MossHit hit = hits[i];
                float strength = hit.strength - heal;
                if (strength <= 0f) hits.RemoveAt(i);
                else hits[i] = new MossHit(hit.Position, hit.radius, hit.maxRadius, strength);
            }
        }

        public MossHit[] ToArray() => hits.ToArray();

        // Nạp từ file lưu: không tin dữ liệu đọc từ ngoài vào (bỏ vết hỏng, kẹp giá trị, cắt bớt nếu quá nhiều)
        public void Load(MossHit[] saved)
        {
            hits.Clear();
            if (saved == null) return;

            foreach (MossHit hit in saved)
            {
                if (hits.Count >= Capacity) break;
                if (!IsFinite(hit.x) || !IsFinite(hit.y) || !IsFinite(hit.z) || !IsFinite(hit.radius) || !IsFinite(hit.strength)) continue;
                if (hit.radius <= 0f || hit.strength <= 0f) continue;

                float maxRadius = IsFinite(hit.maxRadius) && hit.maxRadius >= hit.radius ? hit.maxRadius : hit.radius * MaxRadiusFactor;
                hits.Add(new MossHit(hit.Position, hit.radius, maxRadius, Mathf.Min(hit.strength, 1f)));
            }
        }

        int IndexOfWeakest()
        {
            int weakest = 0;
            for (int i = 1; i < hits.Count; i++)
            {
                if (hits[i].strength < hits[weakest].strength) weakest = i;
            }
            return weakest;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
