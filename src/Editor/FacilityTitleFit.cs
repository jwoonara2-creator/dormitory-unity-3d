using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// The title pill ("세탁실", "체력단련실", "세미나실") in the facility scenes sits in a canvas that uses
// "Constant Pixel Size", so it looks much bigger than the pill in the room views ("1인실"),
// whose canvas scales with the screen (and whose pill is not doubled in size). This gives those canvases
// the room views' scaler settings (Scale With Screen Size, 1280x720, match 0.5), the same top offset
// and scale 1 for the pill.
public static class FacilityTitleFit
{
    static readonly string[] Scenes =
    {
        "LaundryRoom", "LaundryRoomView", "GymRoom", "GymRoomView", "StudyRoom", "StudyRoomView",
    };

    [MenuItem("Tools/Dorm/Match facility title size to room views")]
    public static void Apply()
    {
        int scenesDone = 0;
        foreach (var name in Scenes)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity", OpenSceneMode.Single);
            bool changed = false;
            foreach (var scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (scaler.gameObject.name != "Canvas") continue; // only the title canvas
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                EditorUtility.SetDirty(scaler);

                var bg = scaler.transform.Find("TitleBg") as RectTransform;
                if (bg != null)
                {
                    var p = bg.anchoredPosition;
                    bg.anchoredPosition = new Vector2(p.x, -14f); // same as RoomView
                    bg.localScale = Vector3.one;                  // the facility scenes had it doubled (2,2,2)
                    EditorUtility.SetDirty(bg);
                }
                changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenesDone++;
            }
        }
        Debug.Log($"FacilityTitleFit: {scenesDone} scenes updated");
    }
}
