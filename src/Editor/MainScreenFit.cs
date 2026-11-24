using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Main.unity's canvases use "Constant Pixel Size", so the photo cards sit at fixed pixel offsets
// (laid out for a ~1440x2560 phone). On any other screen size they get cut off.
// This switches every canvas in Main to "Scale With Screen Size" (reference 1440x2560, Expand),
// so the whole layout always stays on screen.
public static class MainScreenFit
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Tools/Dorm/Fit Main screen UI to any screen size")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int n = 0;
        foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 2560);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            EditorUtility.SetDirty(scaler);
            n++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"MainScreenFit: {n} canvas scalers updated");
    }
}
