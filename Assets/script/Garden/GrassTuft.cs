using System;
using UnityEngine;
using Random = System.Random;

[Serializable]
public struct GrassSettings
{
    public int seed;
    public int bladeCount;
    public float height;
    public float spread;       // bán kính mảng cỏ
    public float bladeWidth;
    [Range(0f, 1f)] public float bend;   // độ cong của lưỡi cỏ

    public static GrassSettings Clump(int seed, float height, float spread, int bladeCount) => new GrassSettings
    {
        seed = seed, bladeCount = bladeCount, height = height, spread = spread, bladeWidth = 0.05f, bend = 0.55f,
    };
}

// Một bụi cỏ gồm nhiều lưỡi cỏ cong, gộp thành một mesh. Màu đỉnh: tối ở gốc, sáng ở ngọn; alpha = mức lay theo gió.
[ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GrassTuft : MonoBehaviour
{
    [SerializeField] GrassSettings settings = GrassSettings.Clump(1, 0.8f, 0.4f, 60);

    static readonly Color BaseColor = new Color(0.55f, 0.72f, 0.55f);
    static readonly Color MidColor = new Color(0.9f, 1.0f, 0.7f);
    static readonly Color TipColor = new Color(1.25f, 1.2f, 0.75f);

    Mesh mesh;

    public void Apply(GrassSettings newSettings)
    {
        settings = newSettings;
        Rebuild();
    }

    void OnEnable() { Rebuild(); }
    void OnValidate() { if (isActiveAndEnabled) Rebuild(); }
    void OnDisable() { DestroyMesh(); }

    void Rebuild()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "GrassTuft", hideFlags = HideFlags.HideAndDontSave };
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        var data = new MeshData();
        var random = new Random(settings.seed);
        for (int i = 0; i < settings.bladeCount; i++) AppendBlade(data, random);
        data.ApplyTo(mesh);
    }

    // Lưỡi cỏ: 5 đỉnh (2 ở gốc, 2 ở giữa, 1 ở ngọn), cong theo một hướng
    void AppendBlade(MeshData data, Random random)
    {
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float radius = Mathf.Sqrt((float)random.NextDouble()) * settings.spread;
        Vector3 root = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

        // Hướng cong: thiên về toả ra ngoài, lệch ngẫu nhiên một chút
        Vector3 outward = radius > 0.001f ? root.normalized : Vector3.right;
        float yaw = ((float)random.NextDouble() - 0.5f) * 1.6f;
        Vector3 lean = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f) * outward;
        Vector3 side = new Vector3(-lean.z, 0f, lean.x);

        float height = settings.height * (0.6f + 0.55f * (float)random.NextDouble());
        float curve = settings.bend * height * (0.4f + 0.8f * (float)random.NextDouble());
        float width = settings.bladeWidth * (0.8f + 0.5f * (float)random.NextDouble());
        float brightness = 0.9f + 0.2f * (float)random.NextDouble();

        Vector3 Spine(float t) => root + lean * (curve * t * t) + Vector3.up * (height * t);
        Vector3 normal = (Vector3.up + outward * 0.35f).normalized;

        int start = data.vertices.Count;
        AddVertex(data, Spine(0f) - side * width * 0.5f, normal, BaseColor, brightness, 0f);
        AddVertex(data, Spine(0f) + side * width * 0.5f, normal, BaseColor, brightness, 0f);
        AddVertex(data, Spine(0.5f) - side * width * 0.38f, normal, MidColor, brightness, 0.25f);
        AddVertex(data, Spine(0.5f) + side * width * 0.38f, normal, MidColor, brightness, 0.25f);
        AddVertex(data, Spine(1f), normal, TipColor, brightness, 1f);

        int[] indices = { 0, 2, 1, 1, 2, 3, 2, 4, 3 };
        foreach (int index in indices) data.triangles.Add(start + index);
    }

    static void AddVertex(MeshData data, Vector3 position, Vector3 normal, Color tint, float brightness, float sway)
    {
        data.vertices.Add(position);
        data.normals.Add(normal);
        data.colors.Add(MeshData.EncodeTint(tint * brightness, sway));
    }

    void DestroyMesh()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
        mesh = null;
    }
}
