using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// TEST: the three dorm rooms (1-person, 2-person, accessible) each combined with the bathroom prefab.
// Concept ("method 2, enlarge the room"):
//   - the bathroom prefab is stripped of its hallway parts, turned 90 degrees and attached to the right wall of
//     the room near the entrance; its door opens straight into the room (a studio apartment with its bathroom)
//   - the room keeps its size; only the entrance wall (doorway) and the shared right wall are rebuilt
//   - furniture that would block the bathroom door is moved a little
// The existing room / bathroom prefabs are NOT modified; everything is combined inside this scene only.
// Run: Unity -batchmode -projectPath <proj> -executeMethod RoomBathTestBuilder.BuildAndCapture -captureDir <dir>
public static class RoomBathTestBuilder
{
    const string RootName = "RoomBathTest";
    const float H = 2.4f;

    class Combo { public string name, prefab; public float w; public float x; public float depth; public string saveName; }
    static readonly Combo[] Combos =
    {
        new Combo { name = "Combo 1-person",   prefab = "Assets/Prefabs/DormRoom1Person.prefab",        w = 2.3f, x = -10f, depth = 5.0f, saveName = "RoomWithBath_1Person" },
        new Combo { name = "Combo 2-person",   prefab = "Assets/Prefabs/TwoPersonDormRoom.prefab",      w = 3.0f, x = 0f, depth = 5.0f, saveName = "RoomWithBath_2Person" },
        new Combo { name = "Combo Accessible", prefab = "Assets/Prefabs/AccessibleSingleRoom.prefab",   w = 3.2f, x = 10f, depth = 4.6f, saveName = "RoomWithBath_Accessible" },
    };

    public static void BuildAndCapture()
    {
        // work scene only: used to build the prefab, never saved (no scene file is created)
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Build();
        AssetDatabase.SaveAssets();
        CaptureAll(Arg("-captureDir") ?? "Captures");
    }

    static string Arg(string key)
    {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
        return null;
    }

    static void Build()
    {
        FloorViewBuilder.InitMats();
        GameObject old;
        while ((old = GameObject.Find(RootName)) != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;

        var ground = FloorViewBuilder.Flat("Ground", new Color(0.12f, 0.13f, 0.15f));
        FloorViewBuilder.TBox("Ground", root, new Vector3(2f, -0.3f, 2.5f), new Vector3(44f, 0.1f, 18f), ground, 4f);

        foreach (var c in Combos) BuildCombo(root, c);

        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(62f, 32f, 0f);
            var l = sun.GetComponent<Light>(); l.intensity = 1.0f * LightTuning.SunScale; l.color = new Color(1f, 0.98f, 0.95f); l.shadows = LightShadows.Soft;
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = LightTuning.Amb(new Color(0.55f, 0.55f, 0.56f));

        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            var c = cam.GetComponent<Camera>();
            c.orthographic = false; c.fieldOfView = 40f; c.nearClipPlane = 0.1f; c.farClipPlane = 150f;
            c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.11f, 0.13f);
            var ctrl = cam.GetComponent<FloorViewCamera>();
            if (ctrl == null) ctrl = cam.AddComponent<FloorViewCamera>();
            ctrl.planUp = Vector3.forward;                 // the entrance side (z < 0) is at the bottom of the screen
            ctrl.target = new Vector3(0f, 0f, 1f); ctrl.tilt = 25f; ctrl.yaw = 0f; ctrl.distance = 34f;
            ctrl.minDistance = 4f; ctrl.panLimitX = new Vector2(-30f, 30f); ctrl.panLimitZ = new Vector2(-15f, 20f);
            ctrl.SnapToGoal();
        }
    }

    static void BuildCombo(Transform root, Combo c)
    {
        var g = new GameObject(c.name).transform;
        g.SetParent(root, false);
        g.localPosition = new Vector3(c.x, 0f, 0f);

        float w = c.w, hw = w / 2f;
        var wall = FloorViewBuilder.wallMat;

        // ---- the room itself (prefab instance); parts that get replaced are hidden in this scene only
        var room = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(c.prefab), g);
        room.name = "Room";
        Transform D = room.transform;
        float roomD = 0f;
        foreach (var t in room.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n == "Wall Near" || n == "Wall Front L" || n == "Wall Front R" || n == "Wall Front Lintel" || n == "Entrance Door" || n == "Hinge Frame" || n == "Wall Right")
                t.gameObject.SetActive(false);
            if (n == "Wall Back" || n == "Back Below") roomD = Mathf.Max(roomD, t.position.z);
        }
        float depth = c.depth;
        // the room's real wall materials (right wall = the one the bathroom shares; entrance wall)
        Material rightMat = MaterialOf(room.transform, "Wall Right") ?? wall;
        Material frontMat = MaterialOf(room.transform, "Wall Front L") ?? MaterialOf(room.transform, "Wall Near") ?? wall;

        // ---- bathroom module: hallway parts removed, turned 90 degrees, attached to the right wall near the entrance
        // module spans z in [oz-1.15, oz+1.15]; its door (local x -0.25..0.295) ends up at z in [oz-0.295, oz+0.25]
        const float oz = 1.05f;
        var bath = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bathroom.prefab"), g);
        bath.name = "Bathroom";
        foreach (var t in bath.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Hall ")) t.gameObject.SetActive(false);
            if (t.name == "Hall Light") t.gameObject.SetActive(false);
        }
        bath.transform.localRotation = Quaternion.Euler(0, 90f, 0);
        bath.transform.localPosition = new Vector3(hw + 0.1f, 0f, oz);
        SkinModule(bath, g, rightMat);

        // ---- right wall of the room: the bathroom's own front wall (with its door) replaces it where they meet
        float zb1 = oz + 1.15f;                       // bathroom end (z)
        FloorViewBuilder.TBox("Wall Right (rest)", g, new Vector3(hw + 0.075f, H / 2, (zb1 + depth + 0.15f) / 2), new Vector3(0.15f, H + 0.2f, depth + 0.15f - zb1), rightMat, 2f);

        // ---- entrance wall with a doorway x[-0.45, 0.45]; the door leaf stands open outside, along the left
        float fl = -hw - 0.15f, fr = hw + 0.15f;
        FloorViewBuilder.TBox("Entrance Wall L", g, new Vector3((fl - 0.45f) / 2, H / 2, -0.075f), new Vector3(-0.45f - fl, H + 0.2f, 0.15f), frontMat, 2f);
        FloorViewBuilder.TBox("Entrance Wall R", g, new Vector3((0.45f + fr) / 2, H / 2, -0.075f), new Vector3(fr - 0.45f, H + 0.2f, 0.15f), frontMat, 2f);
        FloorViewBuilder.TBox("Entrance Lintel", g, new Vector3(0, 2.25f, -0.075f), new Vector3(0.9f, H - 2.1f + 0.2f, 0.15f), frontMat, 2f);
        FloorViewBuilder.TBox("Entrance Door", g, new Vector3(-0.43f, 1.05f, -0.55f), new Vector3(0.04f, 2.1f, 0.84f), FloorViewBuilder.birch, 1f);

        // ---- furniture that would block the bathroom door (z 0.76 .. 1.3 along the right wall)
        MoveIfExists(room.transform, "Wardrobe", c.name == "Combo 1-person" ? new Vector3(0, 0, 1.5f) : Vector3.zero);
        if (c.name == "Combo 2-person")
        {
            MoveIfExists(room.transform, "Bed Right", new Vector3(0, 0, 0.15f));
            MoveIfExists(room.transform, "Bed Base Right", new Vector3(0, 0, 0.15f));
        }
        SaveCombo(g, c);
    }

    // whole combination (room + bathroom + rebuilt walls) as one prefab; saved at the origin so that it
    // lands where it is dropped in other scenes. The scene object stays connected to the prefab.
    static void SaveCombo(Transform g, Combo c)
    {
        Directory.CreateDirectory("Assets/Prefabs");
        string path = "Assets/Prefabs/" + c.saveName + ".prefab";
        var keep = g.localPosition;
        string oldName = g.name;
        g.localPosition = Vector3.zero;
        g.name = c.saveName;
        bool ok;
        PrefabUtility.SaveAsPrefabAssetAndConnect(g.gameObject, path, InteractionMode.AutomatedAction, out ok);
        if (!ok) Debug.LogError("RoomBathTestBuilder: failed to save " + path);
        g.localPosition = keep;
    }

    static Material MaterialOf(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) { var r = t.GetComponent<Renderer>(); if (r != null) return r.sharedMaterial; }
        return null;
    }

    // thin skins in the room's wall colour over the tiled outer faces of the bathroom module:
    //   - the front wall, on the side facing the room            (module world -x side)
    //   - the back / left / right walls, on their outer faces      (+x, +z, -z)
    // the tile inside the bathroom stays untouched
    static void SkinModule(GameObject bath, Transform parent, Material mat)
    {
        const float t = 0.01f, off = 0.004f;
        foreach (var r in bath.GetComponentsInChildren<Renderer>(false))
        {
            string n = r.name;
            Bounds b = r.bounds;
            Vector3 c, size;
            if (n == "Wall Front L" || n == "Wall Front R" || n == "Wall Front Lintel")
            { c = new Vector3(b.min.x - off - t / 2, b.center.y, b.center.z); size = new Vector3(t, b.size.y, b.size.z); }
            else if (n == "Wall Back")
            { c = new Vector3(b.max.x + off + t / 2, b.center.y, b.center.z); size = new Vector3(t, b.size.y, b.size.z); }
            else if (n == "Wall Left")
            { c = new Vector3(b.center.x, b.center.y, b.max.z + off + t / 2); size = new Vector3(b.size.x, b.size.y, t); }
            else if (n == "Wall Right")
            { c = new Vector3(b.center.x, b.center.y, b.min.z - off - t / 2); size = new Vector3(b.size.x, b.size.y, t); }
            else continue;
            var skin = FloorViewBuilder.TBox("Wall Skin (" + n + ")", parent, Vector3.zero, size, mat, 2f);
            skin.transform.position = c;
        }
    }

    static void MoveIfExists(Transform root, string name, Vector3 delta)
    {
        if (delta == Vector3.zero) return;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) { t.position += delta; return; }
    }

    static void AddLight(Transform parent, Vector3 pos, float intensity)
    {
        var go = new GameObject("Hall Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.97f, 0.92f); l.intensity = intensity * LightTuning.PointScale; l.range = 5f; l.shadows = LightShadows.None;
    }

    // ---------------------------------------------------------------- capture

    static void CaptureAll(string dir)
    {
        Directory.CreateDirectory(dir);
        var hidden = new List<Renderer>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.name == "Ceiling") { hidden.Add(r); r.enabled = false; }

        var main = GameObject.Find("Main Camera");
        var fc = main.GetComponent<FloorViewCamera>();
        var cam = main.GetComponent<Camera>();
        fc.enabled = false;
        Shoot(fc, cam, "t1_all_top", 0f, new Vector3(0f, 0, 2.0f), 34f, 1800, 700, dir);
        Shoot(fc, cam, "t2_all_tilt", 30f, new Vector3(0f, 0, 1.2f), 30f, 1800, 800, dir);
        Shoot(fc, cam, "t3_room1_top", 0f, new Vector3(-8.9f, 0, 1.2f), 9.5f, 1000, 900, dir);
        Shoot(fc, cam, "t4_room2_top", 0f, new Vector3(1.1f, 0, 1.2f), 10f, 1000, 900, dir);
        Shoot(fc, cam, "t5_acc_top", 0f, new Vector3(11.3f, 0, 1.2f), 10f, 1000, 900, dir);
        fc.tilt = 25f; fc.target = new Vector3(0f, 0, 1f); fc.distance = 34f; fc.SnapToGoal(); fc.enabled = true;
        foreach (var r in hidden) r.enabled = true;

        // eye-level views: standing in the entrance hall of the 2-person combo
        Eye("t6_room_to_bath_door", new Vector3(-0.3f, 1.5f, 0.9f), new Vector3(2.6f, 1.0f, 1.05f), dir);
        Eye("t7_entrance_to_room", new Vector3(0.1f, 1.5f, -1.6f), new Vector3(0f, 1.2f, 3.5f), dir);
        Eye("t8_back_to_entrance", new Vector3(0f, 1.5f, 3.8f), new Vector3(0.4f, 1.2f, -0.5f), dir);
        Eye("t9_bath_inside", new Vector3(2.4f, 1.5f, 1.0f), new Vector3(3.9f, 1.0f, 1.9f), dir);
    }

    static void Eye(string name, Vector3 pos, Vector3 look, string dir)
    {
        var g = new GameObject("_cam");
        var c = g.AddComponent<Camera>();
        c.fieldOfView = 70f; c.nearClipPlane = 0.03f; c.farClipPlane = 100f;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.11f, 0.13f);
        g.AddComponent<UniversalAdditionalCameraData>();
        g.transform.position = pos; g.transform.LookAt(look);
        Render(c, name, 900, 650, dir);
        Object.DestroyImmediate(g);
    }

    static void Shoot(FloorViewCamera fc, Camera cam, string name, float tilt, Vector3 target, float dist, int w, int h, string dir)
    {
        fc.tilt = tilt; fc.target = target; fc.distance = dist;
        fc.SnapToGoal();
        Render(cam, name, w, h, dir);
    }

    static void Render(Camera cam, string name, int w, int h, string dir)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prevTarget = cam.targetTexture; var prevActive = RenderTexture.active;
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        cam.targetTexture = prevTarget; RenderTexture.active = prevActive;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
    }
}
