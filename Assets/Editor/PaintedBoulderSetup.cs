using System;
using System.IO;
using Garden.Core;
using Garden.Layout;
using Garden.Moss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools/Garden/Use painted boulder as main rock: gắn tảng đá vẽ tay (Assets/Garden/Models/Boulder.fbx, xuất từ Blender)
// làm tảng đá chính của vườn bằng ô "Override Mesh" có sẵn, dùng vật liệu BoulderPainted (đá vẽ tay + vẫn có rêu mọc).
// Chạy lại được nhiều lần. Muốn quay về đá sinh bằng code: xoá ô Override Mesh của tảng đá chính rồi chạy
// Tools/Garden/3. Build garden scene. Chạy được cả ở batchmode: -executeMethod PaintedBoulderSetup.ApplyBatch
public static class PaintedBoulderSetup
{
    const string ScenePath = "Assets/cozy garden.unity";
    const string ModelPath = "Assets/Garden/Models/Boulder.fbx";
    const string TexturePath = "Assets/Garden/Textures/boulder_albedo.png";
    const string RockMossPath = "Assets/Garden/Materials/RockMoss.mat";
    const string MaterialPath = "Assets/Garden/Materials/BoulderPainted.mat";
    const string CaptureDir = "../Exports";

    const float TargetWidth = 3.7f;       // bề rộng tảng đá chính trong vườn (mét), đá sinh bằng code rộng khoảng 4.3
    const float SinkIntoMound = 0.2f;     // đáy lún vào mô rêu một chút, giống đá sinh bằng code

    // Cùng công thức mô đất trong GardenSceneBuilder (MoundCenter / MoundSize)
    static float MoundHeight(float x, float z)
    {
        float dx = x / 4.6f;
        float dz = (z - 0.2f) / 3.4f;
        return -0.28f + 0.7f * Mathf.Sqrt(Mathf.Max(0f, 1f - dx * dx - dz * dz));
    }

    [MenuItem("Tools/Garden/Use painted boulder as main rock")]
    public static void ApplyFromMenu() => Apply(capture: false);

    public static void ApplyBatch()
    {
        try
        {
            Apply(capture: true);
        }
        catch (Exception e)
        {
            Debug.LogError("PaintedBoulderSetup thất bại: " + e);
            EditorApplication.Exit(1);
            return;
        }
        EditorApplication.Exit(0);
    }

    static void Apply(bool capture)
    {
        AssetDatabase.Refresh();
        Mesh mesh = LoadMesh();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null) throw new FileNotFoundException(TexturePath);
        Material material = BuildMaterial(texture);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedByUs = !scene.isLoaded;
        if (openedByUs) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        ProceduralBlob blob = FindMainRock(scene);
        var so = new SerializedObject(blob);
        so.FindProperty("overrideMesh").objectReferenceValue = mesh;
        so.ApplyModifiedPropertiesWithoutUndo();

        blob.GetComponent<MeshRenderer>().sharedMaterial = material;
        Place(blob.transform, mesh);
        blob.SetCollider(true);   // vùng va chạm theo mesh mới, để click chuột vẫn trúng đá

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"PaintedBoulderSetup: {blob.name} dùng {mesh.name}, scale {blob.transform.localScale.x:F2}, " +
                  $"vị trí {blob.transform.position}");

        if (capture)
        {
            Capture(scene, "garden_with_painted_rock.png");
            MossGrowth moss = blob.GetComponentInParent<MossGrowth>();
            float coverage = moss.Coverage;
            moss.Coverage = 0f;                                  // tắt rêu để thấy đá vẽ tay (cảnh đã lưu rồi, không bị ảnh hưởng)
            Capture(scene, "garden_painted_rock_no_moss.png");
            moss.Coverage = coverage;
        }
        if (openedByUs) EditorSceneManager.CloseScene(scene, true);
    }

    static Mesh LoadMesh()
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            if (asset is Mesh mesh) return mesh;
        throw new FileNotFoundException("Không thấy mesh trong " + ModelPath);
    }

    static ProceduralBlob FindMainRock(Scene scene)
    {
        foreach (LayoutSlot slot in UnityEngine.Object.FindObjectsByType<LayoutSlot>(FindObjectsInactive.Include))
            if (slot.gameObject.scene == scene && slot.Id == LayoutCatalog.MainRock)
                return slot.GetComponent<ProceduralBlob>();
        throw new InvalidOperationException("Không thấy tảng đá chính, hãy chạy Tools/Garden/3. Build garden scene trước.");
    }

    // Mesh từ Blender có kích thước thật (không nhân scale như đá sinh bằng code), nên đặt lại scale và độ cao
    static void Place(Transform rock, Mesh mesh)
    {
        Bounds bounds = mesh.bounds;
        float scale = TargetWidth / Mathf.Max(bounds.size.x, bounds.size.z);
        Vector3 position = rock.position;
        float baseY = MoundHeight(position.x, position.z) - SinkIntoMound;
        rock.localScale = Vector3.one * scale;
        rock.position = new Vector3(position.x, baseY - bounds.min.y * scale, position.z);
    }

    // Sao chép thiết lập rêu từ RockMoss (mặt nạ, texture rêu, bóc rêu bằng click...), chỉ đổi phần liên quan đến đá
    static Material BuildMaterial(Texture2D texture)
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(RockMossPath);
        if (source == null) throw new FileNotFoundException(RockMossPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        bool isNew = material == null;
        if (isNew)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.SetTexture("_RockTex", texture);
        material.SetColor("_RockColor", new Color(0.85f, 0.85f, 0.88f));
        material.SetFloat("_RockUseUV", 1f);               // đá lấy UV của model, rêu vẫn chiếu 3 mặt phẳng
        material.SetFloat("_UseUV", 0f);
        material.SetFloat("_RockTiling", 1f);
        material.SetFloat("_VertexColorStrength", 0f);     // FBX không có màu đỉnh
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    static void Capture(Scene scene, string fileName)
    {
        Camera camera = null;
        foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>())
        {
            if (c.gameObject.scene != scene) continue;
            if (camera == null || c.CompareTag("MainCamera")) camera = c;
        }
        if (camera == null) throw new InvalidOperationException("Không thấy camera trong cảnh vườn");

        const int width = 1280, height = 960;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, CaptureDir, fileName));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = oldTarget;
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
        Debug.Log("PaintedBoulderSetup: ảnh cảnh vườn " + path);
    }
}
