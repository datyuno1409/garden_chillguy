using System;
using UnityEngine;

[Serializable]
public struct BlobSettings
{
    public BlobShape shape;
    public Color topTint;      // nhân vào màu gốc ở phần trên của khối (sáng hơn)
    public Color bottomTint;   // nhân vào màu gốc ở phần dưới (tối hơn)
    [Range(0f, 0.3f)] public float colorNoise;   // độ lốm đốm màu
    [Range(0f, 1f)] public float sway;           // mức lay theo gió (alpha màu đỉnh)

    public static BlobSettings Rock(int seed) => new BlobSettings
    {
        shape = new BlobShape { seed = seed, subdivisions = 2, noiseAmplitude = 0.22f, noiseFrequency = 1.3f, flattenBottom = 0.35f, flatShaded = true },
        topTint = new Color(1.12f, 1.1f, 1.02f),
        bottomTint = new Color(0.72f, 0.72f, 0.78f),
        colorNoise = 0.12f,
        sway = 0f,
    };

    public static BlobSettings Shrub(int seed) => new BlobSettings
    {
        shape = new BlobShape { seed = seed, subdivisions = 3, noiseAmplitude = 0.2f, noiseFrequency = 1.7f, flattenBottom = 0.4f, flatShaded = false },
        topTint = new Color(1.2f, 1.15f, 0.85f),
        bottomTint = new Color(0.65f, 0.75f, 0.7f),
        colorNoise = 0.1f,
        sway = 0.05f,
    };
}

// Một khối đá / bụi cây / mô đất sinh bằng code từ "seed". Kích thước lấy từ scale của GameObject.
// Mesh không lưu vào scene mà dựng lại mỗi lần chạy, nên scene chỉ chứa seed và vài thông số.
[ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralBlob : MonoBehaviour
{
    [SerializeField] BlobSettings settings = BlobSettings.Rock(1);

    // Mesh thay thế: gán mô hình đã sửa trong Maya vào đây thì dùng nó thay cho mesh sinh bằng code
    [SerializeField] Mesh overrideMesh;

    [SerializeField] bool meshCollider;

    Mesh mesh;

    public BlobSettings Settings => settings;

    public bool HasOverrideMesh => overrideMesh != null;

    // Mesh đang sinh bằng code (null nếu đang dùng mesh thay thế)
    public Mesh GeneratedMesh => overrideMesh == null ? mesh : null;

    public void Apply(BlobSettings newSettings)
    {
        settings = newSettings;
        Rebuild();
    }

    void OnEnable() { Rebuild(); }
    void OnValidate() { if (isActiveAndEnabled) Rebuild(); }
    void OnDisable() { DestroyMesh(); }

    // Bật vùng va chạm theo đúng hình khối để click chuột (tia từ camera) trúng được tảng đá
    public void SetCollider(bool enabled)
    {
        meshCollider = enabled;
        Rebuild();
    }

    void Rebuild()
    {
        var filter = GetComponent<MeshFilter>();

        if (overrideMesh != null)
        {
            DestroyMesh();
            filter.sharedMesh = overrideMesh;
        }
        else
        {
            if (mesh == null) mesh = new Mesh { name = "ProceduralBlob", hideFlags = HideFlags.HideAndDontSave };
            filter.sharedMesh = mesh;

            var data = new MeshData();
            BlobMeshBuilder.AppendBlob(data, settings.shape, Matrix4x4.identity, ColorAt);
            data.ApplyTo(mesh);
        }

        UpdateCollider(filter.sharedMesh);
    }

    void UpdateCollider(Mesh shape)
    {
        bool hasCollider = TryGetComponent(out MeshCollider collider);

        if (!meshCollider)
        {
            if (hasCollider) DestroyUnityObject(collider);
            return;
        }

        if (!hasCollider) collider = gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = null;   // gán lại để Unity dựng lại vùng va chạm theo mesh mới
        collider.sharedMesh = shape;
    }

    static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    Color32 ColorAt(Vector3 direction, float noiseValue)
    {
        float height01 = direction.y * 0.5f + 0.5f;
        Color tint = Color.Lerp(settings.bottomTint, settings.topTint, height01);
        tint *= 1f + settings.colorNoise * noiseValue;
        return MeshData.EncodeTint(tint, settings.sway);
    }

    void DestroyMesh()
    {
        if (mesh == null) return;
        if (TryGetComponent(out MeshCollider collider)) collider.sharedMesh = null;   // đừng để vùng va chạm trỏ vào mesh đã huỷ
        DestroyUnityObject(mesh);
        mesh = null;
    }
}
