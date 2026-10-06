using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

// Dữ liệu mesh tích luỹ được: cho phép gộp nhiều khối vào một mesh duy nhất (sỏi, hoa) để chỉ tốn một draw call.
public sealed class MeshData
{
    public readonly List<Vector3> vertices = new List<Vector3>();
    public readonly List<Vector3> normals = new List<Vector3>();
    public readonly List<Color32> colors = new List<Color32>();
    public readonly List<int> triangles = new List<int>();

    // Màu đỉnh chỉ chứa được 0..1 nhưng ta cần hệ số sáng hơn 1 (ví dụ 1.2). Nên lưu ở nửa giá trị và shader nhân đôi lại.
    // sway (0..1) là mức lay theo gió, đi kèm trong alpha.
    public static Color32 EncodeTint(Color tint, float sway)
    {
        return new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f, sway);
    }

    public void ApplyTo(Mesh mesh)
    {
        mesh.Clear();
        mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }
}

// Hình dạng một khối: hình cầu được làm méo bằng noise. Đá dùng nhiều nếp gấp + phẳng mặt, bụi cây dùng mịn.
[Serializable]
public struct BlobShape
{
    public int seed;
    [Range(0, 4)] public int subdivisions;
    public float noiseAmplitude;
    public float noiseFrequency;
    [Range(0f, 0.9f)] public float flattenBottom;   // cắt phẳng đáy để khối ngồi vững trên mặt đất
    public bool flatShaded;                          // true: từng mặt phẳng (đá), false: mịn (bụi cây)
}

public static class BlobMeshBuilder
{
    // Trả về màu đỉnh từ hướng trên hình cầu đơn vị (y = cao thấp) và giá trị noise tại đó
    public delegate Color32 VertexColorFunc(Vector3 direction, float noiseValue);

    public static void AppendBlob(MeshData data, BlobShape shape, Matrix4x4 matrix, VertexColorFunc colorFunc)
    {
        IcoSphere.Get(shape.subdivisions, out Vector3[] baseVertices, out int[] baseTriangles);

        var seedOffset = new float3(shape.seed * 17.31f, shape.seed * 5.77f, shape.seed * 11.13f);
        float floorY = -1f + shape.flattenBottom;

        var positions = new Vector3[baseVertices.Length];
        var noiseValues = new float[baseVertices.Length];
        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 direction = baseVertices[i];
            float3 q = (float3)(direction * shape.noiseFrequency) + seedOffset;
            float noiseValue = noise.snoise(q) + 0.5f * noise.snoise(q * 2.13f);

            Vector3 p = direction * (1f + shape.noiseAmplitude * noiseValue);
            if (shape.flattenBottom > 0f && p.y < floorY) p.y = floorY;

            positions[i] = matrix.MultiplyPoint3x4(p);
            noiseValues[i] = noiseValue;
        }

        if (shape.flatShaded) AppendFlat(data, baseVertices, baseTriangles, positions, noiseValues, colorFunc);
        else AppendSmooth(data, baseVertices, baseTriangles, positions, noiseValues, colorFunc);
    }

    // Mỗi tam giác có đỉnh riêng và một pháp tuyến riêng: tạo cảm giác đá cạnh góc, màu lấy theo từng mặt
    static void AppendFlat(MeshData data, Vector3[] directions, int[] triangles, Vector3[] positions,
        float[] noiseValues, VertexColorFunc colorFunc)
    {
        for (int t = 0; t < triangles.Length; t += 3)
        {
            int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
            Vector3 normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).normalized;
            Vector3 averageDirection = ((directions[a] + directions[b] + directions[c]) / 3f).normalized;
            float averageNoise = (noiseValues[a] + noiseValues[b] + noiseValues[c]) / 3f;
            Color32 color = colorFunc(averageDirection, averageNoise);

            int start = data.vertices.Count;
            data.vertices.Add(positions[a]);
            data.vertices.Add(positions[b]);
            data.vertices.Add(positions[c]);
            for (int k = 0; k < 3; k++)
            {
                data.normals.Add(normal);
                data.colors.Add(color);
                data.triangles.Add(start + k);
            }
        }
    }

    static void AppendSmooth(MeshData data, Vector3[] directions, int[] triangles, Vector3[] positions,
        float[] noiseValues, VertexColorFunc colorFunc)
    {
        int start = data.vertices.Count;
        var normals = new Vector3[positions.Length];

        for (int t = 0; t < triangles.Length; t += 3)
        {
            int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
            Vector3 faceNormal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            normals[a] += faceNormal;
            normals[b] += faceNormal;
            normals[c] += faceNormal;
            data.triangles.Add(start + a);
            data.triangles.Add(start + b);
            data.triangles.Add(start + c);
        }

        for (int i = 0; i < positions.Length; i++)
        {
            data.vertices.Add(positions[i]);
            data.normals.Add(normals[i].normalized);
            data.colors.Add(colorFunc(directions[i], noiseValues[i]));
        }
    }
}

// Hình cầu từ khối 20 mặt (icosahedron), chia nhỏ dần. Kết quả được nhớ lại để không tính lại.
static class IcoSphere
{
    struct Cached { public Vector3[] vertices; public int[] triangles; }
    static readonly Dictionary<int, Cached> cache = new Dictionary<int, Cached>();

    public static void Get(int subdivisions, out Vector3[] vertices, out int[] triangles)
    {
        if (!cache.TryGetValue(subdivisions, out Cached cached))
        {
            cached = Build(subdivisions);
            cache[subdivisions] = cached;
        }
        vertices = cached.vertices;
        triangles = cached.triangles;
    }

    static Cached Build(int subdivisions)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var vertices = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;

        var triangles = new List<int>
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        for (int level = 0; level < subdivisions; level++)
        {
            var midpoints = new Dictionary<long, int>();
            var next = new List<int>(triangles.Count * 4);
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                int ab = Midpoint(vertices, midpoints, a, b);
                int bc = Midpoint(vertices, midpoints, b, c);
                int ca = Midpoint(vertices, midpoints, c, a);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            triangles = next;
        }

        return new Cached { vertices = vertices.ToArray(), triangles = triangles.ToArray() };
    }

    static int Midpoint(List<Vector3> vertices, Dictionary<long, int> midpoints, int a, int b)
    {
        long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
        if (midpoints.TryGetValue(key, out int index)) return index;

        vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
        index = vertices.Count - 1;
        midpoints[key] = index;
        return index;
    }
}
