using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Tools/Garden/Export rocks to OBJ (Maya): xuất mỗi tảng đá sinh bằng code ra một file OBJ có UV để sửa trong Maya.
// File nằm ở thư mục Exports/Rocks cạnh thư mục Assets (ngoài Assets nên Unity không tự nhập lại chúng).
// Sửa xong xuất FBX từ Maya vào Assets/Garden/Models rồi kéo vào ô "Override Mesh" của tảng đá tương ứng.
public static class GardenExport
{
    const string RocksRootName = "Rocks";

    [MenuItem("Tools/Garden/Export rocks to OBJ (Maya)")]
    public static void ExportRocks()
    {
        GameObject root = GameObject.Find("Garden/" + RocksRootName);
        if (root == null)
        {
            Debug.LogWarning("GardenExport: không thấy Garden/Rocks, hãy chạy Tools/Garden/3. Build garden scene trước.");
            return;
        }

        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Exports/Rocks"));
        Directory.CreateDirectory(dir);

        int count = 0;
        foreach (ProceduralBlob blob in root.GetComponentsInChildren<ProceduralBlob>())
        {
            Mesh mesh = blob.GeneratedMesh;
            if (mesh == null) continue;   // tảng này đã dùng mesh từ Maya, không cần xuất

            string path = Path.Combine(dir, blob.name + ".obj");
            File.WriteAllText(path, BuildObj(blob.name, mesh, blob.transform.lossyScale), new UTF8Encoding(false));
            count++;
        }

        Debug.Log($"GardenExport: đã xuất {count} tảng đá ra {dir}");
        EditorUtility.RevealInFinder(dir);
    }

    // Kích thước thật (đã nhân scale), nằm ở gốc toạ độ. Unity dùng hệ toạ độ trái, OBJ/Maya dùng hệ phải: lật trục X và đảo chiều mặt.
    static string BuildObj(string name, Mesh mesh, Vector3 scale)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;

        var text = new StringBuilder();
        text.AppendLine("# Exported from Unity (Garden)");
        text.AppendLine("o " + name);

        foreach (Vector3 v in vertices)
        {
            Vector3 p = Vector3.Scale(v, scale);
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F5} {1:F5} {2:F5}", -p.x, p.y, p.z));
        }

        foreach (Vector3 n in normals)
        {
            Vector3 q = new Vector3(n.x / scale.x, n.y / scale.y, n.z / scale.z).normalized;
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "vn {0:F5} {1:F5} {2:F5}", -q.x, q.y, q.z));
        }

        // UV tạm bằng chiếu hộp: mỗi đỉnh chiếu theo trục mà pháp tuyến của nó hướng nhiều nhất
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 uv = BoxUv(Vector3.Scale(vertices[i], scale), normals[i]);
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "vt {0:F5} {1:F5}", uv.x, uv.y));
        }

        for (int t = 0; t < triangles.Length; t += 3)
        {
            int a = triangles[t] + 1, b = triangles[t + 1] + 1, c = triangles[t + 2] + 1;
            text.AppendLine($"f {a}/{a}/{a} {c}/{c}/{c} {b}/{b}/{b}");
        }
        return text.ToString();
    }

    static Vector2 BoxUv(Vector3 position, Vector3 normal)
    {
        Vector3 a = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        if (a.y >= a.x && a.y >= a.z) return new Vector2(position.x, position.z);
        return a.x >= a.z ? new Vector2(position.z, position.y) : new Vector2(position.x, position.y);
    }
}
