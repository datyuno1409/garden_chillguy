using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Menu Tools/Garden/3. Build garden scene: dựng góc vườn đá theo ảnh tham chiếu vào scene "cozy garden".
// Chạy lại được nhiều lần: xoá nhóm "Garden" cũ và dựng lại. Không đụng _Manager (cửa sổ PIP) và camera/ánh sáng sẵn có.
public static class GardenSceneBuilder
{
    const string ScenePath = "Assets/cozy garden.unity";
    const string RootName = "Garden";
    const string MaterialDir = "Assets/Garden/Materials";
    const string PipelineAssetPath = "Assets/Settings/GardenURP.asset";
    const string VolumePath = "Assets/Garden/GardenVolume.asset";

    static readonly Color ShadeCool = new Color(0.62f, 0.66f, 0.82f);

    [MenuItem("Tools/Garden/3. Build garden scene")]
    public static void Build()
    {
        Scene scene = OpenScene(out bool openedByUs);
        DestroyOld(scene);

        var root = new GameObject(RootName);
        SceneManager_Move(root, scene);

        var materials = CreateMaterials();
        BuildGround(root.transform, materials);
        BuildRocks(root.transform, materials);
        BuildPlants(root.transform, materials);
        BuildBackdrop(root.transform, materials);
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
        public Material rock, lawn, gravel, pebbles, shrub, shrubDark, maple, grass, flowers, wood, wall, paver, trunk;
    }

    static Materials CreateMaterials()
    {
        Directory.CreateDirectory(MaterialDir);
        Shader shader = Shader.Find("Garden/GhibliToon");

        return new Materials
        {
            rock = Mat(shader, "Rock", new Color(0.66f, 0.63f, 0.58f), vertexStrength: 1f, rim: 0.3f),
            lawn = Mat(shader, "Lawn", new Color(0.38f, 0.6f, 0.24f), vertexStrength: 1f, rim: 0.12f),
            gravel = Mat(shader, "Gravel", new Color(0.55f, 0.53f, 0.49f), vertexStrength: 0f),
            pebbles = Mat(shader, "Pebbles", Color.white, vertexStrength: 1f, rim: 0.1f),
            shrub = Mat(shader, "Shrub", new Color(0.3f, 0.58f, 0.24f), vertexStrength: 1f, wind: 0.05f),
            shrubDark = Mat(shader, "ShrubDark", new Color(0.2f, 0.46f, 0.22f), vertexStrength: 1f, wind: 0.04f),
            maple = Mat(shader, "Maple", new Color(0.5f, 0.2f, 0.2f), vertexStrength: 1f, wind: 0.05f),
            grass = Mat(shader, "Grass", new Color(0.5f, 0.78f, 0.3f), vertexStrength: 1f, wind: 0.22f, cullOff: true),
            flowers = Mat(shader, "Flowers", Color.white, vertexStrength: 1f, rim: 0.2f, wind: 0.03f),
            wood = Mat(shader, "Wood", new Color(0.55f, 0.38f, 0.25f), vertexStrength: 0f),
            wall = Mat(shader, "Wall", new Color(0.62f, 0.68f, 0.74f), vertexStrength: 0f),
            paver = Mat(shader, "Paver", new Color(0.55f, 0.55f, 0.55f), vertexStrength: 0f),
            trunk = Mat(shader, "Trunk", new Color(0.36f, 0.25f, 0.2f), vertexStrength: 0f),
        };
    }

    static Material Mat(Shader shader, string name, Color baseColor, float vertexStrength, float rim = 0.2f,
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

    // ---------- Mặt đất ----------

    static void BuildGround(Transform parent, Materials m)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Prepare(ground, "GravelBed", parent, new Vector3(0f, 0f, 6f), m.gravel);
        ground.transform.localScale = new Vector3(6f, 1f, 4f);   // 60 x 40 m, đủ rộng để không thấy mép

        // Sỏi: gộp nhiều viên vào một mesh
        var pebbles = NewEmpty("Pebbles", parent, new Vector3(0.6f, 0f, -0.4f), m.pebbles);
        pebbles.AddComponent<BlobScatter>().Apply(ScatterSettings.Pebbles(4, new Vector2(10f, 6.5f), 1100));

        // Bãi cỏ phía trước: một mô đất dẹt có viền tự nhiên
        var lawn = NewBlob("Lawn", parent, new Vector3(-2.6f, -0.12f, -3.0f), new Vector3(3.6f, 0.3f, 2.4f), m.lawn,
            new BlobSettings
            {
                shape = new BlobShape { seed = 9, subdivisions = 4, noiseAmplitude = 0.07f, noiseFrequency = 1.1f, flattenBottom = 0f, flatShaded = false },
                topTint = new Color(1.12f, 1.1f, 0.85f), bottomTint = new Color(0.8f, 0.9f, 0.7f), colorNoise = 0.08f, sway = 0f,
            });
        lawn.name = "Lawn";

        // Vài viên gạch lát lối đi ở góc dưới bên phải
        for (int i = 0; i < 4; i++)
        {
            var paver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Prepare(paver, "Paver" + i, parent, new Vector3(2.6f + i * 0.55f, 0.03f, -3.2f + i * 0.65f), m.paver);
            paver.transform.localScale = new Vector3(0.95f, 0.07f, 0.55f);
            paver.transform.rotation = Quaternion.Euler(0f, 12f + i * 3f, 0f);
        }
    }

    // ---------- Đá ----------

    static void BuildRocks(Transform parent, Materials m)
    {
        var rocks = new GameObject("Rocks").transform;
        rocks.SetParent(parent, false);

        // x, z, bán kính x/y/z, seed
        float[][] layout =
        {
            new[] { 0.1f, -0.2f, 1.3f, 0.8f, 1.05f, 3f },
            new[] { -1.9f, 0.3f, 0.95f, 0.7f, 0.85f, 7f },
            new[] { -2.7f, 1.2f, 0.9f, 0.8f, 0.8f, 11f },
            new[] { 1.9f, -0.1f, 0.85f, 0.65f, 0.8f, 5f },
            new[] { 2.4f, 0.9f, 0.9f, 0.8f, 0.8f, 13f },
            new[] { -0.2f, 1.3f, 0.7f, 0.5f, 0.6f, 31f },
            new[] { 0.9f, -1.35f, 0.6f, 0.38f, 0.5f, 17f },
            new[] { -0.9f, -1.0f, 0.5f, 0.35f, 0.45f, 19f },
            new[] { -1.5f, -0.9f, 0.38f, 0.26f, 0.34f, 23f },
            new[] { 1.7f, -1.2f, 0.35f, 0.26f, 0.32f, 29f },
        };

        foreach (float[] r in layout)
        {
            BlobSettings settings = BlobSettings.Rock((int)r[5]);
            var scale = new Vector3(r[2], r[3], r[4]);
            float y = scale.y * (1f - settings.shape.flattenBottom) - 0.08f;   // đáy hơi lún xuống đất

            GameObject rock = NewBlob("Rock_" + (int)r[5], rocks, new Vector3(r[0], y, r[1]), scale, m.rock, settings);
            rock.transform.rotation = Quaternion.Euler(0f, r[5] * 23f, 0f);
        }
    }

    // ---------- Cây cỏ hoa ----------

    static void BuildPlants(Transform parent, Materials m)
    {
        var plants = new GameObject("Plants").transform;
        plants.SetParent(parent, false);

        // Cẩm tú cầu trắng bên phải: cụm hoa nhỏ rải dày trên bụi
        Bush(plants, "Hydrangea", new Vector3(1.7f, 1.0f, 2.4f), new Vector3(1.0f, 0.85f, 0.9f), m.shrub, m.flowers, 41,
            ScatterSettings.Blossoms(5, 1.0f, 36, new Vector2(0.12f, 0.2f), new Color(0.97f, 0.96f, 0.9f), new Color(0.92f, 0.94f, 0.88f), new Color(0.98f, 0.97f, 0.95f)));

        // Hoa san hô / đỏ ở giữa phía sau đá
        Bush(plants, "CoralFlowers", new Vector3(-0.1f, 0.95f, 2.1f), new Vector3(0.8f, 0.6f, 0.7f), m.shrub, m.flowers, 47,
            ScatterSettings.Blossoms(6, 1.0f, 26, new Vector2(0.09f, 0.15f), new Color(0.93f, 0.4f, 0.3f), new Color(0.96f, 0.55f, 0.4f), new Color(0.85f, 0.3f, 0.3f)));

        // Oải hương tím
        Bush(plants, "Lavender", new Vector3(-1.5f, 0.7f, 1.6f), new Vector3(0.55f, 0.45f, 0.5f), m.shrubDark, m.flowers, 53,
            ScatterSettings.Blossoms(7, 1.0f, 30, new Vector2(0.06f, 0.1f), new Color(0.6f, 0.45f, 0.82f), new Color(0.5f, 0.38f, 0.75f)));

        // Cỏ trang trí cao nổi bật phía sau đá, và cỏ nhỏ chen quanh đá
        Grass(plants, new Vector3(-0.9f, 0f, 1.0f), GrassSettings.Clump(11, 1.5f, 0.55f, 160), m.grass);
        Grass(plants, new Vector3(0.9f, 0f, 0.9f), GrassSettings.Clump(12, 1.35f, 0.5f, 150), m.grass);
        Grass(plants, new Vector3(0.3f, 0f, 2.0f), GrassSettings.Clump(13, 1.2f, 0.5f, 110), m.grass);
        Grass(plants, new Vector3(-2.3f, 0f, 2.0f), GrassSettings.Clump(14, 1.3f, 0.55f, 120), m.grass);
        Grass(plants, new Vector3(-1.2f, 0f, -1.3f), GrassSettings.Clump(15, 0.4f, 0.25f, 40), m.grass);
        Grass(plants, new Vector3(1.2f, 0f, -0.9f), GrassSettings.Clump(16, 0.45f, 0.25f, 40), m.grass);
        Grass(plants, new Vector3(-3.5f, 0.1f, -1.2f), GrassSettings.Clump(17, 0.35f, 0.4f, 45), m.grass);

        // Mép bãi cỏ
        for (int i = 0; i < 6; i++)
        {
            Grass(plants, new Vector3(-4.8f + i * 0.9f, 0.1f, -2.0f - (i % 2) * 0.5f),
                GrassSettings.Clump(30 + i, 0.3f, 0.3f, 30), m.grass);
        }
    }

    static void Bush(Transform parent, string name, Vector3 position, Vector3 scale, Material leaves, Material blossomMaterial,
        int seed, ScatterSettings blossoms)
    {
        GameObject bush = NewBlob(name, parent, position, scale, leaves, BlobSettings.Shrub(seed));

        // Hoa bám trên bề mặt bụi: con của bụi nên theo cùng tỉ lệ
        var flowerObject = NewEmpty(name + "_Blossoms", bush.transform, Vector3.zero, blossomMaterial);
        flowerObject.AddComponent<BlobScatter>().Apply(blossoms);
    }

    static void Grass(Transform parent, Vector3 position, GrassSettings settings, Material material)
    {
        var grass = NewEmpty("Grass", parent, position, material);
        grass.AddComponent<GrassTuft>().Apply(settings);
    }

    // ---------- Phông nền ----------

    static void BuildBackdrop(Transform parent, Materials m)
    {
        var backdrop = new GameObject("Backdrop").transform;
        backdrop.SetParent(parent, false);

        // Hàng rào gỗ bên trái, đủ dài để không lộ mép hai bên
        for (int i = 0; i < 32; i++)
        {
            float x = -14f + i * 0.5f;
            float height = 4.6f + (i % 3) * 0.08f;
            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Prepare(plank, "Plank" + i, backdrop, new Vector3(x, height * 0.5f, 6.2f), m.wood);
            plank.transform.localScale = new Vector3(0.44f, height, 0.1f);
        }

        // Tường nhà xám xanh bên phải
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Prepare(wall, "Wall", backdrop, new Vector3(8.6f, 2.5f, 6.3f), m.wall);
        wall.transform.localScale = new Vector3(14.4f, 5f, 0.3f);

        // Bụi cây lớn phía sau và cây phong đỏ ở góc trái
        NewBlob("BackShrubL", backdrop, new Vector3(-3.0f, 1.0f, 3.9f), new Vector3(1.7f, 1.3f, 1.4f), m.shrub, BlobSettings.Shrub(61));
        NewBlob("BackShrubC", backdrop, new Vector3(-0.2f, 1.2f, 4.5f), new Vector3(1.9f, 1.5f, 1.4f), m.shrubDark, BlobSettings.Shrub(62));
        NewBlob("BackShrubR", backdrop, new Vector3(3.0f, 1.0f, 4.0f), new Vector3(1.6f, 1.2f, 1.3f), m.shrub, BlobSettings.Shrub(63));

        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Prepare(trunk, "MapleTrunk", backdrop, new Vector3(-3.4f, 1.1f, 3.4f), m.trunk);
        trunk.transform.localScale = new Vector3(0.18f, 1.1f, 0.18f);
        NewBlob("Maple", backdrop, new Vector3(-3.4f, 2.7f, 3.4f), new Vector3(1.5f, 1.1f, 1.3f), m.maple, BlobSettings.Shrub(64));
    }

    // ---------- Ánh sáng, camera, hậu kỳ ----------

    static void SetupLighting()
    {
        Light sun = Object.FindAnyObjectByType<Light>();
        if (sun != null)
        {
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
        }

        // Ánh sáng môi trường 3 tầng: trời xanh nhạt, ngang tầm vàng xanh, mặt đất ấm
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.78f, 0.95f);
        RenderSettings.ambientEquatorColor = new Color(0.78f, 0.82f, 0.68f);
        RenderSettings.ambientGroundColor = new Color(0.5f, 0.45f, 0.38f);
    }

    static void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("GardenSceneBuilder: không thấy Main Camera.");
            return;
        }

        // Nhìn chếch từ trên xuống như ảnh tham chiếu, ống kính hơi tele cho cảm giác mô hình thu nhỏ
        camera.transform.position = new Vector3(0f, 4.0f, -7.2f);
        camera.transform.LookAt(new Vector3(0f, 0.9f, 1.6f));
        camera.fieldOfView = 42f;

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

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.28f);
            bloom.scatter.Override(0.7f);

            var colors = profile.Add<ColorAdjustments>(true);
            colors.saturation.Override(14f);
            colors.contrast.Override(6f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.16f);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            foreach (VolumeComponent component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
        }

        var volumeObject = new GameObject("PostProcessing");
        volumeObject.transform.SetParent(parent, false);
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    // Đổ bóng đủ dùng cho khung hình nhỏ, tiết kiệm GPU khi chạy cả ngày
    static void TuneRenderPipeline()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
        if (pipeline == null) return;

        pipeline.shadowDistance = 16f;
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

    static void SceneManager_Move(GameObject go, Scene scene)
    {
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }

    static Scene OpenScene(out bool openedByUs)
    {
        Scene existing = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        openedByUs = !existing.isLoaded;
        return existing.isLoaded ? existing : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }

    // Xoá nhóm Garden cũ và quả bóng thử nghiệm
    static void DestroyOld(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RootName || root.name == "Sphere") Object.DestroyImmediate(root);
        }
    }
}
