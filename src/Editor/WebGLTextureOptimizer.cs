using UnityEditor;
using UnityEngine;

// Shrinks textures for the WebGL build only: a "WebGL" platform override (smaller max size, low-quality ASTC),
// so PC / Android settings of the same textures are untouched.
public static class WebGLTextureOptimizer
{
    const string Platform = "WebGL";
    const int MaxSize = 512;

    [MenuItem("Tools/Dorm/Shrink textures for WebGL")]
    public static void Apply()
    {
        int changed = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Editor/")) continue;
                if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;

                var want = new TextureImporterPlatformSettings
                {
                    name = Platform,
                    overridden = true,
                    maxTextureSize = MaxSize,
                    format = TextureImporterFormat.Automatic,
                    // normal maps keep normal quality (low quality ASTC makes shading blocky)
                    textureCompression = imp.textureType == TextureImporterType.NormalMap
                        ? TextureImporterCompression.Compressed
                        : TextureImporterCompression.CompressedLQ,
                    compressionQuality = 50,
                    crunchedCompression = false,
                };
                var have = imp.GetPlatformTextureSettings(Platform);
                if (have.overridden && have.maxTextureSize == want.maxTextureSize &&
                    have.textureCompression == want.textureCompression) continue;

                imp.SetPlatformTextureSettings(want);
                imp.SaveAndReimport();
                changed++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        Debug.Log($"WebGLTextureOptimizer: {changed} textures updated");
    }
}
