using System.IO;
using Garden.Core;
using UnityEditor;
using UnityEngine;

// Công cụ để thử rêu mà không phải đợi nhiều ngày. Chỉ dùng trong Editor, không ghi vào vườn thật.
public static class GardenDevTools
{
    [MenuItem("Tools/Garden/Dev/Preview moss at day 0")] static void Day0() { Preview(0); }
    [MenuItem("Tools/Garden/Dev/Preview moss at day 1")] static void Day1() { Preview(1); }
    [MenuItem("Tools/Garden/Dev/Preview moss at day 3")] static void Day3() { Preview(3); }
    [MenuItem("Tools/Garden/Dev/Preview moss at day 5")] static void Day5() { Preview(5); }
    [MenuItem("Tools/Garden/Dev/Preview moss at day 7")] static void Day7() { Preview(7); }
    [MenuItem("Tools/Garden/Dev/Preview moss at day 10")] static void Day10() { Preview(10); }

    [MenuItem("Tools/Garden/Dev/Preview rain ON")] static void RainOn() { PreviewRain(1f); }
    [MenuItem("Tools/Garden/Dev/Preview rain OFF")] static void RainOff() { PreviewRain(0f); }

    static void PreviewRain(float intensity)
    {
        var view = Object.FindAnyObjectByType<Garden.Rain.RainView>();
        if (view == null)
        {
            Debug.LogWarning("GardenDevTools: không thấy RainView, hãy chạy Tools/Garden/3. Build garden scene.");
            return;
        }

        view.PreviewInEditor(intensity);
        SceneView.RepaintAll();
    }

    // Xoá file vườn của Editor (không đụng file vườn thật của bản build) để bắt đầu lại từ ngày 0
    [MenuItem("Tools/Garden/Dev/Reset editor garden state")]
    static void ResetEditorState()
    {
        string path = GardenPaths.StateFile;
        if (File.Exists(path)) File.Delete(path);
        Debug.Log("GardenDevTools: đã xoá " + path);
    }

    public static void Preview(double days)
    {
        var host = Object.FindAnyObjectByType<GardenHost>();
        if (host == null)
        {
            Debug.LogWarning("GardenDevTools: không thấy GardenHost, hãy chạy Tools/Garden/3. Build garden scene.");
            return;
        }

        host.PreviewAge(days);
        SceneView.RepaintAll();
    }
}
