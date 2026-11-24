using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Women's dorm floor (여자 기숙사): an L-shaped corridor.
//   wing A : straight corridor running east from the entrance (west end)
//              north row : B203 B202 B201 | 계단실 E/V홀 E/V 샤프트 | (gap) | 휴게실
//              south row : B204 .. B210
//   wing B : corridor leaving the junction ~19 degrees east of north
//              east row  : B211 .. B220 (south -> north), 계단실 at the north end
//              west row  : B228 .. B221 (south -> north)
// All geometry is axis-aligned inside a wing frame (z along the corridor, x across) and the wing groups are
// rotated/moved into place, so the existing corridor/room helpers are reused unchanged.

public static class WomenLayout
{
    public const float M = 2.9f, CW = 2.2f, H = 2.45f, RD = 4.5f, X0 = 1.2f, WH = 2.4f;
    public const float JA = 23.4f;                 // junction: along wing A and world x of J
    public const float ANG = 19f;                  // wing B yaw (degrees east of north)
    public const float ZBOT = -4.6f, ZTOP = 27f;   // wing B corridor extent in its frame

    // wing A north row: block intervals (z along the corridor)
    public const float StairA0 = 3 * M, StairA1 = 10.9f, EvHall1 = 13.2f, Ev1 = 15.1f, Shaft1 = 17.0f;
    public const float LoungeZ0 = 18.3f, LoungeZ1 = 20.7f, LoungeDepth = 3.4f;

    public static readonly Vector3 J = new Vector3(JA, 0f, 0f);
    public static Matrix4x4 FrameA() { return Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 90f, 0), Vector3.one); }
    public static Matrix4x4 FrameB() { return Matrix4x4.TRS(J, Quaternion.Euler(0, ANG, 0), Vector3.one); }

    public static Transform MakeWing(string name, Transform parent, bool isA, bool placeNow)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        if (placeNow) PlaceWing(g, isA);
        return g;
    }

    public static void PlaceWing(Transform g, bool isA)
    {
        g.localPosition = isA ? Vector3.zero : J;
        g.localRotation = Quaternion.Euler(0, isA ? 90f : ANG, 0);
    }
}

// ============================================================ corridor scene
public static class WomenCorridorBuilder
{
    const string ScenePath = "Assets/Scenes/WomenCorridor.unity";
    const string PrefabPath = "Assets/Prefabs/WomenCorridor.prefab";
    public const string RootName = "WomenCorridor";

    struct Tube { public Matrix4x4 toWorld, toLocal; public float zmin, zmax; }

    static Tube MakeTube(Matrix4x4 frame, float zmin, float zmax) { return new Tube { toWorld = frame, toLocal = frame.inverse, zmin = zmin, zmax = zmax }; }

    public static void BuildAndCapture()
    {
        Scene scene;
        if (File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        Build();
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        bool ok;
        PrefabUtility.SaveAsPrefabAssetAndConnect(GameObject.Find(RootName), PrefabPath, InteractionMode.AutomatedAction, out ok);
        if (!ok) Debug.LogError("WomenCorridorBuilder: failed to save prefab");
        RoomLookCamera.AttachTo(GameObject.Find("Main Camera"));
        NavBuilder.AddRoomEntry();
        EditorSceneManager.SaveScene(scene);
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
        CorridorBuilder.InitMaterials();
        GameObject old;
        while ((old = GameObject.Find(RootName)) != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;

        var wingA = WomenLayout.MakeWing("Wing A", root, true, true);
        var wingB = WomenLayout.MakeWing("Wing B", root, false, true);
        var tubeA = MakeTube(WomenLayout.FrameA(), 0f, WomenLayout.JA);
        var tubeB = MakeTube(WomenLayout.FrameB(), WomenLayout.ZBOT, WomenLayout.ZTOP);
        BuildShell(wingA, tubeA, tubeB, true);
        BuildShell(wingB, tubeB, tubeA, false);
        BuildDoorsA(wingA);
        BuildDoorsB(wingB);

        var sun = GameObject.Find("Directional Light");
        if (sun != null) { var l = sun.GetComponent<Light>(); l.intensity = 0.05f; l.shadows = LightShadows.None; }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.35f);

        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            cam.transform.position = WorldA(0.1f, 1.5f, 2.0f);
            cam.transform.LookAt(WorldA(0f, 1.3f, 22f));
            var c = cam.GetComponent<Camera>();
            c.fieldOfView = 70f; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
        }
    }

    static Vector3 WorldA(float x, float y, float z) { return WomenLayout.FrameA().MultiplyPoint3x4(new Vector3(x, y, z)); }
    static Vector3 WorldB(float x, float y, float z) { return WomenLayout.FrameB().MultiplyPoint3x4(new Vector3(x, y, z)); }

    // ---------------------------------------------------------------- shell of one straight tube

    static void BuildShell(Transform wing, Tube t, Tube other, bool isA)
    {
        var shell = CorridorBuilder.Group("Shell", wing);
        float hw = WomenLayout.CW / 2f, H = WomenLayout.H;
        float len = t.zmax - t.zmin, zc = (t.zmin + t.zmax) / 2f;

        CorridorBuilder.TBox("Floor Side", shell, new Vector3(0, -0.05f, zc), new Vector3(WomenLayout.CW, 0.1f, len), CorridorBuilder.floorS, 1f);
        CorridorBuilder.TBox("Floor Center", shell, new Vector3(0, 0.005f, zc), new Vector3(1.1f, 0.01f, len), CorridorBuilder.floorC, 1.2f, true);
        CorridorBuilder.TBox("Ceiling", shell, new Vector3(0, H + 0.05f, zc), new Vector3(WomenLayout.CW + 0.3f, 0.1f, len), CorridorBuilder.ceil, 1.2f);

        foreach (int side in new[] { -1, 1 })
        {
            foreach (var iv in OutsideIntervals(t, side * hw, other))
            {
                float l = iv.y - iv.x;
                if (l < 0.06f) continue;
                float c = (iv.x + iv.y) / 2f;
                CorridorBuilder.TBox("Wall", shell, new Vector3(side * (hw + 0.05f), H / 2, c), new Vector3(0.1f, H, l), CorridorBuilder.wall, 1.2f, true);
                CorridorBuilder.TBox("Baseboard", shell, new Vector3(side * (hw - 0.012f), 0.05f, c), new Vector3(0.024f, 0.1f, l), CorridorBuilder.baseboard, 1f);
            }
        }

        var lights = CorridorBuilder.Group("Lights", wing);
        int n = Mathf.FloorToInt(len / (2f * WomenLayout.M));
        for (int k = 0; k <= n; k++)
        {
            float z = t.zmin + (k + 0.5f) * 2f * WomenLayout.M;
            if (z > t.zmax - 0.5f) break;
            CorridorBuilder.Cyl("Downlight", lights, new Vector3(0, H - 0.004f, z), 0.12f, 0.01f, CorridorBuilder.glow);
            CorridorBuilder.AddLight("Light " + k, lights, new Vector3(0, H - 0.3f, z), new Color(0.97f, 0.98f, 1f), 2.0f, 7.5f, k % 2 == 0 ? LightShadows.Soft : LightShadows.None);
        }

        if (isA)
        {
            // west end: glass entrance door "여자기숙사 출입구"
            var e = CorridorBuilder.Group("Entrance Glass Door", shell);
            CorridorBuilder.TBox("Post L", e, new Vector3(-0.6f, (H - 0.01f) / 2, -0.03f), new Vector3(0.07f, H - 0.01f, 0.1f), CorridorBuilder.steel, 1f);
            CorridorBuilder.TBox("Post R", e, new Vector3(0.6f, (H - 0.01f) / 2, -0.03f), new Vector3(0.07f, H - 0.01f, 0.1f), CorridorBuilder.steel, 1f);
            CorridorBuilder.TBox("Header", e, new Vector3(0, 2.2f, -0.03f), new Vector3(WomenLayout.CW, 0.5f, 0.1f), CorridorBuilder.wall, 1.2f);
            CorridorBuilder.TBox("Glass Door", e, new Vector3(0, 1.0f, -0.03f), new Vector3(1.14f, 2.0f, 0.014f), CorridorBuilder.glassClear, 1f);
            CorridorBuilder.TBox("Glass Side L", e, new Vector3(-0.85f, 1.0f, -0.03f), new Vector3(0.5f, 2.0f, 0.014f), CorridorBuilder.glassClear, 1f);
            CorridorBuilder.TBox("Glass Side R", e, new Vector3(0.85f, 1.0f, -0.03f), new Vector3(0.5f, 2.0f, 0.014f), CorridorBuilder.glassClear, 1f);
            CorridorBuilder.Cyl("Handle A", e, new Vector3(0.4f, 1.0f, 0.04f), 0.014f, 0.6f, CorridorBuilder.steel);
            CorridorBuilder.Cyl("Handle B", e, new Vector3(0.28f, 1.0f, 0.04f), 0.014f, 0.6f, CorridorBuilder.steel);
            CorridorBuilder.Quad("Sign Entrance", e, new Vector3(0f, 2.22f, 0.027f), 180f, new Vector2(1.6f, 0.32f), CorridorBuilder.SignMat("womenentry"));
            CorridorBuilder.ExitSign(shell, new Vector3(0f, H - 0.17f, 2.4f), 0f, CorridorBuilder.exitGlow);
            CorridorBuilder.TBox("Exit Sign Hanger", shell, new Vector3(0f, H - 0.04f, 2.4f), new Vector3(0.03f, 0.08f, 0.03f), CorridorBuilder.steel, 1f);
        }
        else
        {
            // north end: black grid window + railing; south end: plain wall
            CorridorBuilder.RailEnd("End Window", shell, new Vector3(0f, 0f, WomenLayout.ZTOP + 0.02f));
            CorridorBuilder.ExitSign(shell, new Vector3(0f, H - 0.17f, WomenLayout.ZTOP - 2.4f), 0f, CorridorBuilder.exitGlow);
            CorridorBuilder.TBox("Exit Sign Hanger", shell, new Vector3(0f, H - 0.04f, WomenLayout.ZTOP - 2.4f), new Vector3(0.03f, 0.08f, 0.03f), CorridorBuilder.steel, 1f);
            CorridorBuilder.TBox("Wall End", shell, new Vector3(0, H / 2, WomenLayout.ZBOT - 0.05f), new Vector3(WomenLayout.CW + 0.2f, H, 0.1f), CorridorBuilder.wall, 1.2f);
        }
    }

    // parts of the wall line x = x0 (in tube t's frame) that are NOT inside the other tube
    static List<Vector2> OutsideIntervals(Tube t, float x0, Tube o)
    {
        const float eps = 0.04f;
        float hw = WomenLayout.CW / 2f;
        Vector3 p0 = o.toLocal.MultiplyPoint3x4(t.toWorld.MultiplyPoint3x4(new Vector3(x0, 0, 0)));
        Vector3 p1 = o.toLocal.MultiplyPoint3x4(t.toWorld.MultiplyPoint3x4(new Vector3(x0, 0, 1)));
        float lo = -1e9f, hi = 1e9f;
        Slab(ref lo, ref hi, p0.x, p1.x - p0.x, -hw - eps, hw + eps);
        Slab(ref lo, ref hi, p0.z, p1.z - p0.z, o.zmin - eps, o.zmax + eps);
        var res = new List<Vector2>();
        if (lo >= hi) { res.Add(new Vector2(t.zmin, t.zmax)); return res; }
        if (lo > t.zmin) res.Add(new Vector2(t.zmin, Mathf.Min(lo, t.zmax)));
        if (hi < t.zmax) res.Add(new Vector2(Mathf.Max(hi, t.zmin), t.zmax));
        return res;
    }

    static void Slab(ref float lo, ref float hi, float a, float b, float mn, float mx)
    {
        if (Mathf.Abs(b) < 1e-6f) { if (a < mn || a > mx) { lo = 1f; hi = 0f; } return; }
        float t1 = (mn - a) / b, t2 = (mx - a) / b;
        if (t1 > t2) { var tmp = t1; t1 = t2; t2 = tmp; }
        lo = Mathf.Max(lo, t1); hi = Mathf.Min(hi, t2);
    }

    // ---------------------------------------------------------------- doors

    static void BuildDoorsA(Transform wing)
    {
        var doors = CorridorBuilder.Group("Doors", wing);
        float M = WomenLayout.M;
        // north row (side -1)
        for (int i = 0; i < 3; i++)
        {
            float zc = (i + 0.5f) * M;
            CorridorBuilder.RoomDoor(doors, -1, zc, "B" + (203 - i), i);
            if (i == 1) CorridorBuilder.UtilityDoor(doors, -1, zc + 1.05f);
        }
        float s0 = WomenLayout.StairA0, s1 = WomenLayout.StairA1, h1 = WomenLayout.EvHall1, e1 = WomenLayout.Ev1, x1 = WomenLayout.Shaft1;
        CorridorBuilder.StairDoor(doors, -1, (s0 + s1) / 2f, "stair");
        CorridorBuilder.PanelDoor(doors, -1, (s1 + h1) / 2f, 1.0f, 2.1f, CorridorBuilder.doorDark, "EV Hall Door");
        CorridorBuilder.Plate(doors, -1, (s1 + h1) / 2f, 2.2f, "evhall", 0.45f, 0.15f);
        CorridorBuilder.EvDoor(doors, -1, (h1 + e1) / 2f);
        CorridorBuilder.Plate(doors, -1, (h1 + e1) / 2f, 2.2f, "ev", 0.45f, 0.15f);
        CorridorBuilder.PanelDoor(doors, -1, (e1 + x1) / 2f, 0.55f, 1.9f, CorridorBuilder.doorLight, "Shaft Door");
        CorridorBuilder.Plate(doors, -1, (e1 + x1) / 2f, 2.05f, "shaft", 0.45f, 0.15f);
        LoungeDoor(doors, -1, (WomenLayout.LoungeZ0 + WomenLayout.LoungeZ1) / 2f, WomenLayout.LoungeZ1 - WomenLayout.LoungeZ0);
        // south row (side +1)
        for (int i = 0; i < 7; i++)
        {
            float zc = (i + 0.5f) * M;
            CorridorBuilder.RoomDoor(doors, +1, zc, "B" + (204 + i), i);
            if (i % 2 == 1) CorridorBuilder.UtilityDoor(doors, +1, zc + 1.05f);
        }
    }

    static void BuildDoorsB(Transform wing)
    {
        var doors = CorridorBuilder.Group("Doors", wing);
        float M = WomenLayout.M;
        for (int i = 0; i < 10; i++)     // east row: B211 (south) .. B220 (north)
        {
            float zc = WomenLayout.ZBOT + (i + 0.5f) * M;
            CorridorBuilder.RoomDoor(doors, +1, zc, "B" + (211 + i), i);
            if (i % 2 == 0) CorridorBuilder.UtilityDoor(doors, +1, zc + 1.05f);
        }
        CorridorBuilder.StairDoor(doors, +1, WomenLayout.ZTOP - 1.3f, "stair");
        for (int i = 0; i < 8; i++)      // west row: B221 (north) .. B228 (south)
        {
            float zc = WomenLayout.ZTOP - (i + 0.5f) * M;
            CorridorBuilder.RoomDoor(doors, -1, zc, "B" + (221 + i), i);
            if (i % 2 == 1) CorridorBuilder.UtilityDoor(doors, -1, zc + 1.05f);
        }
    }

    // glass storefront of the lounge on a corridor wall (side -1 or +1)
    static void LoungeDoor(Transform parent, int side, float zc, float width)
    {
        var g = CorridorBuilder.Group("Lounge Entrance", parent);
        float x = side * (WomenLayout.CW / 2f - 0.03f);
        float xi = x - side * 0.01f;
        CorridorBuilder.TBox("Glass", g, new Vector3(x, 1.0f, zc), new Vector3(0.014f, 2.0f, width - 0.1f), CorridorBuilder.glassClear, 1f);
        foreach (float dz in new[] { -width / 2 + 0.05f, -0.5f, 0.5f, width / 2 - 0.05f })
            CorridorBuilder.TBox("Post", g, new Vector3(xi, (WomenLayout.H - 0.01f) / 2, zc + dz), new Vector3(0.08f, WomenLayout.H - 0.01f, 0.07f), CorridorBuilder.steel, 1f);
        CorridorBuilder.TBox("Header", g, new Vector3(xi, 2.28f, zc), new Vector3(0.1f, 0.34f, width), CorridorBuilder.steel, 1f);
        CorridorBuilder.TBox("Kick", g, new Vector3(xi, 0.06f, zc), new Vector3(0.1f, 0.12f, width), CorridorBuilder.steel, 1f);
        CorridorBuilder.Cyl("Handle A", g, new Vector3(xi - side * 0.05f, 1.0f, zc + 0.42f), 0.014f, 0.6f, CorridorBuilder.steel);
        CorridorBuilder.Cyl("Handle B", g, new Vector3(xi - side * 0.05f, 1.0f, zc + 0.3f), 0.014f, 0.6f, CorridorBuilder.steel);
        CorridorBuilder.Quad("Backing", g, new Vector3(x - side * 0.5f, 1.2f, zc), side < 0 ? 270f : 90f, new Vector2(width, 2.4f), CorridorBuilder.backing);
        CorridorBuilder.Plate(g, side, zc, 2.0f, "lounge", 0.55f, 0.18f);
    }

    // ---------------------------------------------------------------- capture

    static void CaptureAll(string dir)
    {
        Directory.CreateDirectory(dir);
        Shot("w1_west_entrance", GameObject.Find("Main Camera").GetComponent<Camera>(), 900, 700, dir);
        Shot2("w2_wingA_east", WorldA(0f, 1.5f, 3.0f), WorldA(0f, 1.3f, 22f), dir);
        Shot2("w3_junction", WorldA(0f, 1.5f, 15f), WorldB(0f, 1.3f, 14f), dir);
        Shot2("w4_wingB_north", WorldB(0.1f, 1.5f, -2.8f), WorldB(0f, 1.3f, 26f), dir);
        Shot2("w5_wingB_back", WorldB(0f, 1.5f, 23f), WorldB(0f, 1.3f, -2f), dir);
        Shot2("w6_wingA_back", WorldA(0f, 1.5f, 20f), WorldA(0f, 1.3f, 0f), dir);

        var ceil = new List<Renderer>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.name == "Ceiling") { ceil.Add(r); r.enabled = false; }
        var g = new GameObject("_cam");
        var c = g.AddComponent<Camera>();
        c.fieldOfView = 40f; c.nearClipPlane = 0.1f; c.farClipPlane = 200f;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.11f, 0.13f);
        g.AddComponent<UniversalAdditionalCameraData>();
        g.transform.position = new Vector3(18f, 62f, 11f);
        g.transform.LookAt(new Vector3(18f, 0f, 11f), Vector3.forward);
        Shot("w7_top", c, 1200, 900, dir);
        Object.DestroyImmediate(g);
        foreach (var r in ceil) r.enabled = true;
    }

    static void Shot2(string name, Vector3 pos, Vector3 look, string dir)
    {
        var g = new GameObject("_cam");
        var c = g.AddComponent<Camera>();
        c.fieldOfView = 70f; c.nearClipPlane = 0.03f; c.farClipPlane = 120f;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
        g.AddComponent<UniversalAdditionalCameraData>();
        g.transform.position = pos; g.transform.LookAt(look);
        Shot(name, c, 900, 700, dir);
        Object.DestroyImmediate(g);
    }

    static void Shot(string name, Camera cam, int w, int h, string dir)
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

// ============================================================ top-view scene
public static class WomenFloorViewBuilder
{
    const string ScenePath = "Assets/Scenes/WomenFloorView.unity";
    const string PrefabPath = "Assets/Prefabs/WomenFloorView.prefab";
    const string RootName = "WomenFloorView";

    public static void BuildAndCapture()
    {
        Scene scene;
        if (File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        Build();
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        bool ok;
        PrefabUtility.SaveAsPrefabAssetAndConnect(GameObject.Find(RootName), PrefabPath, InteractionMode.AutomatedAction, out ok);
        if (!ok) Debug.LogError("WomenFloorViewBuilder: failed to save prefab");
        NavBuilder.AddRoomEntry();
        EditorSceneManager.SaveScene(scene);
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
        float M = WomenLayout.M, X0 = WomenLayout.X0, RD = WomenLayout.RD, WH = WomenLayout.WH;

        var ground = FloorViewBuilder.Flat("Ground", new Color(0.12f, 0.13f, 0.15f));
        FloorViewBuilder.TBox("Ground", root, new Vector3(20f, -0.3f, 11f), new Vector3(58f, 0.1f, 52f), ground, 4f);

        // corridor (ceiling, light fixtures, exit signs and backdrops off)
        var corrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WomenCorridor.prefab");
        var corr = (GameObject)PrefabUtility.InstantiatePrefab(corrPrefab, root);
        corr.name = "Corridor";
        foreach (var t in corr.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Ceiling" && t.GetComponent<Renderer>() != null) t.GetComponent<Renderer>().enabled = false;
            if (t.name == "Downlight" || t.name.StartsWith("Exit Sign") || t.name == "Backing") t.gameObject.SetActive(false);
        }

        // ---- wing A (frame kept at identity while furniture is placed, moved into place at the end)
        var wingA = WomenLayout.MakeWing("Wing A", root, true, false);
        var walls = FloorViewBuilder.Group("Walls", wingA);
        for (int i = 0; i < 3; i++) FloorViewBuilder.BuildRoom(wingA, -1, (i + 0.5f) * M, "B" + (203 - i));
        FloorViewBuilder.Mass(wingA, "Stair Block A", WomenLayout.StairA0, WomenLayout.StairA1, "stair");
        FloorViewBuilder.Mass(wingA, "EV Hall", WomenLayout.StairA1, WomenLayout.EvHall1, "evhall");
        FloorViewBuilder.Mass(wingA, "EV", WomenLayout.EvHall1, WomenLayout.Ev1, "ev");
        FloorViewBuilder.Mass(wingA, "Shaft", WomenLayout.Ev1, WomenLayout.Shaft1, "shaft");
        FloorViewBuilder.Mass(wingA, "Void", WomenLayout.Shaft1, WomenLayout.LoungeZ0, null);
        for (int i = 0; i < 7; i++) FloorViewBuilder.BuildRoom(wingA, +1, (i + 0.5f) * M, "B" + (204 + i));

        var loungePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RestLounge.prefab");
        var lounge = (GameObject)PrefabUtility.InstantiatePrefab(loungePrefab, wingA);
        lounge.name = "Rest Lounge";
        lounge.transform.localScale = Vector3.one * 0.62f;
        lounge.transform.localRotation = Quaternion.Euler(0, 270f, 0);
        lounge.transform.localPosition = new Vector3(-(X0 - 0.05f), 0f, (WomenLayout.LoungeZ0 + WomenLayout.LoungeZ1) / 2f);
        foreach (var t in lounge.GetComponentsInChildren<Transform>(true))
        {
            if ((t.name == "Ceiling" || t.name == "Hall Ceiling") && t.GetComponent<Renderer>() != null) t.GetComponent<Renderer>().enabled = false;
            if (t.name == "Storefront" || t.name == "Outside" || t.name == "Downlight" || t.name == "Speaker" || t.name == "Smoke Detector") t.gameObject.SetActive(false);
        }
        foreach (var l in lounge.GetComponentsInChildren<Light>(true)) l.intensity *= 0.5f;

        // partition / outer walls of wing A rows (north row generic part ends at the shaft; lounge has its own short walls)
        Partitions(walls, -1, new[] { 0f, M, 2 * M, 3 * M, WomenLayout.StairA1, WomenLayout.EvHall1, WomenLayout.Ev1, WomenLayout.Shaft1, WomenLayout.LoungeZ0 }, 0f, WomenLayout.LoungeZ0, RD);
        Partitions(walls, -1, new[] { WomenLayout.LoungeZ0, WomenLayout.LoungeZ1 }, WomenLayout.LoungeZ0, WomenLayout.LoungeZ1, WomenLayout.LoungeDepth);
        var southBounds = new List<float>(); for (int k = 0; k <= 7; k++) southBounds.Add(k * M);
        Partitions(walls, +1, southBounds.ToArray(), 0f, 7 * M, RD);

        // ---- wing B
        var wingB = WomenLayout.MakeWing("Wing B", root, false, false);
        var wallsB = FloorViewBuilder.Group("Walls", wingB);
        for (int i = 0; i < 10; i++) FloorViewBuilder.BuildRoom(wingB, +1, WomenLayout.ZBOT + (i + 0.5f) * M, "B" + (211 + i));
        FloorViewBuilder.Mass(wingB, "Stair Block B", WomenLayout.ZTOP - 2.6f, WomenLayout.ZTOP, "stair", +1, 0f);
        for (int i = 0; i < 8; i++) FloorViewBuilder.BuildRoom(wingB, -1, WomenLayout.ZTOP - (i + 0.5f) * M, "B" + (221 + i));
        var eastBounds = new List<float>(); for (int k = 0; k <= 10; k++) eastBounds.Add(WomenLayout.ZBOT + k * M);
        eastBounds.Add(WomenLayout.ZTOP);
        Partitions(wallsB, +1, eastBounds.ToArray(), WomenLayout.ZBOT, WomenLayout.ZTOP, RD);
        var westBounds = new List<float>(); for (int k = 0; k <= 8; k++) westBounds.Add(WomenLayout.ZTOP - k * M);
        Partitions(wallsB, -1, westBounds.ToArray(), WomenLayout.ZTOP - 8 * M, WomenLayout.ZTOP, RD);

        WomenLayout.PlaceWing(wingA, true);
        WomenLayout.PlaceWing(wingB, false);

        // ---- light + camera
        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(62f, 32f, 0f);
            var l = sun.GetComponent<Light>(); l.intensity = 1.25f; l.color = new Color(1f, 0.98f, 0.95f); l.shadows = LightShadows.Soft;
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.56f);

        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            var c = cam.GetComponent<Camera>();
            c.orthographic = false; c.fieldOfView = 32f; c.nearClipPlane = 0.1f; c.farClipPlane = 220f;
            c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.11f, 0.13f);
            var ctrl = cam.GetComponent<FloorViewCamera>();
            if (ctrl == null) ctrl = cam.AddComponent<FloorViewCamera>();
            ctrl.planUp = Vector3.forward;
            ctrl.target = new Vector3(19f, 0f, 11f); ctrl.tilt = 20f; ctrl.yaw = 0f; ctrl.distance = 62f;
            ctrl.maxDistance = 120f; ctrl.panLimitX = new Vector2(-15f, 55f); ctrl.panLimitZ = new Vector2(-20f, 50f);
            ctrl.SnapToGoal();
        }
    }

    // dividing walls between rooms of one row + the outer wall of the row (frame-local, row at side*(X0 .. X0+depth))
    static void Partitions(Transform walls, int side, float[] zs, float zmin, float zmax, float depth)
    {
        float X0 = WomenLayout.X0, WH = WomenLayout.WH;
        foreach (float z in zs)
            FloorViewBuilder.TBox("Partition", walls, new Vector3(side * (X0 + (depth + 0.1f) / 2), WH / 2, z), new Vector3(depth + 0.1f, WH, 0.1f), FloorViewBuilder.wallMat, 1f);
        FloorViewBuilder.TBox("Outer Wall", walls, new Vector3(side * (X0 + depth + 0.05f), WH / 2, (zmin + zmax) / 2f), new Vector3(0.1f, WH, zmax - zmin + 0.1f), FloorViewBuilder.wallMat, 1f);
    }

    static void CaptureAll(string dir)
    {
        Directory.CreateDirectory(dir);
        var main = GameObject.Find("Main Camera");
        var fc = main.GetComponent<FloorViewCamera>();
        var cam = main.GetComponent<Camera>();
        fc.enabled = false;
        Shoot(fc, cam, "f1_top", 0f, new Vector3(19f, 0, 11f), 62f, dir);
        Shoot(fc, cam, "f2_tilt20", 20f, new Vector3(19f, 0, 11f), 62f, dir);
        Shoot(fc, cam, "f3_junction", 0f, new Vector3(23f, 0, 3f), 22f, dir);
        Shoot(fc, cam, "f4_north_end", 20f, new Vector3(31f, 0, 22f), 24f, dir);
        Shoot(fc, cam, "f5_west_end", 0f, new Vector3(8f, 0, 0f), 24f, dir);
        fc.tilt = 20f; fc.target = new Vector3(19f, 0, 11f); fc.distance = 62f;
        fc.SnapToGoal();
        fc.enabled = true;
    }

    static void Shoot(FloorViewCamera fc, Camera cam, string name, float tilt, Vector3 target, float dist, string dir)
    {
        fc.tilt = tilt; fc.target = target; fc.distance = dist;
        fc.SnapToGoal();
        var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prevTarget = cam.targetTexture; var prevActive = RenderTexture.active;
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        cam.targetTexture = prevTarget; RenderTexture.active = prevActive;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
    }
}

public static class WomenRebuild
{
    public static void Run()
    {
        WomenCorridorBuilder.BuildAndCapture();
        WomenFloorViewBuilder.BuildAndCapture();
    }
}
