using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Bathroom rebuilt from the reference photos + sketch (top view):
//   back wall, left -> right :  [ shower stall | partition | pedestal sink | toilet ]
//   entrance door in the front wall (lower right of the sketch)
//   partition = tile wall at the back + glass screen at the front; the wooden shower door is
//   hinged at the front-left corner and opened inward along the left wall (photo 1).
// Everything sits under one "Bathroom" root in Assets/Scenes/Bathroom.unity.
// Run: Unity -batchmode -projectPath <proj> -executeMethod BathroomAssetBuilder.BuildAndCapture -captureDir <dir>
public static class BathroomAssetBuilder
{
    const string MatDir = "Assets/Materials/BathroomAsset";
    const string TexDir = "Assets/Textures/Bathroom/";
    const string Kit = "Assets/Brick Project Studio/Apartment Kit/";
    const string Pre = Kit + "_Prefabs/";

    // interior: x in [-W/2, W/2], z in [0 (entrance wall), D (back wall)], y in [0, H]
    const float W = 2.3f, D = 2.1f, H = 2.3f;
    const float PartX = -0.05f;          // partition between shower (x<PartX) and sink/toilet side
    const float GlassZ0 = 1.15f, GlassZ1 = 1.5f; // glass screen front part; tile wall from GlassZ1 to the back
    const float DoorX0 = 0.0f, DoorX1 = 0.8f;    // entrance opening

    static readonly StringBuilder Log = new StringBuilder();

    public static void BuildAndCapture()
    {
        // work scene only: used to build the prefab, never saved (no scene file is created)
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Build();
        PrefabUtility.SaveAsPrefabAssetAndConnect(GameObject.Find("Bathroom"), "Assets/Prefabs/Bathroom.prefab", InteractionMode.AutomatedAction, out _);
        AssetDatabase.SaveAssets();
        string dir = Arg("-captureDir") ?? "Captures";
        CaptureAll(dir);
        File.WriteAllText(Path.Combine(dir, "build_log.txt"), Log.ToString());
    }

    static string Arg(string key)
    {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
        return null;
    }

    // ---------------------------------------------------------------- build

    static void Build()
    {
        Directory.CreateDirectory(MatDir);
        GameObject old;
        while ((old = GameObject.Find("Bathroom")) != null) Object.DestroyImmediate(old);
        var root = new GameObject("Bathroom").transform;

        // ---- materials
        var wallTile = TexMat("WallTile", TexDir + "WallTile.png", null, Color.white, 0.55f, 0f);
        var floorTile = TexMat("FloorTile", TexDir + "FloorTile.png", null, Color.white, 0.4f, 0f);
        var ceil = Flat("Ceiling", new Color(0.72f, 0.73f, 0.74f), false);
        var hallWall = Flat("HallWall", new Color(0.82f, 0.8f, 0.76f), false);
        var hallFloor = Flat("HallFloor", new Color(0.8f, 0.8f, 0.79f), false);
        var wood = TexMat("DoorWood", Kit + "Common/Textures/Surface/White Wood.png", Kit + "Common/Textures/Surface/White Wood_normal.png", new Color(0.88f, 0.82f, 0.7f), 0.25f, 0.4f);
        var chrome = Flat("Chrome", new Color(0.78f, 0.79f, 0.82f), false); chrome.SetFloat("_Metallic", 0.9f); chrome.SetFloat("_Smoothness", 0.85f);
        var porcelain = Flat("Porcelain", new Color(0.95f, 0.95f, 0.94f), false); porcelain.SetFloat("_Smoothness", 0.6f);
        var glass = GlassMat("GlassClear");
        var mirror = Flat("Mirror", new Color(0.7f, 0.76f, 0.8f), false); mirror.SetFloat("_Metallic", 0.7f); mirror.SetFloat("_Smoothness", 0.9f);
        var cabinet = Flat("CabinetSilver", new Color(0.62f, 0.66f, 0.68f), false); cabinet.SetFloat("_Metallic", 0.6f); cabinet.SetFloat("_Smoothness", 0.8f);
        var glassShelf = Flat("GlassShelf", new Color(0.65f, 0.8f, 0.78f), false); glassShelf.SetFloat("_Smoothness", 0.8f);
        var black = Flat("Black", new Color(0.08f, 0.08f, 0.09f), false);
        var grate = Flat("Grate", new Color(0.5f, 0.52f, 0.55f), false); grate.SetFloat("_Metallic", 0.8f);
        var curb = Flat("Curb", new Color(0.85f, 0.8f, 0.7f), false);
        var mint = Flat("PoufMint", new Color(0.65f, 0.88f, 0.8f), false);
        var lime = Flat("PoufLime", new Color(0.75f, 0.86f, 0.3f), false);
        var towel = Flat("TowelOlive", new Color(0.7f, 0.72f, 0.48f), false);
        var bWhite = Flat("BottleWhite", new Color(0.93f, 0.93f, 0.9f), false);
        var bGreen = Flat("BottleGreen", new Color(0.15f, 0.3f, 0.22f), false);
        var bBlue = Flat("BottleBlue", new Color(0.2f, 0.45f, 0.85f), false);
        var bYellow = Flat("BottleYellow", new Color(0.9f, 0.78f, 0.35f), false);
        var lightGlow = Flat("LightGlow", new Color(0.97f, 0.97f, 0.95f), false);
        lightGlow.EnableKeyword("_EMISSION"); lightGlow.SetColor("_EmissionColor", new Color(1.3f, 1.3f, 1.25f));

        // ---- shell: tiled room + plain hallway side
        var shell = Group("Shell", root);
        float cz = D / 2f;
        TBox("Floor", shell, new Vector3(0, -0.05f, cz), new Vector3(W, 0.1f, D), floorTile, 1.2f);
        TBox("Ceiling", shell, new Vector3(0, H + 0.05f, cz), new Vector3(W + 0.2f, 0.1f, D + 0.2f), ceil, 1f);
        TBox("Wall Left", shell, new Vector3(-W / 2 - 0.05f, H / 2, cz), new Vector3(0.1f, H, D), wallTile, 1.2f, true);
        TBox("Wall Right", shell, new Vector3(W / 2 + 0.05f, H / 2, cz), new Vector3(0.1f, H, D), wallTile, 1.2f);
        TBox("Wall Back", shell, new Vector3(0, H / 2, D + 0.05f), new Vector3(W + 0.2f, H, 0.1f), wallTile, 1.2f);
        // front wall with the entrance opening x[DoorX0,DoorX1], height 2.05
        float fl = (-W / 2 - 0.1f), fr = (W / 2 + 0.1f);
        TBox("Wall Front L", shell, new Vector3((fl + DoorX0) / 2, H / 2, -0.05f), new Vector3(DoorX0 - fl, H, 0.1f), wallTile, 1.2f);
        TBox("Wall Front R", shell, new Vector3((DoorX1 + fr) / 2, H / 2, -0.05f), new Vector3(fr - DoorX1, H, 0.1f), wallTile, 1.2f);
        TBox("Wall Front Lintel", shell, new Vector3((DoorX0 + DoorX1) / 2, 2.175f, -0.05f), new Vector3(DoorX1 - DoorX0, H - 2.05f, 0.1f), wallTile, 1.2f);
        // hallway side: painted skin over the front wall, hallway floor, wood door frame
        TBox("Hall Skin L", shell, new Vector3((fl + DoorX0) / 2, H / 2, -0.12f), new Vector3(DoorX0 - fl, H, 0.04f), hallWall, 1f);
        TBox("Hall Skin R", shell, new Vector3((DoorX1 + fr) / 2, H / 2, -0.12f), new Vector3(fr - DoorX1, H, 0.04f), hallWall, 1f);
        TBox("Hall Skin Top", shell, new Vector3((DoorX0 + DoorX1) / 2, 2.175f, -0.12f), new Vector3(DoorX1 - DoorX0, H - 2.05f, 0.04f), hallWall, 1f);
        TBox("Hall Floor", shell, new Vector3(0.3f, -0.05f, -1.6f), new Vector3(4.0f, 0.1f, 3.0f), hallFloor, 1f);
        TBox("Hall Wall L", shell, new Vector3(-1.9f, H / 2, -1.6f), new Vector3(0.1f, H, 3.0f), hallWall, 1f);
        TBox("Hall Wall R", shell, new Vector3(2.3f, H / 2, -1.6f), new Vector3(0.1f, H, 3.0f), hallWall, 1f);
        TBox("Hall Wall Far", shell, new Vector3(0.2f, H / 2, -3.15f), new Vector3(4.4f, H, 0.1f), hallWall, 1f);
        TBox("Hall Ceiling", shell, new Vector3(0.3f, H + 0.05f, -1.6f), new Vector3(4.2f, 0.1f, 3.2f), ceil, 1f);
        TBox("Frame Top", shell, new Vector3((DoorX0 + DoorX1) / 2, 2.07f, -0.06f), new Vector3(DoorX1 - DoorX0 + 0.1f, 0.06f, 0.2f), wood, 1f);
        TBox("Frame L", shell, new Vector3(DoorX0 - 0.02f, 1.02f, -0.06f), new Vector3(0.07f, 2.04f, 0.2f), wood, 1f);
        TBox("Frame R", shell, new Vector3(DoorX1 + 0.02f, 1.02f, -0.06f), new Vector3(0.07f, 2.04f, 0.2f), wood, 1f);
        TBox("Threshold", shell, new Vector3((DoorX0 + DoorX1) / 2, 0.01f, -0.04f), new Vector3(DoorX1 - DoorX0, 0.02f, 0.14f), wood, 1f);

        // ---- entrance door: wooden slab swung open against the right wall, inside
        var door = Group("Entrance Door", root);
        // hinged at the right post of the frame, opened 90 degrees into the room (leaf along +z, 0.05 clear of the post)
        TBox("Slab", door, new Vector3(DoorX1 + 0.07f, 1.0f, 0.46f), new Vector3(0.04f, 2.0f, 0.78f), wood, 1f);
        TBox("Handle", door, new Vector3(DoorX1 + 0.04f, 1.0f, 0.78f), new Vector3(0.03f, 0.04f, 0.12f), chrome, 1f);

        // ---- partition: tile wall at the back + glass screen at the front
        var part = Group("Partition", root);
        TBox("Tile Wall", part, new Vector3(PartX, H / 2, (GlassZ1 + D) / 2), new Vector3(0.1f, H, D - GlassZ1), wallTile, 1.2f, true);
        TBox("Glass", part, new Vector3(PartX, 1.0f, (GlassZ0 + GlassZ1) / 2), new Vector3(0.012f, 1.96f, GlassZ1 - GlassZ0), glass, 1f);
        TBox("Frame Top", part, new Vector3(PartX, 1.97f, (GlassZ0 + GlassZ1) / 2), new Vector3(0.04f, 0.03f, GlassZ1 - GlassZ0), chrome, 1f);
        TBox("Frame Post", part, new Vector3(PartX, 1.0f, GlassZ0), new Vector3(0.04f, 2.0f, 0.04f), chrome, 1f);
        TBox("Frame Bottom", part, new Vector3(PartX, 0.02f, (GlassZ0 + GlassZ1) / 2), new Vector3(0.04f, 0.04f, GlassZ1 - GlassZ0), chrome, 1f);

        // ---- shower stall (left, back)
        var shower = Group("Shower", root);
        TBox("Curb", shower, new Vector3((-W / 2 + PartX) / 2, 0.012f, GlassZ0), new Vector3(PartX + W / 2, 0.024f, 0.06f), curb, 1f);
        var drain = TBox("Drain", shower, new Vector3(-0.6f, 0.004f, 1.7f), new Vector3(0.14f, 0.008f, 0.14f), grate, 1f);
        float bx = -0.34f, bz = D - 0.03f; // slide bar on the back wall near the partition
        Cyl("Slide Bar", shower, new Vector3(bx, 1.4f, bz), 0.012f, 1.1f, chrome);
        TBox("Bar Cap Top", shower, new Vector3(bx, 1.96f, bz - 0.01f), new Vector3(0.07f, 0.1f, 0.03f), chrome, 1f);
        TBox("Soap Dish", shower, new Vector3(bx - 0.05f, 1.27f, bz - 0.03f), new Vector3(0.13f, 0.015f, 0.08f), chrome, 1f);
        TBox("Hand Shower Holder", shower, new Vector3(bx, 1.12f, bz - 0.03f), new Vector3(0.05f, 0.05f, 0.05f), chrome, 1f);
        Sphere("Pouf Mint", shower, new Vector3(bx + 0.02f, 1.62f, bz - 0.07f), 0.1f, mint);
        Sphere("Pouf Lime", shower, new Vector3(bx - 0.03f, 1.1f, bz - 0.07f), 0.09f, lime);
        // corner shelf (back wall / partition wall corner)
        var shelf = TBox("Corner Shelf", shower, new Vector3(PartX - 0.12f, 0.8f, D - 0.12f), new Vector3(0.3f, 0.02f, 0.14f), chrome, 1f);
        shelf.transform.localEulerAngles = new Vector3(0, 45f, 0);
        Cyl("Bottle White", shower, new Vector3(PartX - 0.11f, 0.9f, D - 0.1f), 0.035f, 0.16f, bWhite);
        Cyl("Bottle Green", shower, new Vector3(PartX - 0.18f, 0.9f, D - 0.07f), 0.03f, 0.15f, bGreen);
        TBox("Mixer Body", shower, new Vector3(bx - 0.14f, 0.62f, bz), new Vector3(0.1f, 0.1f, 0.06f), chrome, 1f);
        var mh = TBox("Mixer Handle", shower, new Vector3(bx - 0.14f, 0.67f, bz - 0.05f), new Vector3(0.1f, 0.022f, 0.022f), chrome, 1f);
        mh.transform.localEulerAngles = new Vector3(0, 0, 25f);
        PlaceKit(Pre + "Props/Bathroom/Shower_Head_01.prefab", "Shower Head", shower, 0.6f, 180f, new Vector3(-0.6f, 2.05f, D - 0.12f), false);

        // shower door (photo 1): hinged at the front-left corner and opened 90 degrees inward, lying along the left wall
        var sdoor = Group("Shower Door", shower);
        float dl = 0.86f;
        TBox("Leaf", sdoor, new Vector3(-W / 2 + 0.03f, 1.0f, GlassZ0 + dl / 2), new Vector3(0.035f, 2.0f, dl), wood, 1f);
        TBox("Lever", sdoor, new Vector3(-W / 2 + 0.07f, 1.0f, GlassZ0 + 0.14f), new Vector3(0.07f, 0.025f, 0.11f), chrome, 1f);

        // ---- sink side (right of the partition): pedestal sink, mirror, glass shelf
        var fix = Group("Fixtures", root);
        float sx = 0.3f, sz = D - 0.28f;
        Cyl("Pedestal", fix, new Vector3(sx, 0.34f, sz + 0.04f), 0.075f, 0.66f, porcelain);
        TBox("Basin", fix, new Vector3(sx, 0.78f, sz), new Vector3(0.5f, 0.12f, 0.38f), porcelain, 1f);
        TBox("Basin Rim Back", fix, new Vector3(sx, 0.87f, sz + 0.17f), new Vector3(0.5f, 0.1f, 0.05f), porcelain, 1f);
        TBox("Faucet", fix, new Vector3(sx, 0.92f, sz + 0.14f), new Vector3(0.03f, 0.1f, 0.03f), chrome, 1f);
        TBox("Faucet Spout", fix, new Vector3(sx, 0.97f, sz + 0.07f), new Vector3(0.025f, 0.025f, 0.14f), chrome, 1f);
        // mirror above the sink, silver cabinet doors above the toilet side
        TBox("Mirror", fix, new Vector3(sx - 0.02f, 1.55f, D - 0.01f), new Vector3(0.62f, 0.8f, 0.012f), mirror, 1f);
        TBox("Mirror Cabinet", fix, new Vector3(0.84f, 1.72f, D - 0.06f), new Vector3(0.58f, 0.72f, 0.12f), cabinet, 1f);
        TBox("Cabinet Seam", fix, new Vector3(0.84f, 1.72f, D - 0.123f), new Vector3(0.006f, 0.7f, 0.006f), chrome, 1f);
        TBox("Glass Shelf", fix, new Vector3(sx - 0.02f, 1.08f, D - 0.07f), new Vector3(0.5f, 0.012f, 0.12f), glassShelf, 1f);
        Cyl("Pump Bottle", fix, new Vector3(sx - 0.12f, 1.17f, D - 0.07f), 0.03f, 0.14f, bYellow);
        Cyl("Tube", fix, new Vector3(sx + 0.1f, 1.14f, D - 0.07f), 0.022f, 0.1f, bBlue);
        Cyl("Soap Pump", fix, new Vector3(sx + 0.17f, 0.9f, sz - 0.05f), 0.03f, 0.12f, bYellow);
        Cyl("Cleaner", fix, new Vector3(0.62f, 2.15f, D - 0.07f), 0.035f, 0.18f, lime);
        Cyl("Cleaner 2", fix, new Vector3(0.78f, 2.12f, D - 0.07f), 0.03f, 0.12f, bWhite);

        // toilet (kit prefab, tank to the back wall)
        float tx = 0.82f;
        var toilet = PlaceKit(Pre + "Furniture/Bathroom/Toilet_Apt_01.prefab", "Toilet", fix, 0.6f, 90f, new Vector3(tx, 0f, D - 0.358f), true);
        if (toilet != null)
            foreach (var r in toilet.GetComponentsInChildren<Renderer>()) { }

        // towel bar on the dry side of the partition wall + olive towel
        TBox("Towel Bar", fix, new Vector3(PartX + 0.1f, 1.05f, 1.8f), new Vector3(0.025f, 0.025f, 0.5f), chrome, 1f);
        TBox("Towel Bar Post A", fix, new Vector3(PartX + 0.07f, 1.05f, 1.58f), new Vector3(0.06f, 0.02f, 0.02f), chrome, 1f);
        TBox("Towel Bar Post B", fix, new Vector3(PartX + 0.07f, 1.05f, 2.02f), new Vector3(0.06f, 0.02f, 0.02f), chrome, 1f);
        TBox("Towel", fix, new Vector3(PartX + 0.1f, 0.84f, 1.8f), new Vector3(0.03f, 0.4f, 0.4f), towel, 1f);
        PlaceKit(Pre + "Props/Bathroom/ToiletPaperHolder_01.prefab", "Paper Holder", fix, 0.9f, 0f, new Vector3(W / 2 - 0.13f, 0.82f, 1.45f), false);

        // ---- lights
        var lights = Group("Lights", root);
        Cyl("Round Light", lights, new Vector3(-0.6f, H - 0.01f, 1.55f), 0.1f, 0.02f, lightGlow);
        TBox("LED Panel", lights, new Vector3(0.4f, H - 0.01f, 0.95f), new Vector3(0.65f, 0.02f, 0.16f), lightGlow, 1f);
        AddLight("Shower Light", lights, new Vector3(-0.6f, H - 0.3f, 1.55f), new Color(0.95f, 0.97f, 1f), 2.2f, 4f, LightShadows.Soft);
        AddLight("Dry Light", lights, new Vector3(0.4f, H - 0.3f, 1.0f), new Color(0.97f, 0.98f, 1f), 2.6f, 4.5f, LightShadows.Soft);
        AddLight("Hall Light", lights, new Vector3(0.3f, H - 0.3f, -1.3f), new Color(1f, 0.96f, 0.9f), 1.6f, 5f, LightShadows.None);

        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(50f, 140f, 0f);
            var l = sun.GetComponent<Light>(); l.intensity = 0.15f * LightTuning.SunScale; l.shadows = LightShadows.None;
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = LightTuning.Amb(new Color(0.46f, 0.47f, 0.48f));

        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            cam.transform.SetPositionAndRotation(new Vector3(0.4f, 1.5f, -1.5f), Quaternion.identity);
            cam.transform.LookAt(new Vector3(0.35f, 1.05f, 1.9f));
            var c = cam.GetComponent<Camera>();
            c.fieldOfView = 60f; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.3f, 0.3f, 0.32f);
        }
    }

    // -------------------------------------------------------------- kit prefab placement

    static GameObject PlaceKit(string path, string name, Transform parent, float scale, float yaw, Vector3 pos, bool floorSnap)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Log.AppendLine("MISSING " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.localScale = Vector3.one * scale;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        go.transform.position = Vector3.zero;
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        go.transform.position = new Vector3(pos.x - b.center.x, floorSnap ? -b.min.y : pos.y - b.center.y, pos.z - b.center.z);
        Log.AppendLine($"{name}: size=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2})");
        return go;
    }

    // -------------------------------------------------------------- helpers

    static Transform Group(string name, Transform parent)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    static GameObject TBox(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, float tile, bool swapUV = false)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.AddComponent<MeshFilter>().sharedMesh = MeshStore.Box(size, tile, swapUV);
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    static Mesh BoxMesh(Vector3 s, float tile, bool swapUV)
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

    static GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material mat, Vector3? euler = null)
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

    static GameObject Sphere(string name, Transform parent, Vector3 pos, float radius, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = Vector3.one * radius * 2f;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    static Material TexMat(string name, string albedo, string normal, Color tint, float smooth, float bump)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
        if (tex == null) Log.AppendLine("MISSING texture " + albedo);
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        if (!string.IsNullOrEmpty(normal))
        {
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
            if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Flat(string name, Color c, bool unlit)
    {
        string path = MatDir + "/" + name + ".mat";
        var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.3f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material GlassMat(string name)
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
        m.SetColor("_BaseColor", new Color(0.82f, 0.92f, 0.95f, 0.16f));
        m.SetFloat("_Smoothness", 0.9f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void AddLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range, LightShadows shadows)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = color; l.intensity = intensity * LightTuning.PointScale; l.range = range; l.shadows = shadows;
    }

    // -------------------------------------------------------------- capture

    static void CaptureAll(string dir)
    {
        Directory.CreateDirectory(dir);
        var main = GameObject.Find("Main Camera");
        if (main != null) Shoot("1_entrance", main.GetComponent<Camera>(), 700, 900, dir);

        var shower = TempCam(new Vector3(-0.4f, 1.5f, 0.35f), Quaternion.identity, 66f);
        shower.transform.LookAt(new Vector3(-0.4f, 1.2f, D));
        Shoot("2_shower", shower, 700, 900, dir);

        var toilet = TempCam(new Vector3(-0.7f, 1.5f, 0.9f), Quaternion.identity, 70f);
        toilet.transform.LookAt(new Vector3(0.8f, 0.9f, 1.9f));
        Shoot("3_toward_toilet", toilet, 700, 900, dir);

        var inside = TempCam(new Vector3(0.45f, 1.55f, 0.15f), Quaternion.identity, 68f);
        inside.transform.LookAt(new Vector3(0.4f, 1.0f, D));
        Shoot("4_sink_toilet", inside, 700, 900, dir);

        var top = TempCam(new Vector3(0.2f, 5.0f, 1.2f), Quaternion.Euler(90f, 0f, 0f), 45f);
        foreach (var n in new[] { "Ceiling", "Hall Ceiling" })
        {
            var go = GameObject.Find(n); if (go != null) go.GetComponent<Renderer>().enabled = false;
        }
        Shoot("5_top", top, 800, 800, dir);
        foreach (var n in new[] { "Ceiling", "Hall Ceiling" })
        {
            var go = GameObject.Find(n); if (go != null) go.GetComponent<Renderer>().enabled = true;
        }

        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c.name == "_CaptureCam") Object.DestroyImmediate(c.gameObject);
    }

    static Camera TempCam(Vector3 pos, Quaternion rot, float fov)
    {
        var g = new GameObject("_CaptureCam");
        g.transform.SetPositionAndRotation(pos, rot);
        var c = g.AddComponent<Camera>();
        c.fieldOfView = fov; c.nearClipPlane = 0.03f; c.farClipPlane = 30f;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.3f, 0.3f, 0.32f);
        g.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        return c;
    }

    static void Shoot(string name, Camera cam, int w, int h, string dir)
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
