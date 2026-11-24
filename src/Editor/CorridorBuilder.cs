using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Dorm corridor (복도) from the photo + floor plan.
// Plan x -> world z (0..L). Plan top row = left wall (x = -CW/2), bottom row = right wall (x = +CW/2).
//   left wall  : 계단실 | A225 .. A215 (11 rooms) | 샤프트 | E/V | E/V홀 | 계단실
//   right wall : A201 .. A214 (14 rooms) | 휴게실 (glass)
//   left end   : black grid window + railing + exit sign      right end : glass door to 남자기숙
// Root "Corridor" -> Assets/Scenes/Corridor.unity and Assets/Prefabs/Corridor.prefab.
// Run: Unity -batchmode -projectPath <proj> -executeMethod CorridorBuilder.BuildAndCapture -captureDir <dir>
public static class CorridorBuilder
{
    public const string ScenePath = "Assets/Scenes/Corridor.unity";
    public const string PrefabPath = "Assets/Prefabs/Corridor.prefab";
    public const string MatDir = "Assets/Materials/Corridor";
    public const string TexDir = "Assets/Textures/Corridor/";
    public const string RootName = "Corridor";

    public const float M = 2.9f;          // module (one room width)
    public const int N = 15;              // modules
    public const float L = M * N;         // corridor length
    public const float CW = 2.2f;         // corridor width
    public const float H = 2.45f;         // ceiling height

    public static Material steel, doorDark, doorLight, black, glassClear, glassDark, glow, white, keypad;

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
        RoomLookCamera.AttachTo(GameObject.Find("Main Camera"));
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
        if (!ok) Debug.LogError("CorridorBuilder: failed to save prefab " + PrefabPath);
    }

    public static Material wall, floorC, floorS, ceil, baseboard, night, backing, speaker, exitGlow;

    public static void InitMaterials()
    {
        Directory.CreateDirectory(MatDir);
        wall = TexMat("WallPanel", TexDir + "WallPanel.png", Color.white, 0.25f);
        floorC = TexMat("FloorCenter", TexDir + "FloorCenter.png", Color.white, 0.35f);
        floorS = Flat("FloorSide", new Color(0.36f, 0.35f, 0.37f)); floorS.SetFloat("_Smoothness", 0.35f);
        ceil = TexMat("CeilingTile", TexDir + "CeilingTile.png", Color.white, 0.05f);
        baseboard = Flat("Baseboard", new Color(0.5f, 0.51f, 0.52f));
        steel = Flat("Steel", new Color(0.7f, 0.71f, 0.73f)); steel.SetFloat("_Metallic", 0.85f); steel.SetFloat("_Smoothness", 0.6f);
        doorDark = Flat("DoorDark", new Color(0.3f, 0.31f, 0.33f)); doorDark.SetFloat("_Metallic", 0.4f); doorDark.SetFloat("_Smoothness", 0.6f);
        doorLight = Flat("DoorLight", new Color(0.78f, 0.79f, 0.8f)); doorLight.SetFloat("_Metallic", 0.2f); doorLight.SetFloat("_Smoothness", 0.5f);
        black = Flat("Black", new Color(0.05f, 0.05f, 0.06f)); black.SetFloat("_Smoothness", 0.35f);
        keypad = Flat("Keypad", new Color(0.1f, 0.1f, 0.11f)); keypad.SetFloat("_Metallic", 0.5f); keypad.SetFloat("_Smoothness", 0.7f);
        glassClear = GlassMat("GlassClear", new Color(0.82f, 0.92f, 0.95f, 0.1f));
        glassDark = GlassMat("GlassDark", new Color(0.04f, 0.06f, 0.08f, 0.6f));
        night = Flat("Outside", new Color(0.1f, 0.12f, 0.15f), true);
        backing = Flat("RoomBacking", new Color(0.12f, 0.12f, 0.13f), true);
        white = Flat("White", new Color(0.93f, 0.93f, 0.92f));
        glow = Flat("LightGlow", new Color(1f, 1f, 0.95f)); glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(2.4f, 2.4f, 2.2f));
        speaker = Flat("Speaker", new Color(0.82f, 0.82f, 0.8f));
        exitGlow = TexMat("Sign_exit_lit", TexDir + "Sign_exit.png", Color.white, 0.3f);
        exitGlow.EnableKeyword("_EMISSION"); exitGlow.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "Sign_exit.png")); exitGlow.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.9f));
    }

    // ---------------------------------------------------------------- build

    public static void Build()
    {
        Directory.CreateDirectory(MatDir);
        GameObject old;
        while ((old = GameObject.Find(RootName)) != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;

        InitMaterials();

        // ---- shell
        var shell = Group("Shell", root);
        float hw = CW / 2f;
        TBox("Floor Center", shell, new Vector3(0, -0.05f, L / 2), new Vector3(1.1f, 0.1f, L), floorC, 1.2f, true);
        TBox("Floor Side L", shell, new Vector3(-hw + 0.275f, -0.05f, L / 2), new Vector3(0.55f, 0.1f, L), floorS, 1f);
        TBox("Floor Side R", shell, new Vector3(hw - 0.275f, -0.05f, L / 2), new Vector3(0.55f, 0.1f, L), floorS, 1f);
        TBox("Ceiling", shell, new Vector3(0, H + 0.05f, L / 2), new Vector3(CW + 0.3f, 0.1f, L), ceil, 1.2f);
        TBox("Wall Left", shell, new Vector3(-hw - 0.05f, H / 2, L / 2), new Vector3(0.1f, H, L), wall, 1.2f, true);
        TBox("Wall Right", shell, new Vector3(hw + 0.05f, H / 2, L / 2), new Vector3(0.1f, H, L), wall, 1.2f, true);
        TBox("Baseboard Left", shell, new Vector3(-hw + 0.012f, 0.05f, L / 2), new Vector3(0.024f, 0.1f, L), baseboard, 1f);
        TBox("Baseboard Right", shell, new Vector3(hw - 0.012f, 0.05f, L / 2), new Vector3(0.024f, 0.1f, L), baseboard, 1f);

        // ---- left end: black grid window + railing, exit sign
        var endL = RailEnd("End Window", shell, new Vector3(0f, 0, -0.02f));
        var outside = Group("Outside", root);
        Quad("Night", outside, new Vector3(0, 1.2f, -1.4f), 180f, new Vector2(7f, 3f), night);
        ExitSign(shell, new Vector3(0f, H - 0.17f, 2.4f), 0f, exitGlow);
        TBox("Exit Sign Hanger", shell, new Vector3(0f, H - 0.04f, 2.4f), new Vector3(0.03f, 0.08f, 0.03f), steel, 1f);

        // ---- right end: glass door to the men's dorm
        var endR = Group("End Glass Door", shell);
        TBox("Post L", endR, new Vector3(-0.6f, (H - 0.01f) / 2, L + 0.03f), new Vector3(0.07f, H - 0.01f, 0.1f), steel, 1f);
        TBox("Post R", endR, new Vector3(0.6f, (H - 0.01f) / 2, L + 0.03f), new Vector3(0.07f, H - 0.01f, 0.1f), steel, 1f);
        TBox("Header", endR, new Vector3(0, 2.2f, L + 0.03f), new Vector3(CW, 0.5f, 0.1f), wall, 1.2f);
        TBox("Glass Door", endR, new Vector3(0, 1.0f, L + 0.03f), new Vector3(1.14f, 2.0f, 0.014f), glassClear, 1f);
        TBox("Glass Side L", endR, new Vector3(-0.85f, 1.0f, L + 0.03f), new Vector3(0.5f, 2.0f, 0.014f), glassClear, 1f);
        TBox("Glass Side R", endR, new Vector3(0.85f, 1.0f, L + 0.03f), new Vector3(0.5f, 2.0f, 0.014f), glassClear, 1f);
        Cyl("Handle A", endR, new Vector3(0.4f, 1.0f, L - 0.04f), 0.014f, 0.6f, steel);
        Cyl("Handle B", endR, new Vector3(0.28f, 1.0f, L - 0.04f), 0.014f, 0.6f, steel);
        Quad("Sign Men", endR, new Vector3(0f, 2.22f, L - 0.025f), 180f, new Vector2(0.9f, 0.225f), SignMat("men"));
        Quad("Night Right", outside, new Vector3(0, 1.2f, L + 1.6f), 180f, new Vector2(7f, 3f), backing);
        ExitSign(shell, new Vector3(0f, H - 0.17f, L - 2.2f), 180f, exitGlow);
        TBox("Exit Sign Hanger R", shell, new Vector3(0f, H - 0.04f, L - 2.2f), new Vector3(0.03f, 0.08f, 0.03f), steel, 1f);

        // ---- doors
        var doors = Group("Doors", root);
        // left wall (plan top row)
        StairDoor(doors, -1, 0.5f * M, "stair");
        for (int i = 1; i <= 11; i++)
        {
            float zc = (i + 0.5f) * M;
            RoomDoor(doors, -1, zc, "A" + (226 - i), i);
            if (i % 2 == 0) UtilityDoor(doors, -1, zc + 1.05f);
        }
        float z0 = 12f * M;
        PanelDoor(doors, -1, z0 + 0.325f * M, 0.55f, 1.9f, doorLight, "Shaft Door");
        Plate(doors, -1, z0 + 0.325f * M, 2.05f, "shaft", 0.45f, 0.15f);
        EvDoor(doors, -1, z0 + (0.65f + 0.375f) * M);
        Plate(doors, -1, z0 + (0.65f + 0.375f) * M, 2.2f, "ev", 0.45f, 0.15f);
        PanelDoor(doors, -1, z0 + (1.4f + 0.4f) * M, 1.0f, 2.1f, doorDark, "EV Hall Door");
        Plate(doors, -1, z0 + (1.4f + 0.4f) * M, 2.2f, "evhall", 0.45f, 0.15f);
        StairDoor(doors, -1, z0 + (2.2f + 0.4f) * M, "stair");

        // right wall (plan bottom row)
        for (int i = 0; i <= 13; i++)
        {
            float zc = (i + 0.5f) * M;
            RoomDoor(doors, +1, zc, "A" + (201 + i), i);
            if (i % 2 == 1) UtilityDoor(doors, +1, zc + 1.05f);
        }
        Lounge(doors, 14.5f * M, backing);

        // ---- ceiling fixtures
        var lights = Group("Lights", root);
        foreach (int k in new[] { 1, 4, 7, 10, 13 })
        {
            float z = (k + 0.5f) * M;
            Cyl("Downlight", lights, new Vector3(0, H - 0.004f, z), 0.12f, 0.01f, glow);
            AddLight("Light " + k, lights, new Vector3(0, H - 0.3f, z), new Color(0.97f, 0.98f, 1f), 2.0f, 7.5f, k == 1 || k == 7 || k == 13 ? LightShadows.Soft : LightShadows.None);
        }
        foreach (int k in new[] { 2, 5, 8, 11 })
        {
            float z = (k + 0.2f) * M;
            Cyl("Speaker", lights, new Vector3(0, H - 0.003f, z), 0.1f, 0.01f, speaker);
            Cyl("Smoke Detector", lights, new Vector3(0.45f, H - 0.012f, z + 1.4f), 0.045f, 0.024f, white);
        }

        var sun = GameObject.Find("Directional Light");
        if (sun != null) { var l = sun.GetComponent<Light>(); l.intensity = 0.05f; l.shadows = LightShadows.None; }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.35f);

        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            cam.transform.SetPositionAndRotation(new Vector3(0.1f, 1.5f, 3.2f), Quaternion.identity);
            cam.transform.LookAt(new Vector3(0f, 1.3f, 22f));
            var c = cam.GetComponent<Camera>();
            c.fieldOfView = 70f; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
        }
    }

    // ---------------------------------------------------------------- doors

    public static void RoomDoor(Transform parent, int side, float zc, string name, int idx)
    {
        PanelDoor(parent, side, zc, 0.92f, 2.1f, doorDark, "Door " + name);
        float hs = (idx % 2 == 0) ? 1f : -1f;
        float xs = side * (CW / 2f - 0.03f);
        float xh = xs - side * 0.05f;
        // lever handle + keypad lock above it
        TBox("Handle Plate", parent, new Vector3(xh, 1.0f, zc + 0.33f * hs), new Vector3(0.025f, 0.2f, 0.05f), steel, 1f);
        TBox("Handle Lever", parent, new Vector3(xh - side * 0.02f, 1.0f, zc + 0.33f * hs - 0.08f * hs), new Vector3(0.03f, 0.022f, 0.14f), steel, 1f);
        TBox("Keypad", parent, new Vector3(xh, 1.24f, zc + 0.33f * hs), new Vector3(0.022f, 0.16f, 0.06f), keypad, 1f);
        Plate(parent, side, zc + 0.3f * hs, 1.72f, name, 0.16f, 0.08f);
    }

    public static void StairDoor(Transform parent, int side, float zc, string key)
    {
        PanelDoor(parent, side, zc, 0.95f, 2.1f, doorDark, "Stair Door");
        float xh = side * (CW / 2f - 0.08f);
        TBox("Stair Handle", parent, new Vector3(xh, 1.0f, zc + 0.34f), new Vector3(0.03f, 0.03f, 0.16f), steel, 1f);
        Plate(parent, side, zc, 2.22f, key, 0.45f, 0.15f);
    }

    public static void UtilityDoor(Transform parent, int side, float zc)
    {
        PanelDoor(parent, side, zc, 0.45f, 1.85f, doorLight, "Utility Door");
        float xh = side * (CW / 2f - 0.07f);
        TBox("Utility Knob", parent, new Vector3(xh, 0.95f, zc + 0.15f), new Vector3(0.03f, 0.05f, 0.05f), steel, 1f);
    }

    public static void EvDoor(Transform parent, int side, float zc)
    {
        PanelDoor(parent, side, zc, 1.25f, 2.1f, steel, "EV Door");
        float xs = side * (CW / 2f - 0.05f);
        TBox("EV Seam", parent, new Vector3(xs - side * 0.008f, 1.05f, zc), new Vector3(0.012f, 2.08f, 0.01f), black, 1f);
        TBox("EV Button", parent, new Vector3(xs, 1.1f, zc + 0.78f), new Vector3(0.02f, 0.14f, 0.06f), black, 1f);
    }

    public static void PanelDoor(Transform parent, int side, float zc, float w, float h, Material slab, string name)
    {
        float xs = side * (CW / 2f - 0.03f);
        TBox(name, parent, new Vector3(xs, h / 2f, zc), new Vector3(0.05f, h, w), slab, 1f);
        float xf = xs - side * 0.012f;
        TBox("Frame Top", parent, new Vector3(xf, h + 0.03f, zc), new Vector3(0.08f, 0.06f, w + 0.12f), steel, 1f);
        TBox("Frame L", parent, new Vector3(xf, h / 2f + 0.01f, zc - w / 2 - 0.03f), new Vector3(0.08f, h + 0.02f, 0.06f), steel, 1f);
        TBox("Frame R", parent, new Vector3(xf, h / 2f + 0.01f, zc + w / 2 + 0.03f), new Vector3(0.08f, h + 0.02f, 0.06f), steel, 1f);
    }

    // sign plate on the corridor wall (faces the corridor)
    public static void Plate(Transform parent, int side, float zc, float y, string key, float w, float h)
    {
        float x = side * (CW / 2f - 0.058f);
        Quad("Sign " + key, parent, new Vector3(x, y, zc), side < 0 ? 270f : 90f, new Vector2(w, h), SignMat(key));
    }

    public static void Lounge(Transform parent, float zc, Material backing)
    {
        var g = Group("Lounge Entrance", parent);
        float x = CW / 2f - 0.03f;
        TBox("Glass", g, new Vector3(x, 1.0f, zc), new Vector3(0.014f, 2.0f, M - 0.1f), glassClear, 1f);
        foreach (float dz in new[] { -M / 2 + 0.05f, -0.5f, 0.5f, M / 2 - 0.05f })
            TBox("Post", g, new Vector3(x - 0.01f, H / 2, zc + dz), new Vector3(0.08f, H, 0.07f), steel, 1f);
        TBox("Header", g, new Vector3(x - 0.01f, 2.28f, zc), new Vector3(0.1f, 0.34f, M), steel, 1f);
        TBox("Kick", g, new Vector3(x - 0.01f, 0.06f, zc), new Vector3(0.1f, 0.12f, M), steel, 1f);
        Cyl("Handle A", g, new Vector3(x - 0.06f, 1.0f, zc + 0.42f), 0.014f, 0.6f, steel);
        Cyl("Handle B", g, new Vector3(x - 0.06f, 1.0f, zc + 0.3f), 0.014f, 0.6f, steel);
        Quad("Backing", g, new Vector3(x + 0.5f, 1.2f, zc), 90f, new Vector2(M, 2.4f), backing);
        Plate(g, +1, zc, 2.0f, "lounge", 0.55f, 0.18f);
    }

    // black grid window + railing across the corridor end (z = pos.z), running along x
    public static Transform RailEnd(string name, Transform parent, Vector3 pos)
    {
        var g = Group(name, parent);
        g.localPosition = pos;
        float len = CW;
        float x0 = 0f;
        TBox("Curb", g, new Vector3(x0, 0.06f, 0), new Vector3(len, 0.12f, 0.1f), black, 1f);
        int bars = Mathf.FloorToInt(len / 0.11f);
        for (int i = 0; i <= bars; i++)
            TBox("Bar", g, new Vector3(-len / 2 + i * (len / bars), 0.57f, 0), new Vector3(0.018f, 0.9f, 0.018f), black, 1f);
        TBox("Rail Top", g, new Vector3(x0, 1.03f, 0), new Vector3(len, 0.045f, 0.06f), black, 1f);
        TBox("Sill", g, new Vector3(x0, 1.1f, 0), new Vector3(len, 0.08f, 0.12f), black, 1f);
        TBox("Head", g, new Vector3(x0, H - 0.05f, 0), new Vector3(len, 0.1f, 0.12f), black, 1f);
        TBox("Mid Rail", g, new Vector3(x0, 1.75f, 0), new Vector3(len, 0.05f, 0.08f), black, 1f);
        foreach (float mx in new[] { -len / 2, -len / 6, len / 6, len / 2 })
            TBox("Mullion", g, new Vector3(mx, (1.14f + H - 0.1f) / 2f, 0), new Vector3(0.06f, H - 1.24f, 0.1f), black, 1f);
        TBox("Window Glass", g, new Vector3(x0, (1.14f + H - 0.1f) / 2f, 0), new Vector3(len, H - 1.24f, 0.01f), glassDark, 1f);
        return g;
    }

    public static void ExitSign(Transform parent, Vector3 pos, float yaw, Material mat)
    {
        var g = Group("Exit Sign", parent);
        g.localPosition = pos;
        TBox("Box", g, new Vector3(0, 0, 0), new Vector3(0.4f, 0.16f, 0.06f), white, 1f);
        Quad("Face A", g, new Vector3(0, 0, -0.031f), 0f, new Vector2(0.38f, 0.14f), mat);
        Quad("Face B", g, new Vector3(0, 0, 0.031f), 180f, new Vector2(0.38f, 0.14f), mat);
    }

    // -------------------------------------------------------------- helpers

    public static Material SignMat(string key) { return TexMat("Sign_" + key, TexDir + "Sign_" + key + ".png", Color.white, 0.3f); }

    public static Transform Group(string name, Transform parent)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    public static GameObject TBox(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, float tile, bool swapUV = false)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.AddComponent<MeshFilter>().sharedMesh = MeshStore.Box(size, tile, swapUV);
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    public static Mesh BoxMesh(Vector3 s, float tile, bool swapUV)
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
        if (swapUV) for (int k = 0; k < uvs.Count; k++) uvs[k] = new Vector2(uvs[k].y, uvs[k].x);
        var m = new Mesh { name = "TBox" };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetNormals(nrm); m.SetTriangles(tris, 0);
        m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }

    public static GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material mat, Vector3? euler = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
        if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    // yaw: 0 = faces -z, 180 = faces +z, 90 = faces -x, 270 = faces +x
    public static GameObject Quad(string name, Transform parent, Vector3 pos, float yaw, Vector2 size, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localEulerAngles = new Vector3(0, yaw, 0);
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

    public static Material Flat(string name, Color c, bool unlit = false)
    {
        string path = MatDir + "/" + name + ".mat";
        var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", c);
        if (!unlit) m.SetFloat("_Smoothness", 0.3f);
        EditorUtility.SetDirty(m);
        return m;
    }

    public static Material GlassMat(string name, Color c)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.9f);
        EditorUtility.SetDirty(m);
        return m;
    }

    public static void AddLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range, LightShadows shadows)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = shadows;
    }

    // -------------------------------------------------------------- capture

    public static void CaptureAll(string dir)
    {
        Directory.CreateDirectory(dir);
        var main = GameObject.Find("Main Camera");
        if (main != null) Shoot("1_main", main.GetComponent<Camera>(), 700, 900, dir);

        var back = TempCam(new Vector3(-0.1f, 1.5f, L - 1.2f), Quaternion.identity, 70f);
        back.transform.LookAt(new Vector3(0f, 1.3f, 4f));
        Shoot("2_from_end", back, 700, 900, dir);

        var ev = TempCam(new Vector3(0.7f, 1.5f, 11.2f * M), Quaternion.identity, 74f);
        ev.transform.LookAt(new Vector3(-1.1f, 1.2f, 13.6f * M));
        Shoot("3_left_cluster", ev, 800, 600, dir);

        var lounge = TempCam(new Vector3(-0.7f, 1.5f, 13.4f * M), Quaternion.identity, 72f);
        lounge.transform.LookAt(new Vector3(1.1f, 1.2f, 14.5f * M));
        Shoot("4_lounge_door", lounge, 800, 600, dir);

        var endW = TempCam(new Vector3(0f, 1.5f, 6f), Quaternion.identity, 66f);
        endW.transform.LookAt(new Vector3(0f, 1.3f, 0f));
        Shoot("6_end_window", endW, 800, 700, dir);

        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c.name == "_CaptureCam") Object.DestroyImmediate(c.gameObject);
    }

    public static Camera TempCam(Vector3 pos, Quaternion rot, float fov)
    {
        var g = new GameObject("_CaptureCam");
        g.transform.SetPositionAndRotation(pos, rot);
        var c = g.AddComponent<Camera>();
        c.fieldOfView = fov; c.nearClipPlane = 0.03f; c.farClipPlane = 80f;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
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
