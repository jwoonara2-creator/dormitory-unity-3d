using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Removes the computer gear (monitor, tower, keyboard, mouse, speakers) from the kit's
// "Table_Computer_01_Setup" desk set, keeping the desk and the chair.
public static class DeskCleanup
{
    static readonly HashSet<string> Parts = new HashSet<string>
    {
        "Computer_apt_01", "Monitor_Apt_01", "Screen", "KeyBoard_Apt_01", "Mouse_apt_01", "Speaker Sub", "Speaker Sub (1)",
    };

    // returns how many objects were removed under 'root'
    public static int RemoveComputer(GameObject root)
    {
        if (root == null) return 0;
        var targets = new List<GameObject>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (Parts.Contains(t.name)) targets.Add(t.gameObject);

        // children of a prefab instance can't be deleted, so unpack the desk set first
        var seen = new HashSet<GameObject>();
        foreach (var go in targets)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;
            var inst = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (inst != null && seen.Add(inst))
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        int n = 0;
        foreach (var go in targets)
            if (go != null) { Object.DestroyImmediate(go); n++; }
        return n;
    }
}
