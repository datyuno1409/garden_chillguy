using System;
using UnityEngine;
using Random = System.Random;

public enum ScatterArea { Rect, Dome }

[Serializable]
public struct ScatterSettings
{
    public ScatterArea area;
    public Vector3 extent;        // Rect: x = rộng, z = sâu. Dome: x = bán kính
    public int count;
    public Vector2 sizeRange;     // kích thước mỗi khối nhỏ (min, max)
    public Vector3 stretch;       // nhân vào kích thước theo từng trục (dẹt, dài...)
    public int seed;
    [Range(0, 2)] public int subdivisions;
    public float noiseAmplitude;
    public bool flatShaded;
    public Color[] palette;       // màu sRGB, mỗi khối nhỏ chọn một màu ngẫu nhiên
    [Range(0f, 1f)] public float sway;

    public static ScatterSettings Pebbles(int seed, Vector2 area, int count) => new ScatterSettings
    {
        area = ScatterArea.Rect, extent = new Vector3(area.x, 0f, area.y), count = count,
        sizeRange = new Vector2(0.05f, 0.12f), stretch = new Vector3(1f, 0.6f, 0.9f),
        seed = seed, subdivisions = 0, noiseAmplitude = 0.25f, flatShaded = true, sway = 0f,
        palette = new[]
        {
            new Color(0.62f, 0.6f, 0.57f), new Color(0.76f, 0.73f, 0.67f), new Color(0.5f, 0.48f, 0.46f),
            new Color(0.84f, 0.8f, 0.72f), new Color(0.6f, 0.54f, 0.46f),
        },
    };

    public static ScatterSettings Blossoms(int seed, float radius, int count, Vector2 sizeRange, params Color[] palette) => new ScatterSettings
    {
        area = ScatterArea.Dome, extent = new Vector3(radius, 0f, 0f), count = count,
        sizeRange = sizeRange, stretch = Vector3.one,
        seed = seed, subdivisions = 1, noiseAmplitude = 0.15f, flatShaded = false, sway = 0.04f,
        palette = palette,
    };
}

// Rải nhiều khối nhỏ (sỏi, cụm hoa) vào MỘT mesh duy nhất. Dùng seed nên kết quả luôn giống nhau mỗi lần chạy.
[ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BlobScatter : MonoBehaviour
{
    [SerializeField] ScatterSettings settings = ScatterSettings.Pebbles(1, new Vector2(2f, 2f), 50);

    Mesh mesh;

    public void Apply(ScatterSettings newSettings)
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
            mesh = new Mesh { name = "BlobScatter", hideFlags = HideFlags.HideAndDontSave };
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        var data = new MeshData();
        var random = new Random(settings.seed);
        Color[] palette = settings.palette != null && settings.palette.Length > 0 ? settings.palette : new[] { Color.white };

        for (int i = 0; i < settings.count; i++)
        {
            float size = Mathf.Lerp(settings.sizeRange.x, settings.sizeRange.y, (float)random.NextDouble());
            Vector3 scale = Vector3.Scale(Vector3.one * size, settings.stretch);
            Vector3 position = RandomPosition(random, size);
            Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            Color baseColor = palette[random.Next(palette.Length)].linear;   // màu đỉnh không tự đổi sRGB nên đổi tay

            var shape = new BlobShape
            {
                seed = random.Next(1000), subdivisions = settings.subdivisions,
                noiseAmplitude = settings.noiseAmplitude, noiseFrequency = 1.5f,
                flattenBottom = 0f, flatShaded = settings.flatShaded,
            };
            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
            float sway = settings.sway;
            BlobMeshBuilder.AppendBlob(data, shape, matrix, (direction, noiseValue) =>
            {
                Color c = baseColor * (0.88f + 0.2f * (direction.y * 0.5f + 0.5f)) * (1f + 0.06f * noiseValue);
                return MeshData.EncodeTint(c, sway);
            });
        }

        data.ApplyTo(mesh);
    }

    Vector3 RandomPosition(Random random, float size)
    {
        if (settings.area == ScatterArea.Rect)
        {
            float x = ((float)random.NextDouble() - 0.5f) * settings.extent.x;
            float z = ((float)random.NextDouble() - 0.5f) * settings.extent.z;
            return new Vector3(x, size * 0.25f, z);   // nằm sát mặt đất, hơi lún
        }

        // Dome: điểm trên nửa mặt cầu trên, sát vỏ ngoài để hoa nằm trên bề mặt bụi cây
        Vector3 direction;
        do
        {
            direction = new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble(), (float)random.NextDouble() * 2f - 1f);
        } while (direction.sqrMagnitude > 1f || direction.sqrMagnitude < 0.04f || direction.y < 0.12f);

        return direction.normalized * settings.extent.x * (0.88f + 0.1f * (float)random.NextDouble());
    }

    void DestroyMesh()
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
        mesh = null;
    }
}
