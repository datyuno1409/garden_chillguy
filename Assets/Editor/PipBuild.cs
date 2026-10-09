using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menu Tools/Garden: dựng scene và build ra 2 app (Aquarium + ControlPanel) cạnh nhau trong thư mục Builds.
public static class PipBuild
{
    const string AquariumScene = "Assets/cozy garden.unity";
    const string PanelScene = "Assets/ControlPanel.unity";
    const string ManagerName = "_Manager";

    [MenuItem("Tools/Garden/1. Setup scenes")]
    public static void SetupScenes()
    {
        SetupAquarium();
        SetupPanel();
        AssetDatabase.SaveAssets();
        Debug.Log("PipBuild: đã dựng xong 2 scene.");
    }

    [MenuItem("Tools/Garden/2. Build both apps")]
    public static void BuildBoth()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds"));
        Build(AppNames.Aquarium, AquariumScene, root, resizable: true, new Vector2Int(400, 400));
        Build(AppNames.ControlPanel, PanelScene, root, resizable: true, new Vector2Int(360, 320));
        Debug.Log("PipBuild: build xong tại " + root);
    }

    // Thêm PipWindow, PipCommandServer và PipTitleBar vào _Manager của scene bể cá (không đụng các object khác)
    static void SetupAquarium()
    {
        Scene scene = OpenAdditive(AquariumScene, out bool openedByUs);

        GameObject manager = FindRoot(scene, ManagerName);
        if (manager == null)
        {
            manager = new GameObject(ManagerName);
            SceneManager.MoveGameObjectToScene(manager, scene);
        }
        EnsureComponent<PipWindow>(manager);
        EnsureComponent<PipCommandServer>(manager);
        EnsureComponent<PipTitleBar>(manager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedByUs) EditorSceneManager.CloseScene(scene, true);
    }

    static void SetupPanel()
    {
        if (File.Exists(PanelScene)) return;   // đã có thì giữ nguyên

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        var cameraObject = new GameObject("Main Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.16f, 0.2f, 0.18f);
        SceneManager.MoveGameObjectToScene(cameraObject, scene);

        var panel = new GameObject("ControlPanel", typeof(ControlPanel));
        SceneManager.MoveGameObjectToScene(panel, scene);

        EditorSceneManager.SaveScene(scene, PanelScene);
        EditorSceneManager.CloseScene(scene, true);
    }

    static Scene OpenAdditive(string path, out bool openedByUs)
    {
        Scene existing = SceneManager.GetSceneByPath(path);
        openedByUs = !existing.isLoaded;
        return existing.isLoaded ? existing : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    static void EnsureComponent<T>(GameObject target) where T : Component
    {
        if (target.GetComponent<T>() == null) target.AddComponent<T>();
    }

    // Build một app. Cài đặt Player được đổi tạm rồi trả lại như cũ để không làm bẩn ProjectSettings.
    static void Build(string appName, string scenePath, string root, bool resizable, Vector2Int size)
    {
        string oldName = PlayerSettings.productName;
        int oldWidth = PlayerSettings.defaultScreenWidth;
        int oldHeight = PlayerSettings.defaultScreenHeight;
        bool oldResizable = PlayerSettings.resizableWindow;
        bool oldBackground = PlayerSettings.runInBackground;
        FullScreenMode oldMode = PlayerSettings.fullScreenMode;

        try
        {
            PlayerSettings.productName = appName;
            PlayerSettings.defaultScreenWidth = size.x;
            PlayerSettings.defaultScreenHeight = size.y;
            PlayerSettings.resizableWindow = resizable;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = Path.Combine(root, appName, appName + ".exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Build {appName} thất bại: {report.summary.result}");
        }
        finally
        {
            PlayerSettings.productName = oldName;
            PlayerSettings.defaultScreenWidth = oldWidth;
            PlayerSettings.defaultScreenHeight = oldHeight;
            PlayerSettings.resizableWindow = oldResizable;
            PlayerSettings.runInBackground = oldBackground;
            PlayerSettings.fullScreenMode = oldMode;
        }
    }
}
