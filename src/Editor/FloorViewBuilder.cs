using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Whole-floor cutaway (no ceiling) built around the Corridor prefab, following the floor plan:
//   plan x -> world z (0..L); plan top row = left (x<0), bottom row = right (x>0).
//   left : 계단실 | A225..A215 (11 rooms) | 샤프트 | E/V | E/V홀 | 계단실       right : A201..A214 | 휴게실
// Rooms: bed, desk set (no computer), wardrobe, toilet nook. Lounge: RestLounge prefab scaled 0.8.
// Root "FloorView" -> Assets/Scenes/FloorView.unity and Assets/Prefabs/FloorView.prefab.
// Run: Unity -batchmode -projectPath <proj> -executeMethod FloorViewBuilder.BuildAndCapture -captureDir <dir>
public static class FloorViewBuilder
{
    public const string ScenePath = "Assets/Scenes/FloorView.unity";
    public const string PrefabPath = "Assets/Prefabs/FloorView.prefab";
    public const string MatDir = "Assets/Materials/FloorView";
    public const string Kit = "Assets/Brick Project Studio/Apartment Kit/";
    public const string Pre = Kit + "_Prefabs/";
    public const string CorrTex = "Assets/Textures/Corridor/";
    public const string RootName = "FloorView";

    public const float M = 2.9f;          // module width (same as CorridorBuilder)
    public const int N = 15;
    public const float L = M * N;
    public const float RD = 4.5f;         // room depth
    public const float X0 = 1.2f;         // |x| of the outer face of the corridor walls
    public const float WH = 2.4f;         // wall height (no ceiling)

    public static Material wallMat, darkMat, floorWood, floorTile, birch, mattressMat, white;

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
        SaveAsPrefab();
        NavBuilder.AddRoomEntry();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        CaptureAll(Arg("-captureDir") ?? "Captures");
    }

    public static string Arg(string key)
    {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
        return null;
    }

    public static void SaveAsPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var root = GameObject.Find(RootName);
        bool ok;
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction, out ok);
        if (!ok) Debug.LogError("FloorViewBuilder: failed to save prefab " + PrefabPath);
    }

    public static void InitMats()
    {
        Directory.CreateDirectory(MatDir);
        wallMat = Flat("WallWhite", new Color(0.9f, 0.9f, 0.88f)); wallMat.SetFloat("_Smoothness", 0.1f);
        darkMat = Flat("MassDark", new Color(0.24f, 0.25f, 0.27f)); darkMat.SetFloat("_Smoothness", 0.2f);
        floorWood = TexMat("FloorWood", Kit + "Common/Textures/Surface/White Wood.png", new Color(0.62f, 0.6f, 0.56f), 0.3f);
        floorTile = Flat("BathTile", new Color(0.55f, 0.57f, 0.58f)); floorTile.SetFloat("_Smoothness", 0.5f);
        birch = TexMat("Birch", Kit + "Common/Textures/Surface/White Wood.png", new Color(0.9f, 0.8f, 0.66f), 0.3f);
        mattressMat = TexMat("MattressTaupe", Kit + "Common/Textures/Surface/Fabric_01_A.png", new Color(0.62f, 0.58f, 0.58f), 0.1f);
        white = Flat("White", new Color(0.93f, 0.93f, 0.92f)); white.SetFloat("_Smoothness", 0.6f);
    }

    // ---------------------------------------------------------------- build

    public static void Build()
    {
        Directory.CreateDirectory(MatDir);
        GameObject old;
        while ((old = GameObject.Find(RootName)) != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;

        InitMats();
        var ground = Flat("Ground", new Color(0.12f, 0.13f, 0.15f));

        TBox("Ground", root, new Vector3(0, -0.3f, L / 2), new Vector3(26f, 0.1f, L + 10f), ground, 4f);

        // ---- corridor (ceiling off)
        var corrPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Corridor.prefab");
        var corr = (GameObject)PrefabUtility.InstantiatePrefab(corrPrefab, root);
        corr.name = "Corridor";
        var ceil = corr.transform.Find("Shell/Ceiling"); if (ceil != null) ceil.GetComponent<Renderer>().enabled = false;
        var backing = corr.transform.Find("Doors/Lounge Entrance/Backing"); if (backing != null) backing.gameObject.SetActive(false);
        HideFixtures(corr.transform);
        // drop the dark backdrop slabs and exit signs that belong to the corridor's end walls
        foreach (var t in corr.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Exit Sign") || t.name == "Outside") t.gameObject.SetActive(false);

        // ---- partitions between rooms + outer walls
        var walls = Group("Walls", root);
        foreach (int side in new[] { -1, 1 })
        {
            // left row: the shaft / E/V / E/V hall / stair blocks are narrower than a room module,
            // so their dividing walls follow the block edges instead of the 2.9 m grid
            var zs = new List<float>();
            for (int k = 0; k <= N; k++) { if (side < 0 && k >= 13 && k <= 14) continue; zs.Add(k * M); }
            if (side < 0) { float zb0 = 12f * M; zs.Add(zb0 + 0.65f * M); zs.Add(zb0 + 1.4f * M); zs.Add(zb0 + 2.2f * M); }
            foreach (float zz in zs)
                TBox("Partition", walls, new Vector3(side * (X0 + (RD + 0.1f) / 2), WH / 2, zz), new Vector3(RD + 0.1f, WH, 0.1f), wallMat, 1f);
            TBox("Outer Wall", walls, new Vector3(side * (X0 + RD + 0.05f), WH / 2, L / 2), new Vector3(0.1f, WH, L + 0.1f), wallMat, 1f);
        }

        // ---- left row (plan top): stair block, 11 rooms, shaft / E/V / E/V hall / stair
        var left = Group("Left Row", root);
        Mass(left, "Stair Block A", 0f, M, "stair");
        for (int i = 1; i <= 11; i++) BuildRoom(left, -1, (i + 0.5f) * M, "A" + (226 - i));
        float z0 = 12f * M;
        Mass(left, "Shaft", z0, z0 + 0.65f * M, "shaft");
        Mass(left, "EV", z0 + 0.65f * M, z0 + 1.4f * M, "ev");
        Mass(left, "EV Hall", z0 + 1.4f * M, z0 + 2.2f * M, "evhall");
        Mass(left, "Stair Block B", z0 + 2.2f * M, z0 + 3.0f * M, "stair");

        // ---- right row (plan bottom): 14 rooms, lounge
        var right = Group("Right Row", root);
        for (int i = 0; i <= 13; i++) BuildRoom(right, +1, (i + 0.5f) * M, "A" + (201 + i));
        var loungePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RestLounge.prefab");
        var lounge = (GameObject)PrefabUtility.InstantiatePrefab(loungePrefab, right);
        lounge.name = "Rest Lounge";
        lounge.transform.localScale = Vector3.one * 0.8f;
        lounge.transform.rotation = Quaternion.Euler(0, 90f, 0);
        lounge.transform.position = new Vector3(X0 - 0.05f, 0f, 14.5f * M);
        var lc = lounge.transform.Find("Shell/Ceiling"); if (lc != null) lc.GetComponent<Renderer>().enabled = false;
        var sf = lounge.transform.Find("Storefront"); if (sf != null) sf.gameObject.SetActive(false);
        var os = lounge.transform.Find("Outside"); if (os != null) os.gameObject.SetActive(false);
        HideFixtures(lounge.transform);
        foreach (var l in lounge.GetComponentsInChildren<Light>(true)) l.intensity *= 0.5f; // lounge was blown out in the plan view

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
            c.orthographic = false; c.fieldOfView = 32f; c.nearClipPlane = 0.1f; c.farClipPlane = 150f;
            c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.11f, 0.13f);
            var ctrl = cam.GetComponent<FloorViewCamera>();
            if (ctrl == null) ctrl = cam.AddComponent<FloorViewCamera>();
            ctrl.target = new Vector3(0f, 0f, L / 2f); ctrl.tilt = 20f; ctrl.yaw = 0f; ctrl.distance = 38f;
            ctrl.SnapToGoal();
        }
    }

    // ceiling fixtures would float in mid-air once the ceiling is gone (the point lights stay)
    public static void HideFixtures(Transform t)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true))
            if (c.name == "Downlight" || c.name == "Speaker" || c.name == "Smoke Detector") c.gameObject.SetActive(false);
    }

    // dark solid block (stairs / elevator) with a label on top
    public static void Mass(Transform parent, string name, float za, float zb, string signKey, int side = -1, float labelYaw = -90f)
    {
        var g = Group(name, parent);
        float zc = (za + zb) / 2f;
        TBox("Block", g, new Vector3(side * (X0 + RD / 2), WH / 2, zc), new Vector3(RD, WH, zb - za - 0.1f), darkMat, 1f);
        if (signKey != null)
            Quad("Label", g, new Vector3(side * (X0 + 1.2f), WH + 0.02f, zc), new Vector3(90f, labelYaw, 0f), new Vector2(Mathf.Min(1.5f, zb - za - 0.3f), 0.5f), SignMat(signKey));
    }

    // one dorm room; side = -1 (left row) or +1 (right row); local frame: +x away from the corridor, +z along the corridor (mirrored on the left)
    public static void BuildRoom(Transform parent, int side, float zc, string name)
    {
        var g = Group("Room " + name, parent);
        g.localPosition = new Vector3(side * X0, 0f, zc);
        g.localRotation = Quaternion.Euler(0, side > 0 ? 0f : 180f, 0);

        TBox("Floor", g, new Vector3(RD / 2, -0.02f, 0), new Vector3(RD, 0.04f, M - 0.1f), floorWood, 1.6f);
        // toilet nook near the entrance (v in [-1.4,-0.55], u in [0,1.9])
        TBox("Bath Floor", g, new Vector3(0.95f, 0.003f, -0.975f), new Vector3(1.88f, 0.044f, 0.83f), floorTile, 1f);
        TBox("Bath Wall Side", g, new Vector3(0.95f, WH / 2, -0.55f), new Vector3(1.9f, WH, 0.08f), wallMat, 1f);
        TBox("Bath Wall End", g, new Vector3(1.9f, WH / 2, -1.325f), new Vector3(0.08f, WH, 0.15f), wallMat, 1f);
        // pedestal sink
        Cyl("Sink Pedestal", g, new Vector3(1.45f, 0.35f, -1.2f), 0.06f, 0.7f, white);
        TBox("Sink Basin", g, new Vector3(1.45f, 0.78f, -1.2f), new Vector3(0.42f, 0.12f, 0.34f), white, 1f);
        TBox("Shower Drain", g, new Vector3(1.0f, 0.03f, -0.75f), new Vector3(0.12f, 0.01f, 0.12f), Flat("Drain", new Color(0.35f, 0.36f, 0.38f)), 1f);

        var toilet = PlaceLocal(g, Pre + "Furniture/Bathroom/Toilet_Apt_01.prefab", "Toilet", 0.6f, 0f, new Vector2(0.359f, -0.975f), true);
        var ward = PlaceLocal(g, Pre + "Furniture/Bedroom/Dresser Closet_01.prefab", "Wardrobe", 0.48f, 90f, new Vector2(0.7f, 1.09f), true);
        var bed = PlaceLocal(g, Pre + "Furniture/Bedroom/Bed_Apt_01_02.prefab", "Bed", 0.6f, 180f, new Vector2(RD - 1.002f, 0.898f), true);
        Recolor(bed, birch);
        if (bed != null)
            foreach (var r in bed.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                    if (ms[i] != null && ms[i].name.StartsWith("Fabric") && !r.name.StartsWith("Pillow")) ms[i] = mattressMat;
                r.sharedMaterials = ms;
            }
        var desk = PlaceLocal(g, Pre + "Furniture/Living Room/Table_Computer_01_Setup.prefab", "Desk Set", 0.6f, -90f, new Vector2(3.55f, -0.8f), true);
        DeskCleanup.RemoveComputer(desk);
        Recolor(desk, birch);
    }

    // places a kit prefab so that its bounds centre sits at local (u, v) of the room group
    public static GameObject PlaceLocal(Transform g, string path, string name, float scale, float localYaw, Vector2 uv, bool snap)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning("missing " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
        go.name = name;
        go.transform.localScale = Vector3.one * scale;
        go.transform.localRotation = Quaternion.Euler(0, localYaw, 0);
        go.transform.localPosition = Vector3.zero;
        var b = Bounds(go);
        var target = g.TransformPoint(new Vector3(uv.x, 0, uv.y));
        go.transform.position += new Vector3(target.x - b.center.x, snap ? -b.min.y : 0f, target.z - b.center.z);
        return go;
    }

    public static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    public static void Recolor(GameObject go, Material birch)
    {
        if (go == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i] == null) continue;
                var n = ms[i].name;
                if (n.Contains("Ebony") || n.Contains("Dark Wood") || n.Contains("Rustic") || n.Contains("Cabinets_0") || n.Contains("Wood"))
                { ms[i] = birch; changed = true; }
            }
            if (changed) r.sharedMaterials = ms;
        }
    }

    // -------------------------------------------------------------- helpers

    public static Material SignMat(string key) { return TexMat("Sign_" + key, CorrTex + "Sign_" + key + ".png", Color.white, 0.3f); }

    public static Transform Group(string name, Transform parent)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    public static GameObject TBox(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, float tile)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.AddComponent<MeshFilter>().sharedMesh = MeshStore.Box(size, tile);
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    public static Mesh BoxMesh(Vector3 s, float tile)
    {
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var nrm = new List<Vector3>(); var tris = new List<int>();
        void Face(Vector3 n, Vector3 u, Vector3 w)
        {
            float ul = Mathf.Abs(Vector3.Dot(u, s)), wl = Mathf.Abs(Vector3.Dot(w, s));
            Vector3 c = Vector3.Scale(n, s * 0.5f), a = u * ul * 0.5f, b = w * wl * 0.5f;
            int i = verts.Count;
            verts.Add(c - a - b); verts.Add(c + a - b); verts.Add(c + a + b); verts.Add(c - a + b);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(ul / tile, 0)); uvs.Add(new Vector2(ul / tile, wl / tile)); uvs.Add(new Vector2(0, wl / tile));
            for (int k = 0; k < 4; k++) nrm.Add(n);
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        Face(Vector3.right, Vector3.up, Vector3.forward);
        Face(Vector3.left, Vector3.forward, Vector3.up);
        Face(Vector3.up, Vector3.forward, Vector3.right);
        Face(Vector3.down, Vector3.right, Vector3.forward);
        Face(Vector3.forward, Vector3.right, Vector3.up);
        Face(Vector3.back, Vector3.up, Vector3.right);
        var m = new Mesh { name = "TBox" };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetNormals(nrm); m.SetTriangles(tris, 0);
        m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }

    public static GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    public static GameObject Quad(string name, Transform parent, Vector3 pos, Vector3 euler, Vector2 size, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localEulerAngles = euler;
        g.transform.localScale = new Vector3(size.x, size.y, 1f);
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    public static Material TexMat(string name, string albedo, Color tint, float smooth)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Material Flat(string name, Color c)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.3f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // -------------------------------------------------------------- capture

    public static void CaptureAll(string dir)
    {
        Directory.CreateDirectory(dir);
        var main = GameObject.Find("Main Camera");
        if (main != null)
        {
            var fc = main.GetComponent<FloorViewCamera>();
            fc.enabled = false;
            foreach (float t in new[] { 0f, 20f, 40f })
            {
                fc.tilt = t; fc.distance = 38f + t * 0.2f;
                fc.SnapToGoal();
                Shoot("1_tilt" + (int)t, main.GetComponent<Camera>(), 2200, 820, dir);
            }
            fc.tilt = 20f; fc.distance = 38f;
            fc.SnapToGoal();
            fc.enabled = true;
        }

        var east = TempCam(new Vector3(0f, 40f, 37.5f), Quaternion.Euler(90f, -90f, 0f), 50f);
        east.orthographic = true; east.orthographicSize = 6f;
        Shoot("2_top_right_end", east, 1400, 800, dir);

        var west = TempCam(new Vector3(0f, 40f, 8f), Quaternion.Euler(90f, -90f, 0f), 50f);
        west.orthographic = true; west.orthographicSize = 6f;
        Shoot("3_top_left_end", west, 1400, 800, dir);

        var blk = TempCam(new Vector3(-8f, 22f, 12f * M + 1.5f * M), Quaternion.identity, 40f);
        blk.transform.LookAt(new Vector3(-3.6f, 0f, 12f * M + 1.5f * M), new Vector3(-1f, 0f, 0f));
        Shoot("5_blocks", blk, 1200, 800, dir);

        var ang = TempCam(new Vector3(-17f, 25f, -7f), Quaternion.identity, 55f);
        ang.transform.LookAt(new Vector3(0f, 0f, 20f));
        Shoot("4_angled", ang, 1600, 900, dir);

        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c.name == "_CaptureCam") Object.DestroyImmediate(c.gameObject);
    }

    public static Camera TempCam(Vector3 pos, Quaternion rot, float fov)
    {
        var g = new GameObject("_CaptureCam");
        g.transform.SetPositionAndRotation(pos, rot);
        var c = g.AddComponent<Camera>();
        c.fieldOfView = fov; c.nearClipPlane = 0.1f; c.farClipPlane = 120f;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.11f, 0.13f);
        g.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        return c;
    }

    public static void Shoot(string name, Camera cam, int w, int h, string dir)
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
