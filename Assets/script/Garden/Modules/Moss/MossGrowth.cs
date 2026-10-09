using System.Collections.Generic;
using UnityEngine;

namespace Garden.Moss
{
    // Phần "hiển thị" của rêu: đẩy độ phủ (0..1) và hạt giống loang vào mọi renderer con dùng shader Garden/RockMoss,
    // và đẩy danh sách vết bị bóc vào biến toàn cục của shader (mọi vật liệu rêu dùng chung một danh sách).
    // Độ phủ do MossModule đặt theo số ngày; ở Editor có thể kéo thanh trượt để xem thử.
    [ExecuteAlways]
    public class MossGrowth : MonoBehaviour
    {
        static readonly int CoverageId = Shader.PropertyToID("_MossCoverage");
        static readonly int SeedId = Shader.PropertyToID("_MossSeed");
        static readonly int HitsId = Shader.PropertyToID("_MossHits");
        static readonly int HitStrengthsId = Shader.PropertyToID("_MossHitStrengths");
        static readonly int HitCountId = Shader.PropertyToID("_MossHitCount");

        // Các mảng này giữ nguyên kích thước (shader khai báo mảng cỡ cố định), chỉ ghi đè nội dung
        static readonly Vector4[] hitPositions = new Vector4[MossDamage.Capacity];
        static readonly float[] hitStrengths = new float[MossDamage.Capacity];

        [SerializeField, Range(0f, 1f)] float coverage = 0.6f;
        [SerializeField] float seed;

        MaterialPropertyBlock block;

        public float Coverage
        {
            get => coverage;
            set
            {
                coverage = Mathf.Clamp01(value);
                Apply();
            }
        }

        // Mỗi vườn một hạt giống: các mảng rêu loang theo kiểu khác nhau
        public float Seed
        {
            get => seed;
            set
            {
                seed = value;
                Apply();
            }
        }

        // Cập nhật danh sách vết bị bóc cho shader (xyz = vị trí thế giới, w = bán kính)
        public void SetHits(IReadOnlyList<MossHit> hits)
        {
            int count = Mathf.Min(hits.Count, MossDamage.Capacity);
            for (int i = 0; i < count; i++)
            {
                hitPositions[i] = new Vector4(hits[i].x, hits[i].y, hits[i].z, hits[i].radius);
                hitStrengths[i] = hits[i].strength;
            }

            Shader.SetGlobalVectorArray(HitsId, hitPositions);
            Shader.SetGlobalFloatArray(HitStrengthsId, hitStrengths);
            Shader.SetGlobalFloat(HitCountId, count);
        }

        void OnEnable() { Apply(); }
        void OnValidate() { if (isActiveAndEnabled) Apply(); }

        // Chỉ chạy khi giá trị đổi, không tốn gì mỗi khung hình
        void Apply()
        {
            block ??= new MaterialPropertyBlock();

            foreach (Renderer target in GetComponentsInChildren<Renderer>())
            {
                Material material = target.sharedMaterial;
                if (material == null || !material.HasProperty(CoverageId)) continue;

                target.GetPropertyBlock(block);
                block.SetFloat(CoverageId, coverage);
                block.SetFloat(SeedId, seed);
                target.SetPropertyBlock(block);
            }
        }
    }
}
