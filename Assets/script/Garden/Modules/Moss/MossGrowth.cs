using UnityEngine;

namespace Garden.Moss
{
    // Phần "hiển thị" của rêu: đẩy độ phủ (0..1) và hạt giống loang vào mọi renderer con dùng shader Garden/RockMoss.
    // Độ phủ do MossModule đặt theo số ngày; ở Editor có thể kéo thanh trượt để xem thử.
    [ExecuteAlways]
    public class MossGrowth : MonoBehaviour
    {
        static readonly int CoverageId = Shader.PropertyToID("_MossCoverage");
        static readonly int SeedId = Shader.PropertyToID("_MossSeed");

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
