using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

// Tạo 3 texture mẫu lặp liền mạch cho đá phủ rêu để bạn mở ra vẽ đè lên: đá, rêu, mặt nạ rêu.
// Chỉ tạo khi file chưa có, nên không bao giờ ghi đè texture bạn đã chỉnh.
public static class GardenTextureGenerator
{
    public const string Dir = "Assets/Garden/Textures";
    public const string RockPath = Dir + "/rock_albedo.png";
    public const string MossPath = Dir + "/moss_albedo.png";
    public const string MaskPath = Dir + "/moss_mask.png";

    const int Size = 512;

    public static void EnsureAll()
    {
        Directory.CreateDirectory(Dir);
        Create(RockPath, srgb: true, RockPixel);
        Create(MossPath, srgb: true, MossPixel);
        Create(MaskPath, srgb: false, MaskPixel);
    }

    delegate Color PixelFunc(float u, float v);

    static void Create(string path, bool srgb, PixelFunc pixel)
    {
        if (File.Exists(path)) return;

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var colors = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                colors[y * Size + x] = pixel((x + 0.5f) / Size, (y + 0.5f) / Size);
            }
        }
        texture.SetPixels32(colors);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = srgb;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }

    // ---------- Noise lặp liền mạch ----------

    // Nhiễu fBm trên mặt xuyến (torus) nên mép trái khớp mép phải, mép trên khớp mép dưới
    static float TileableFbm(float u, float v, float frequency, int octaves, float seed)
    {
        float sum = 0f, amplitude = 0.5f, total = 0f;
        for (int i = 0; i < octaves; i++)
        {
            float radius = frequency / (2f * Mathf.PI);
            float a = u * 2f * Mathf.PI, b = v * 2f * Mathf.PI;
            var p = new float4(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, Mathf.Cos(b) * radius, Mathf.Sin(b) * radius);
            sum += amplitude * (noise.snoise(p + seed * 7.13f) * 0.5f + 0.5f);
            total += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }
        return sum / total;
    }

    // ---------- Từng texture ----------

    static Color RockPixel(float u, float v)
    {
        float body = TileableFbm(u, v, 4f, 5, 1f);
        float cracks = 1f - Mathf.Abs(TileableFbm(u, v, 6f, 3, 2f) * 2f - 1f);   // đường nứt: mảnh và dài
        float stain = TileableFbm(u, v, 2f, 2, 3f);                              // loang màu ấm / lạnh

        Color dark = new Color(0.42f, 0.41f, 0.4f);
        Color light = new Color(0.8f, 0.77f, 0.7f);
        Color color = Color.Lerp(dark, light, Mathf.SmoothStep(0.2f, 0.85f, body));
        color = Color.Lerp(color, new Color(0.7f, 0.62f, 0.5f), stain * 0.25f);
        color *= 1f - 0.35f * Mathf.Pow(cracks, 6f);
        return color;
    }

    static Color MossPixel(float u, float v)
    {
        float clump = TileableFbm(u, v, 10f, 4, 4f);
        float fine = TileableFbm(u, v, 48f, 2, 5f);

        Color deep = new Color(0.5f, 0.68f, 0.3f);
        Color bright = new Color(0.8f, 0.94f, 0.55f);
        Color color = Color.Lerp(deep, bright, Mathf.SmoothStep(0.25f, 0.8f, clump));
        return color * (0.8f + 0.35f * fine);
    }

    // R: mảng rêu lớn (độ phủ tăng thì loang theo mảng này). G: hạt mịn tạo mép rêu xù.
    static Color MaskPixel(float u, float v)
    {
        float patches = Mathf.SmoothStep(0.15f, 0.85f, TileableFbm(u, v, 3f, 4, 6f));
        float fuzz = TileableFbm(u, v, 40f, 2, 7f);
        return new Color(patches, fuzz, 0f, 1f);
    }
}
