using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Menu Tools/Garden/3. Build garden scene: dựng góc vườn nhỏ với tảng đá phủ rêu vào scene "cozy garden".
// Chạy lại được nhiều lần: xoá nhóm "Garden" cũ và dựng lại. Không đụng _Manager (cửa sổ PIP).
// Không ghi đè texture hay thiết lập vật liệu đá/rêu mà bạn đã chỉnh (chỉ tạo khi chưa có).
public static class GardenSceneBuilder
{
    const string ScenePath = "Assets/cozy garden.unity";
    const string RootName = "Garden";
    const string MaterialDir = "Assets/Garden/Materials";
    const string PipelineAssetPath = "Assets/Settings/GardenURP.asset";
    const string VolumePath = "Assets/Garden/GardenVolume.asset";

    static readonly Color ShadeCool = new Color(0.62f, 0.66f, 0.82f);
    static readonly Vector3 CameraTarget = new Vector3(0f, 1.05f, 0f);   // tâm tảng đá chính

    [MenuItem("Tools/Garden/3. Build garden scene")]
    public static void Build()
    {
        GardenTextureGenerator.EnsureAll();

        Scene scene = OpenScene(out bool openedByUs);
        if (HasHandMadeMeshes(scene))
        {
            Debug.LogWarning("GardenSceneBuilder: có tảng đá đang dùng mô hình bạn gán (Override Mesh). " +
                "Dựng lại sẽ xóa chúng nên đã dừng. Gỡ mô hình đó ra khỏi nhóm Garden (hoặc xóa ô Override Mesh) rồi chạy lại.");
            if (openedByUs) EditorSceneManager.CloseScene(scene, true);
            return;
        }
        DestroyOld(scene);

        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        Materials materials = CreateMaterials();
        BuildGround(root.transform, materials);
        BuildRocks(root.transform, materials);
        BuildDetails(root.transform, materials);
        BuildFramingLeaves(root.transform, materials);
        SetupLighting();
        SetupCamera();
        SetupPostProcessing(root.transform);
        TuneRenderPipeline();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (openedByUs) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("GardenSceneBuilder: đã dựng xong góc vườn.");
    }

    // ---------- Vật liệu ----------

    struct Materials
    {
        public Material rockMoss, mossGround, grass, mushroomCap, mushroomStem, leaf, backdrop, ground;
    }

    static Materials CreateMaterials()
    {
        Directory.CreateDirectory(MaterialDir);
        Shader toon = Shader.Find("Garden/GhibliToon");
        Shader rockMoss = Shader.Find("Garden/RockMoss");

        return new Materials
        {
            rockMoss = RockMossMat(rockMoss, "RockMoss", coverage: 0.6f),
            mossGround = RockMossMat(rockMoss, "MossGround", coverage: 1f),
            grass = ToonMat(toon, "Grass", new Color(0.5f, 0.78f, 0.3f), vertexStrength: 1f, wind: 0.22f, cullOff: true),
            mushroomCap = ToonMat(toon, "MushroomCap", new Color(0.88f, 0.28f, 0.17f), vertexStrength: 1f, rim: 0.3f),
            mushroomStem = ToonMat(toon, "MushroomStem", new Color(0.96f, 0.9f, 0.76f), vertexStrength: 1f),
            leaf = ToonMat(toon, "LeafPlaceholder", new Color(0.3f, 0.55f, 0.22f), vertexStrength: 1f, wind: 0.04f, cullOff: true),
            backdrop = ToonMat(toon, "Backdrop", new Color(0.07f, 0.2f, 0.13f), vertexStrength: 0f, rim: 0f),
            ground = ToonMat(toon, "Ground", new Color(0.1f, 0.26f, 0.15f), vertexStrength: 0f, rim: 0f),
        };
    }

    // Đá phủ rêu: chỉ tạo giá trị mặc định lần đầu, sau đó giữ nguyên những gì bạn đã chỉnh
    static Material RockMossMat(Shader shader, string name, float coverage)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            material.SetFloat("_MossCoverage", coverage);
        }

        SetTextureIfEmpty(material, "_RockTex", GardenTextureGenerator.RockPath);
        SetTextureIfEmpty(material, "_MossTex", GardenTextureGenerator.MossPath);
        SetTextureIfEmpty(material, "_MossMask", GardenTextureGenerator.MaskPath);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetTextureIfEmpty(Material material, string property, string assetPath)
    {
        if (material.GetTexture(property) != null) return;
        material.SetTexture(property, AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
    }

    static Material ToonMat(Shader shader, string name, Color baseColor, float vertexStrength, float rim = 0.2f,
        float wind = 0f, bool cullOff = false)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_BaseColor", baseColor);
        material.SetColor("_ShadeColor", ShadeCool);
        material.SetFloat("_VertexColorStrength", vertexStrength);
        material.SetFloat("_RimStrength", rim);
        material.SetFloat("_WindStrength", wind);
        material.SetFloat("_Cull", cullOff ? 0f : 2f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---------- Mặt đất và nền ----------

    // Nền xanh thẫm mờ phía sau (như tán lá tối trong ảnh mẫu), không có bụi cây nào
    static void BuildGround(Transform parent, Materials m)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Prepare(ground, "Ground", parent, new Vector3(0f, -0.4f, 4f), m.ground);
        ground.transform.localScale = new Vector3(4f, 1f, 4f);

        var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Prepare(backdrop, "Backdrop", parent, new Vector3(0f, 4f, 7f), m.backdrop);
        backdrop.transform.localScale = new Vector3(24f, 14f, 1f);

        // Mô đất rêu mà tảng đá nằm trên: luôn phủ kín rêu
        NewBlob("MossMound", parent, MoundCenter, MoundSize, m.mossGround, Soft(seed: 5, amplitude: 0.05f, subdivisions: 4));
    }

    static readonly Vector3 MoundCenter = new Vector3(0f, -0.28f, 0.2f);
    static readonly Vector3 MoundSize = new Vector3(4.6f, 0.7f, 3.4f);

    // Độ cao gần đúng của mô đất tại (x, z), để đặt nấm và cỏ lên mặt mô đất
    static float MoundHeight(float x, float z)
    {
        float dx = (x - MoundCenter.x) / MoundSize.x;
        float dz = (z - MoundCenter.z) / MoundSize.z;
        return MoundCenter.y + MoundSize.y * Mathf.Sqrt(Mathf.Max(0f, 1f - dx * dx - dz * dz));
    }

    // ---------- Đá ----------

    static void BuildRocks(Transform parent, Materials m)
    {
        var rocks = new GameObject("Rocks").transform;
        rocks.SetParent(parent, false);
        rocks.gameObject.AddComponent<MossGrowth>().Coverage = 0.6f;

        // x, z, bán kính x/y/z, seed. Tảng đầu tiên là tảng chính ở giữa.
        float[][] layout =
        {
            new[] { 0.0f, 0.3f, 2.15f, 1.55f, 1.8f, 3f },
            new[] { -2.45f, -0.6f, 0.65f, 0.42f, 0.55f, 7f },
            new[] { 2.35f, -0.9f, 0.55f, 0.38f, 0.5f, 11f },
            new[] { -1.3f, -2.1f, 0.38f, 0.27f, 0.34f, 17f },
            new[] { 1.5f, -2.3f, 0.3f, 0.22f, 0.28f, 19f },
        };

        foreach (float[] r in layout)
        {
            BlobSettings settings = Soft((int)r[5], amplitude: 0.11f, subdivisions: 4);
            settings.shape.flattenBottom = 0.3f;
            var scale = new Vector3(r[2], r[3], r[4]);
            float y = MoundHeight(r[0], r[1]) + scale.y * (1f - settings.shape.flattenBottom) - 0.35f;   // đáy lún vào rêu

            GameObject rock = NewBlob("Rock_" + (int)r[5], rocks, new Vector3(r[0], y, r[1]), scale, m.rockMoss, settings);
            rock.transform.rotation = Quaternion.Euler(0f, r[5] * 23f, 0f);
        }
    }

    // Khối mịn tròn trịa kiểu tranh vẽ, màu đỉnh gần trung tính để texture quyết định màu
    static BlobSettings Soft(int seed, float amplitude, int subdivisions) => new BlobSettings
    {
        shape = new BlobShape
        {
            seed = seed, subdivisions = subdivisions, noiseAmplitude = amplitude, noiseFrequency = 1.1f,
            flattenBottom = 0f, flatShaded = false,
        },
        topTint = new Color(1.06f, 1.05f, 1f), bottomTint = new Color(0.82f, 0.84f, 0.88f),
        colorNoise = 0.04f, sway = 0f,
    };

    // ---------- Nấm và cỏ nhỏ ----------

    static void BuildDetails(Transform parent, Materials m)
    {
        var details = new GameObject("Details").transform;
        details.SetParent(parent, false);

        // x, z, kích thước
        float[][] mushrooms = { new[] { -1.65f, -1.45f, 1f }, new[] { 1.85f, -1.5f, 0.9f }, new[] { 2.1f, -1.25f, 0.55f } };
        for (int i = 0; i < mushrooms.Length; i++)
        {
            float x = mushrooms[i][0], z = mushrooms[i][1], size = mushrooms[i][2];
            float ground = MoundHeight(x, z);
            NewBlob("MushroomStem" + i, details, new Vector3(x, ground + 0.1f * size, z), new Vector3(0.05f, 0.11f, 0.05f) * size,
                m.mushroomStem, Soft(30 + i, 0.05f, 2));
            NewBlob("MushroomCap" + i, details, new Vector3(x, ground + 0.21f * size, z), new Vector3(0.15f, 0.1f, 0.15f) * size,
                m.mushroomCap, CapSettings(40 + i));
        }

        // Vài bụi cỏ nhỏ quanh chân tảng đá
        float[][] tufts = { new[] { -1.9f, -0.5f }, new[] { 1.7f, -0.3f }, new[] { -0.6f, -2.0f }, new[] { 0.9f, -1.7f }, new[] { 2.4f, -0.5f } };
        for (int i = 0; i < tufts.Length; i++)
        {
            Grass(details, new Vector3(tufts[i][0], MoundHeight(tufts[i][0], tufts[i][1]), tufts[i][1]),
                GrassSettings.Clump(70 + i, 0.4f, 0.25f, 40), m.grass);
        }
    }

    static BlobSettings CapSettings(int seed)
    {
        BlobSettings settings = Soft(seed, 0.04f, 2);
        settings.shape.flattenBottom = 0.45f;
        return settings;
    }

    static void Grass(Transform parent, Vector3 position, GrassSettings settings, Material material)
    {
        var grass = NewEmpty("Grass", parent, position, material);
        grass.AddComponent<GrassTuft>().Apply(settings);
    }

    // ---------- Lá tạm ở rìa khung ----------

    // Lá phẳng đặt tạm ở rìa khung cho có chiều sâu. Bạn thay bằng lá làm trong Maya (xoá hoặc thay nhóm "Placeholders").
    static void BuildFramingLeaves(Transform parent, Materials m)
    {
        var placeholders = new GameObject("Placeholders").transform;
        placeholders.SetParent(parent, false);

        // vị trí, góc xoay (độ), kích thước
        var leaves = new (Vector3 position, Vector3 euler, Vector3 scale)[]
        {
            (new Vector3(-3.1f, 0.3f, -3.4f), new Vector3(-20f, 25f, 15f), new Vector3(0.85f, 0.05f, 0.5f)),
            (new Vector3(3.2f, 0.3f, -3.4f), new Vector3(-15f, -30f, -10f), new Vector3(0.8f, 0.05f, 0.5f)),
            (new Vector3(-3.3f, 3.0f, 2.0f), new Vector3(30f, 40f, 20f), new Vector3(1.1f, 0.05f, 0.65f)),
            (new Vector3(3.2f, 2.6f, 1.5f), new Vector3(15f, -70f, -35f), new Vector3(1.0f, 0.05f, 0.6f)),
        };

        for (int i = 0; i < leaves.Length; i++)
        {
            GameObject leaf = NewBlob("LeafPlaceholder_" + i, placeholders, leaves[i].position, leaves[i].scale, m.leaf,
                BlobSettings.Shrub(80 + i));
            leaf.transform.rotation = Quaternion.Euler(leaves[i].euler);
        }
    }

    // ---------- Ánh sáng, camera, hậu kỳ ----------

    static void SetupLighting()
    {
        Light sun = Object.FindAnyObjectByType<Light>();
        if (sun != null)
        {
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            sun.color = new Color(1f, 0.94f, 0.8f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
        }

        // Ánh sáng môi trường ngả xanh lá như đang ở dưới tán cây
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.78f, 0.6f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.7f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.28f, 0.38f, 0.22f);
    }

    // Ống kính tele đứng xa, nhìn chếch nhẹ từ trên xuống, kết hợp làm mờ nền để cảnh trông như mô hình thu nhỏ trong tấm ảnh
    static void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("GardenSceneBuilder: không thấy Main Camera.");
            return;
        }

        const float distance = 11f;
        const float pitchDegrees = 12f;
        float pitch = pitchDegrees * Mathf.Deg2Rad;
        camera.transform.position = CameraTarget + new Vector3(0f, Mathf.Sin(pitch), -Mathf.Cos(pitch)) * distance;
        camera.transform.LookAt(CameraTarget);
        camera.fieldOfView = 30f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.2f, 0.13f);

        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
    }

    static void SetupPostProcessing(Transform parent)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);
        }

        var bloom = Ensure<Bloom>(profile);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.28f);
        bloom.scatter.Override(0.7f);

        var colors = Ensure<ColorAdjustments>(profile);
        colors.saturation.Override(14f);
        colors.contrast.Override(6f);

        var vignette = Ensure<Vignette>(profile);
        vignette.intensity.Override(0.22f);

        var tonemapping = Ensure<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.Neutral);

        // Làm mờ phía gần và phía xa tảng đá: tạo cảm giác mô hình thu nhỏ
        var depth = Ensure<DepthOfField>(profile);
        depth.mode.Override(DepthOfFieldMode.Bokeh);
        depth.focusDistance.Override(11f);
        depth.focalLength.Override(75f);
        depth.aperture.Override(3.2f);

        EditorUtility.SetDirty(profile);

        var volumeObject = new GameObject("PostProcessing");
        volumeObject.transform.SetParent(parent, false);
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T existing)) return existing;
        T created = profile.Add<T>(true);
        AssetDatabase.AddObjectToAsset(created, profile);
        return created;
    }

    // Đổ bóng đủ dùng cho khung hình nhỏ, tiết kiệm GPU khi chạy cả ngày
    static void TuneRenderPipeline()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
        if (pipeline == null) return;

        pipeline.shadowDistance = 18f;
        pipeline.shadowCascadeCount = 2;
        EditorUtility.SetDirty(pipeline);
    }

    // ---------- Tiện ích ----------

    static GameObject NewBlob(string name, Transform parent, Vector3 position, Vector3 scale, Material material, BlobSettings settings)
    {
        GameObject go = NewEmpty(name, parent, position, material);
        go.transform.localScale = scale;
        go.AddComponent<ProceduralBlob>().Apply(settings);
        return go;
    }

    // GameObject có MeshFilter + MeshRenderer với vật liệu cho sẵn
    static GameObject NewEmpty(string name, Transform parent, Vector3 position, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    // Hình khối có sẵn của Unity: bỏ collider, gắn vào nhóm, đặt vật liệu
    static void Prepare(GameObject go, string name, Transform parent, Vector3 position, Material material)
    {
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static Scene OpenScene(out bool openedByUs)
    {
        Scene existing = SceneManager.GetSceneByPath(ScenePath);
        openedByUs = !existing.isLoaded;
        return existing.isLoaded ? existing : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }

    // Có khối nào trong nhóm Garden đang dùng mô hình do người dùng gán không (dựng lại sẽ làm mất)
    static bool HasHandMadeMeshes(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != RootName) continue;
            foreach (ProceduralBlob blob in root.GetComponentsInChildren<ProceduralBlob>(true))
            {
                if (blob.HasOverrideMesh) return true;
            }
        }
        return false;
    }

    // Xoá nhóm Garden cũ trước khi dựng lại
    static void DestroyOld(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RootName) Object.DestroyImmediate(root);
        }
    }
}
