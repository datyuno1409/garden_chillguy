using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools/Garden/Preview painted boulder: dựng tảng đá vẽ tay (Blender -> Boulder.fbx) bằng shader đá của game, rêu tắt,
// chụp hai góc ra Exports/ (ngoài Assets nên Unity không nhập lại).
// Dựng trong cảnh xem thử riêng (PreviewRenderUtility) nên không đụng tới cảnh đang mở.
// Chạy được cả ở batchmode: -executeMethod RockPreview.RenderBatch
public static class RockPreview
{
    const string ModelPath = "Assets/Garden/Models/Boulder.fbx";
    const string TexturePath = "Assets/Garden/Textures/boulder_albedo.png";
    const string MaterialPath = "Assets/Garden/Materials/BoulderPreview.mat";   // riêng cho ảnh xem thử (rêu tắt), không phải vật liệu trong game
    const string OutputDir = "../Exports";
    const int Width = 1024, Height = 768;

    // Hai góc nhìn từ trên xuống: trước-trái và sau-phải.
    static readonly Vector2[] Views = { new Vector2(35f, 30f), new Vector2(215f, 30f) };
    static readonly string[] ViewNames = { "front", "back" };

    [MenuItem("Tools/Garden/Preview painted boulder")]
    public static void RenderFromMenu() => Render();

    public static void RenderBatch()
    {
        try
        {
            Render();
        }
        catch (Exception e)
        {
            Debug.LogError("RockPreview thất bại: " + e);
            EditorApplication.Exit(1);
            return;
        }
        EditorApplication.Exit(0);
    }

    static void Render()
    {
        AssetDatabase.Refresh();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (model == null) throw new FileNotFoundException(ModelPath);
        if (texture == null) throw new FileNotFoundException(TexturePath);

        Material material = BuildMaterial(texture);
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, OutputDir));
        Directory.CreateDirectory(dir);

        var preview = new PreviewRenderUtility();
        try
        {
            GameObject rock = preview.InstantiatePrefabInScene(model);
            foreach (var r in rock.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
            Bounds bounds = Encapsulate(rock);

            Camera camera = preview.camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.36f, 0.4f, 0.44f);
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            preview.ambientColor = new Color(0.55f, 0.58f, 0.65f);
            preview.lights[0].intensity = 1.1f;
            preview.lights[0].transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            preview.lights[1].intensity = 0.25f;
            float distance = bounds.extents.magnitude / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;

            for (int i = 0; i < Views.Length; i++)
            {
                Quaternion look = Quaternion.Euler(Views[i].y, Views[i].x, 0f);
                preview.BeginStaticPreview(new Rect(0, 0, Width, Height));
                camera.transform.position = bounds.center - look * Vector3.forward * distance;
                camera.transform.rotation = look;
                preview.Render(true);
                Texture2D image = preview.EndStaticPreview();
                string path = Path.Combine(dir, "rock_preview_" + ViewNames[i] + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                Debug.Log("RockPreview: " + path);
            }
        }
        finally
        {
            preview.Cleanup();
        }
    }

    static Material BuildMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Garden/RockMoss");
        if (shader == null) throw new InvalidOperationException("Không thấy shader Garden/RockMoss");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        bool isNew = material == null;
        if (isNew) material = new Material(shader);
        material.shader = shader;
        material.SetTexture("_RockTex", texture);
        material.SetColor("_RockColor", new Color(0.74f, 0.74f, 0.76f));   // hạ sáng: shader cộng thêm ánh sáng lên bóng đã vẽ
        material.SetFloat("_UseUV", 1f);                   // dùng UV của model, không chiếu 3 mặt phẳng
        material.SetFloat("_RockTiling", 1f);
        // Chỉ xem đá, bỏ rêu hẳn: mặt nạ rêu để đen (mặc định trắng sẽ cho rêu mọc trên mặt hướng lên dù độ phủ = 0)
        material.SetTexture("_MossMask", Texture2D.blackTexture);
        material.SetFloat("_MossCoverage", 0f);
        material.SetFloat("_MossUpBias", 0f);
        material.SetFloat("_MossFuzz", 0f);
        material.SetFloat("_MossThickness", 0f);
        material.SetFloat("_WetHalo", 0f);
        material.SetFloat("_MossDamageable", 0f);
        material.SetFloat("_VertexColorStrength", 0f);     // FBX không có màu đỉnh, tránh bị nhân sáng gấp đôi
        // Texture đã vẽ sẵn sáng tối, nên giảm đổ bóng toon để không bị chiếu sáng hai lần
        material.SetColor("_ShadeColor", Color.white);
        material.SetFloat("_AmbientStrength", 0.75f);
        material.SetFloat("_RimStrength", 0f);
        if (isNew) AssetDatabase.CreateAsset(material, MaterialPath);
        else EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    static Bounds Encapsulate(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }
}
