using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// In the laundry / gym / seminar scenes the top-right "camera" button (opens the *View scene, the room seen
// from above) sits in a "Camera" canvas that uses Constant Pixel Size and is laid out for a ~1440x2560 phone.
// On smaller screens (browser / WebView) it is off screen. This makes that canvas scale with the screen
// (reference 1440x2560, Expand), like MainScreenFit does for the Main map.
public static class FacilityCameraButtonFit
{
    static readonly string[] Scenes =
    {
        "LaundryRoom", "LaundryRoomView", "GymRoom", "GymRoomView", "StudyRoom", "StudyRoomView",
    };

    [MenuItem("Tools/Dorm/Fit facility camera button to any screen size")]
    public static void Apply()
    {
        int n = 0;
        foreach (var name in Scenes)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity", OpenSceneMode.Single);
            bool changed = false;
            foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (scaler.gameObject.name != "Camera") continue;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1440, 2560);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                EditorUtility.SetDirty(scaler);
                changed = true;
            }
            if (!changed) continue;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            n++;
        }
        Debug.Log($"FacilityCameraButtonFit: {n} scenes updated");
    }
}
