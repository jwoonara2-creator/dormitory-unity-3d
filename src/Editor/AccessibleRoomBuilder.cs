using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 1-person room for accessibility ("장애인실"), built from the Brick Project Studio Apartment Kit.
// Layout from the reference photo (left): wide room for wheelchair turning space, gray side walls,
// bed along the right wall at the window end, desk + tall wardrobe + hanger rack along the left wall,
// black-framed window with a gray roller blind on its right half, square ceiling light + AC unit.
// Everything sits under one "AccessibleSingleRoom" root in Assets/Scenes/Room1Accessible.unity.
// Run: Unity -batchmode -projectPath <proj> -executeMethod AccessibleRoomBuilder.BuildAndCapture -captureDir <dir>
public static class AccessibleRoomBuilder
{
    const string MatDir = "Assets/Materials/RoomAccessible";
    const string RootName = "AccessibleSingleRoom";
    const string Kit = "Assets/Brick Project Studio/Apartment Kit/";
    const string Pre = Kit + "_Prefabs/";
    const float KitScale = 0.6f; // kit units are ~1.67x real size

    // interior: x in [-W/2, W/2], z in [0 (entrance wall), D (back wall, window)], y in [0, H]
    const float W = 3.2f, D = 4.6f, H = 2.4f;

    static readonly StringBuilder Log = new StringBuilder();

    public static void BuildAndCapture()
    {
        // work scene only: used to build the prefab, never saved (no scene file is created)
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Build();
        SaveAsPrefab();
        AssetDatabase.SaveAssets();
        string dir = Arg("-captureDir") ?? "Captures";
        CaptureAll(dir);
        File.WriteAllText(Path.Combine(dir, "build_log.txt"), Log.ToString());
    }

    // whole room -> one prefab, so it can be dragged into any other scene
    static void SaveAsPrefab()
    {
        const string path = "Assets/Prefabs/AccessibleSingleRoom.prefab";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var root = GameObject.Find(RootName);
        bool ok;
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction, out ok);
        if (!ok) Debug.LogError("AccessibleRoomBuilder: failed to save prefab " + path);
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
        while ((old = GameObject.Find(RootName)) != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;

        // ---- materials
        var wallBeige = Flat("WallBeige", new Color(0.86f, 0.84f, 0.79f), false); wallBeige.SetFloat("_Smoothness", 0.08f);
        var wallGray = Flat("WallGray", new Color(0.5f, 0.49f, 0.48f), false); wallGray.SetFloat("_Smoothness", 0.08f);
        var ceil = Flat("CeilingWhite", new Color(0.95f, 0.95f, 0.94f), false);
        var floor = TexMat("FloorWood", Kit + "Common/Textures/Surface/White Wood.png", Kit + "Common/Textures/Surface/White Wood_normal.png", new Color(0.62f, 0.6f, 0.56f), 0.3f, 0.5f);
        var birch = TexMat("Birch", Kit + "Common/Textures/Surface/White Wood.png", Kit + "Common/Textures/Surface/White Wood_normal.png", new Color(0.9f, 0.8f, 0.66f), 0.3f, 0.5f);
        var trim = Flat("Trim", new Color(0.93f, 0.93f, 0.9f), false);
        var mattressMat = TexMat("MattressTaupe", Kit + "Common/Textures/Surface/Fabric_01_A.png", Kit + "Common/Textures/Surface/Fabric_01_N.png", new Color(0.62f, 0.58f, 0.58f), 0.1f, 0.4f);
        var sky = Flat("OutsideSky", new Color(0.82f, 0.88f, 0.93f), true);
        var trees = Flat("OutsideTrees", new Color(0.36f, 0.5f, 0.34f), true);
        var glassMat = GlassMat("WindowGlassClear");
        var frameBlack = Flat("WindowFrameBlack", new Color(0.08f, 0.09f, 0.1f), false); frameBlack.SetFloat("_Smoothness", 0.4f);
        var blindGray = Flat("Blind", new Color(0.35f, 0.36f, 0.4f), false);
        var panelGlow = Flat("PanelGlow", new Color(0.97f, 0.97f, 0.95f), false);
        panelGlow.EnableKeyword("_EMISSION"); panelGlow.SetColor("_EmissionColor", new Color(1.1f, 1.1f, 1.05f));
        var panelFrame = Flat("PanelFrame", new Color(0.86f, 0.86f, 0.86f), false);
        var vent = Flat("Vent", new Color(0.9f, 0.9f, 0.9f), false);
        var rackWhite = Flat("RackWhite", new Color(0.94f, 0.94f, 0.93f), false); rackWhite.SetFloat("_Metallic", 0.3f);
        var doorDark = TexMat("DoorDark", Kit + "Common/Textures/Surface/White Wood.png", Kit + "Common/Textures/Surface/White Wood_normal.png", new Color(0.32f, 0.27f, 0.22f), 0.25f, 0.4f);
        var wardMat = Flat("WardrobeBeige", new Color(0.84f, 0.78f, 0.66f), false); wardMat.SetFloat("_Smoothness", 0.2f);
        var white = Flat("White", new Color(0.92f, 0.92f, 0.9f), false);

        // ---- shell
        var shell = Group("Shell", root);
        float cz = D / 2f;
        TBox("Floor", shell, new Vector3(0, -0.05f, cz), new Vector3(W + 0.3f, 0.1f, D + 0.3f), floor, 1.6f, true);
        TBox("Ceiling", shell, new Vector3(0, H + 0.05f, cz), new Vector3(W + 0.3f, 0.1f, D + 0.3f), ceil, 2f);
        TBox("Wall Left", shell, new Vector3(-W / 2 - 0.075f, H / 2, cz), new Vector3(0.15f, H + 0.2f, D + 0.3f), wallGray, 2f);
        TBox("Wall Right", shell, new Vector3(W / 2 + 0.075f, H / 2, cz), new Vector3(0.15f, H + 0.2f, D + 0.3f), wallGray, 2f);
        // entrance wall with a doorway x[0.7,1.5]
        TBox("Wall Front L", shell, new Vector3(-0.45f, H / 2, -0.075f), new Vector3(2.3f + 0.1f, H + 0.2f, 0.15f), wallBeige, 2f);
        TBox("Wall Front R", shell, new Vector3(1.55f, H / 2, -0.075f), new Vector3(0.1f, H + 0.2f, 0.15f), wallBeige, 2f);
        TBox("Wall Front Lintel", shell, new Vector3(1.1f, 2.25f, -0.075f), new Vector3(0.8f, 0.5f, 0.15f), wallBeige, 2f);

        // back wall with the window opening x[-0.8,0.18] y[1.0,2.13]
        float fz = D + 0.075f;
        TBox("Back Below", shell, new Vector3(0, 0.5f, fz), new Vector3(W + 0.3f, 1.0f, 0.15f), wallBeige, 2f);
        TBox("Back Above", shell, new Vector3(0, 2.365f, fz), new Vector3(W + 0.3f, 0.47f, 0.15f), wallBeige, 2f);
        TBox("Back Left", shell, new Vector3((-W / 2 - 0.15f + -0.8f) / 2, 1.565f, fz), new Vector3(-0.8f + W / 2 + 0.15f, 1.13f, 0.15f), wallBeige, 2f);
        TBox("Back Right", shell, new Vector3((0.18f + W / 2 + 0.15f) / 2, 1.565f, fz), new Vector3(W / 2 + 0.15f - 0.18f, 1.13f, 0.15f), wallBeige, 2f);

        TBox("Baseboard L", shell, new Vector3(-W / 2 + 0.012f, 0.05f, cz), new Vector3(0.024f, 0.1f, D), trim, 1f);
        TBox("Baseboard R", shell, new Vector3(W / 2 - 0.012f, 0.05f, cz), new Vector3(0.024f, 0.1f, D), trim, 1f);
        TBox("Baseboard Back", shell, new Vector3(0, 0.05f, D - 0.012f), new Vector3(W, 0.1f, 0.024f), trim, 1f);
        TBox("Outlet", shell, new Vector3(-W / 2 + 0.012f, 0.3f, 0.45f), new Vector3(0.024f, 0.08f, 0.08f), white, 1f);
        TBox("Switch", shell, new Vector3(-W / 2 + 0.012f, 1.0f, 1.0f), new Vector3(0.024f, 0.09f, 0.09f), white, 1f); // low switch (accessible height)

        // ---- window: kit frame recoloured black, clear glass, gray roller blind on the right half
        var win = Group("Window", root);
        var winGo = Place(Pre + "Apt Build Kit/Interiors/Frames Windows Stairs & Doors/Window_Frame_Apt_01.prefab", "Window Frame", win, 0.45f, 90f,
            new Vector3(-0.31f, 0f, D - 0.04f), false);
        if (winGo != null)
        {
            SnapBottom(winGo, 1.0f);
            foreach (var r in winGo.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null) continue;
                    if (ms[i].name.Contains("Glass")) ms[i] = glassMat;
                    else if (ms[i].name.Contains("White Wood")) ms[i] = frameBlack;
                }
                r.sharedMaterials = ms;
            }
        }
        TBox("Blind Box", win, new Vector3(-0.06f, 2.13f, D - 0.08f), new Vector3(0.52f, 0.07f, 0.09f), vent, 1f);
        TBox("Blind", win, new Vector3(-0.06f, 1.63f, D - 0.07f), new Vector3(0.5f, 1.0f, 0.025f), blindGray, 1f);
        var outside = Group("Outside", root);
        Quad("Sky", outside, new Vector3(-0.3f, 2.9f, D + 3.0f), new Vector2(9f, 2.6f), sky);
        Quad("Trees", outside, new Vector3(-0.3f, 1.1f, D + 2.9f), new Vector2(9f, 1.4f), trees);

        // ---- bed along the right wall, head at the window end
        float bedZ = D - 1.0f - 0.002f;   // head almost touching the back wall
        var bed = Place(Pre + "Furniture/Bedroom/Bed_Apt_01_02.prefab", "Bed", root, KitScale, 90f, new Vector3(W / 2 - 0.5f - 0.002f, 0f, bedZ), true);
        Recolor(bed, birch);
        if (bed != null)
            foreach (var r in bed.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                    if (ms[i] != null && ms[i].name.StartsWith("Fabric") && !r.name.StartsWith("Pillow")) ms[i] = mattressMat;
                r.sharedMaterials = ms;
            }

        // box base under the mattress (slightly smaller than the kit frame so no faces overlap)
        TBox("Bed Base", root, new Vector3(W / 2 - 0.5f - 0.002f, 0.16f, bedZ), new Vector3(0.9f, 0.32f, 1.9f), birch, 1f);

        // ---- desk (no computer) with chair against the left wall, toward the window
        var desk = Place(Pre + "Furniture/Living Room/Table_Computer_01_Setup.prefab", "Desk Set", root, KitScale, 0f,
            new Vector3(-W / 2 + 0.65f, 0f, 3.35f), true);
        DeskCleanup.RemoveComputer(desk);
        Recolor(desk, birch);

        // ---- tall wardrobe on the left wall in front of the desk (doors toward the room)
        var ward = Place(Pre + "Furniture/Bedroom/Dresser Closet_01.prefab", "Wardrobe", root, 0.48f, 0f,
            new Vector3(-W / 2 + 0.31f, 0f, 2.0f), true);
        FaceDoors(ward, Vector3.right);
        if (ward != null)
            foreach (var r in ward.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                    if (ms[i] != null && (ms[i].name.StartsWith("Plastic") || ms[i].name.StartsWith("Mirror"))) ms[i] = wardMat;
                r.sharedMaterials = ms;
            }

        // ---- white hanger rack on the left wall near the entrance (two posts + rails)
        var rack = Group("Hanger Rack", root);
        foreach (float rz in new[] { 0.45f, 1.15f })
            Cyl("Rack Post", rack, new Vector3(-W / 2 + 0.38f, 0.85f, rz), 0.013f, 1.7f, rackWhite);
        Cyl("Rack Rail Top", rack, new Vector3(-W / 2 + 0.38f, 1.62f, 0.8f), 0.012f, 0.72f, rackWhite, new Vector3(90f, 0, 0));
        Cyl("Rack Rail Mid", rack, new Vector3(-W / 2 + 0.38f, 1.0f, 0.8f), 0.012f, 0.72f, rackWhite, new Vector3(90f, 0, 0));
        Cyl("Rack Arm A", rack, new Vector3(-W / 2 + 0.2f, 1.62f, 0.45f), 0.01f, 0.36f, rackWhite, new Vector3(0, 0, 90f));
        Cyl("Rack Arm B", rack, new Vector3(-W / 2 + 0.2f, 1.62f, 1.15f), 0.01f, 0.36f, rackWhite, new Vector3(0, 0, 90f));

        // ---- entrance door (dark wood leaf swung open against the right wall)
        var door = Group("Entrance Door", root);
        TBox("Slab", door, new Vector3(W / 2 - 0.06f, 1.0f, 0.5f), new Vector3(0.045f, 2.0f, 0.86f), doorDark, 1f);
        TBox("Frame L", door, new Vector3(0.68f, 1.02f, -0.06f), new Vector3(0.05f, 2.04f, 0.14f), doorDark, 1f);
        TBox("Frame R", door, new Vector3(1.52f, 1.02f, -0.06f), new Vector3(0.05f, 2.04f, 0.14f), doorDark, 1f);

        // ---- ceiling: square light panel + AC unit above the window
        var lights = Group("Lights", root);
        TBox("Ceiling Panel Frame", lights, new Vector3(0, H - 0.01f, 2.1f), new Vector3(0.74f, 0.02f, 0.74f), panelFrame, 1f);
        TBox("Ceiling Panel", lights, new Vector3(0, H - 0.022f, 2.1f), new Vector3(0.64f, 0.01f, 0.64f), panelGlow, 1f);
        TBox("AC Unit", lights, new Vector3(-0.3f, H - 0.1f, D - 0.2f), new Vector3(0.95f, 0.18f, 0.3f), vent, 1f);
        AddLight("Ceiling Light", lights, new Vector3(0, H - 0.35f, 2.1f), new Color(1f, 0.97f, 0.92f), 3.4f, 10f, LightShadows.Soft);
        AddLight("Window Fill", lights, new Vector3(-0.3f, 1.5f, D - 0.7f), new Color(0.85f, 0.92f, 1f), 1.5f, 4.5f, LightShadows.None);
        AddLight("Entrance Fill", lights, new Vector3(0.2f, 2.0f, 0.7f), new Color(1f, 0.97f, 0.92f), 1.0f, 5f, LightShadows.None);

        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(38f, 205f, 0f);
            var l = sun.GetComponent<Light>(); l.intensity = 1.0f * LightTuning.SunScale; l.color = new Color(1f, 0.98f, 0.94f); l.shadows = LightShadows.Soft;
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = LightTuning.Amb(new Color(0.5f, 0.5f, 0.49f));

        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            cam.transform.SetPositionAndRotation(new Vector3(0.9f, 1.5f, 0.3f), Quaternion.identity);
            cam.transform.LookAt(new Vector3(-0.1f, 1.1f, 3.6f));
            var c = cam.GetComponent<Camera>();
            c.fieldOfView = 64f; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.8f, 0.82f, 0.85f);
        }
    }

    // -------------------------------------------------------------- kit prefab placement

    static GameObject Place(string path, string name, Transform parent, float scale, float yaw, Vector3 xz, bool floorSnap)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Log.AppendLine("MISSING " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.localScale = Vector3.one * scale;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        go.transform.position = Vector3.zero;
        var b = Bounds(go);
        go.transform.position = new Vector3(xz.x - b.center.x, floorSnap ? -b.min.y : 0f, xz.z - b.center.z);
        var b2 = Bounds(go);
        Log.AppendLine($"{name}: size=({b2.size.x:F2},{b2.size.y:F2},{b2.size.z:F2}) center=({b2.center.x:F2},{b2.center.y:F2},{b2.center.z:F2})");
        return go;
    }

    static void SnapBottom(GameObject go, float y)
    {
        var b = Bounds(go);
        go.transform.position += new Vector3(0, y - b.min.y, 0);
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void Recolor(GameObject go, Material birch)
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

    // rotate around the bounds centre so the renderers named "Door*" end up on the 'want' side
    static void FaceDoors(GameObject go, Vector3 want)
    {
        if (go == null) return;
        var c = Bounds(go).center;
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            if (r.name.StartsWith("Door")) { sum += r.bounds.center; n++; }
        if (n == 0) return;
        var off = sum / n - c; off.y = 0;
        if (off.sqrMagnitude < 1e-6f) return;
        float ang = Vector3.SignedAngle(off, want, Vector3.up);
        go.transform.RotateAround(c, Vector3.up, ang);
        Log.AppendLine($"FaceDoors {go.name}: rotated {ang:F0}");
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

    static GameObject Quad(string name, Transform parent, Vector3 pos, Vector2 size, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = new Vector3(size.x, size.y, 1f);
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
        m.SetColor("_BaseColor", new Color(0.8f, 0.9f, 0.95f, 0.12f));
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
        if (main != null) Shoot("1_main", main.GetComponent<Camera>(), 700, 760, dir);

        var desk = TempCam(new Vector3(0.7f, 1.5f, 1.3f), Quaternion.identity, 68f);
        desk.transform.LookAt(new Vector3(-1.2f, 0.9f, 3.0f));
        Shoot("2_desk", desk, 800, 600, dir);

        var bed = TempCam(new Vector3(-0.6f, 1.5f, 0.6f), Quaternion.identity, 68f);
        bed.transform.LookAt(new Vector3(1.0f, 0.7f, 3.5f));
        Shoot("3_bed", bed, 800, 600, dir);

        var back = TempCam(new Vector3(0f, 1.5f, D - 0.4f), Quaternion.Euler(8f, 180f, 0f), 75f);
        Shoot("4_from_window", back, 800, 600, dir);

        var top = TempCam(new Vector3(0f, 7f, D / 2f), Quaternion.Euler(90f, 0f, 0f), 50f);
        var ceilGo = GameObject.Find("Ceiling");
        var cr = ceilGo != null ? ceilGo.GetComponent<Renderer>() : null;
        if (cr != null) cr.enabled = false;
        Shoot("5_top", top, 800, 900, dir);
        if (cr != null) cr.enabled = true;

        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c.name == "_CaptureCam") Object.DestroyImmediate(c.gameObject);
    }

    static Camera TempCam(Vector3 pos, Quaternion rot, float fov)
    {
        var g = new GameObject("_CaptureCam");
        g.transform.SetPositionAndRotation(pos, rot);
        var c = g.AddComponent<Camera>();
        c.fieldOfView = fov; c.nearClipPlane = 0.05f; c.farClipPlane = 50f;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.8f, 0.82f, 0.85f);
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
