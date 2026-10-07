using UnityEngine;

// Điều khiển độ phủ rêu (0..1) của mọi renderer con dùng shader Garden/RockMoss.
// Giá trị này sau này sẽ được nối với số ngày trôi qua; hiện chỉnh tay ở Inspector để xem rêu mọc.
[ExecuteAlways]
public class MossGrowth : MonoBehaviour
{
    static readonly int CoverageId = Shader.PropertyToID("_MossCoverage");

    [SerializeField, Range(0f, 1f)] float coverage = 0.6f;

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
            target.SetPropertyBlock(block);
        }
    }
}
