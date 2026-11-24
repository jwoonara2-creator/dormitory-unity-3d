using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Rebuilds the "Dorm Room" in SampleScene using the Brick Project Studio Apartment Kit
// (furniture/window/lamp prefabs) plus tiled PBR-ish textures for walls, floor and wood.
// Run: Unity -batchmode -projectPath <proj> -executeMethod DormRoomAssetBuilder.BuildAndCapture -captureDir <dir>
public static class DormRoomAssetBuilder
{
    const string MatDir = "Assets/Materials/RoomAsset";
    const string Kit = "Assets/Brick Project Studio/Apartment Kit/";
    const string Basic = "Assets/Brick Project Studio/_BPS Basic Assets/Common/Textures/";
    const string Pre = Kit + "_Prefabs/";
    const float KitScale = 0.6f; // kit units are ~1.67x real size

    const float W = 2.3f, D = 5.0f, H = 2.4f;

    static readonly StringBuilder Log = new StringBuilder();

    public static void BuildAndCapture()
    {
        // work scene only: used to build the prefab, never saved (no scene file is created)
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Build();
        PrefabUtility.SaveAsPrefabAssetAndConnect(GameObject.Find("Dorm Room"), "Assets/Prefabs/DormRoom1Person.prefab", InteractionMode.AutomatedAction, out _);
        AssetDatabase.SaveAssets();
        CaptureAll(Arg("-captureDir") ?? "Captures");
        File.WriteAllText(Path.Combine(Arg("-captureDir") ?? "Captures", "build_log.txt"), Log.ToString());
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
        GameObject g;
        while ((g = GameObject.Find("Dorm Room")) != null) Object.DestroyImmediate(g);
        var root = new GameObject("Dorm Room").transform;

        // ---- materials (textured)
        // colours follow the reference photo: gray left wall, light beige-white others, pale gray-beige wood floor
        var wallLight = Flat("WallPaintWhite", new Color(0.91f, 0.9f, 0.87f), false);
        wallLight.SetFloat("_Smoothness", 0.08f);
        var wallDark = Flat("WallPaintGray", new Color(0.5f, 0.49f, 0.48f), false);
        wallDark.SetFloat("_Smoothness", 0.08f);
        var ceil = Flat("CeilingWhite", new Color(0.95f, 0.95f, 0.94f), false);
        var floor = TexMat("FloorWood", Kit + "Common/Textures/Surface/White Wood.png", Kit + "Common/Textures/Surface/White Wood_normal.png", new Color(0.58f, 0.56f, 0.53f), 0.3f, 0.5f);
        var mattressMat = TexMat("MattressTaupe", Kit + "Common/Textures/Surface/Fabric_01_A.png", Kit + "Common/Textures/Surface/Fabric_01_N.png", new Color(0.62f, 0.58f, 0.58f), 0.1f, 0.4f);
        var glassMat = GlassMat("WindowGlassClear");
        var birch = TexMat("Birch", Kit + "Common/Textures/Surface/White Wood.png", Kit + "Common/Textures/Surface/White Wood_normal.png", new Color(0.9f, 0.8f, 0.66f), 0.3f, 0.5f);
        var trim = TexMat("Trim", Kit + "Common/Textures/Surface/White Wood.png", null, new Color(0.95f, 0.95f, 0.93f), 0.25f, 0f);
        var sky = Flat("OutsideSky", new Color(0.8f, 0.86f, 0.92f), true);
        var trees = Flat("OutsideTrees", new Color(0.42f, 0.52f, 0.4f), true);
        var blind = Flat("Blind", new Color(0.52f, 0.52f, 0.55f), false);

        // ---- shell (UV-tiled so textures keep a real-world scale)
        var shell = Group("Shell", root);
        float cz = D / 2f;
        TBox("Floor", shell, new Vector3(0, -0.05f, cz), new Vector3(W + 0.3f, 0.1f, D + 0.3f), floor, 1.6f, true);
        TBox("Ceiling", shell, new Vector3(0, H + 0.05f, cz), new Vector3(W + 0.3f, 0.1f, D + 0.3f), ceil, 2f);
        TBox("Wall Left", shell, new Vector3(-W / 2 - 0.075f, H / 2, cz), new Vector3(0.15f, H + 0.2f, D + 0.3f), wallDark, 2f);
        TBox("Wall Right", shell, new Vector3(W / 2 + 0.075f, H / 2, cz), new Vector3(0.15f, H + 0.2f, D + 0.3f), wallLight, 2f);
        TBox("Wall Near", shell, new Vector3(0, H / 2, -0.075f), new Vector3(W + 0.3f, H + 0.2f, 0.15f), wallLight, 2f);

        float fz = D + 0.075f; // far wall with window opening x[-0.3,0.65] y[1.0,2.1]
        TBox("Far Below", shell, new Vector3(0, 0.5f, fz), new Vector3(2.6f, 1.0f, 0.15f), wallLight, 2f);
        TBox("Far Above", shell, new Vector3(0, 2.25f, fz), new Vector3(2.6f, 0.3f, 0.15f), wallLight, 2f);
        TBox("Far Left", shell, new Vector3(-0.8f, 1.55f, fz), new Vector3(1.0f, 1.1f, 0.15f), wallLight, 2f);
        TBox("Far Right", shell, new Vector3(0.975f, 1.55f, fz), new Vector3(0.65f, 1.1f, 0.15f), wallLight, 2f);
        TBox("Pier", shell, new Vector3(-1.0f, H / 2, 4.8f), new Vector3(0.3f, H, 0.4f), wallLight, 2f);

        TBox("Baseboard L", shell, new Vector3(-W / 2 + 0.012f, 0.05f, cz), new Vector3(0.024f, 0.1f, D), trim, 1f);
        TBox("Baseboard R", shell, new Vector3(W / 2 - 0.012f, 0.05f, cz), new Vector3(0.024f, 0.1f, D), trim, 1f);
        TBox("Baseboard Far", shell, new Vector3(0, 0.05f, D - 0.012f), new Vector3(W, 0.1f, 0.024f), trim, 1f);
        TBox("Switch", shell, new Vector3(-W / 2 + 0.012f, 1.25f, 1.0f), new Vector3(0.024f, 0.09f, 0.09f), trim, 1f);

        // ---- window: kit window frame sits in the opening
        var win = Group("Window", root);
        var winGo = Place(Pre + "Apt Build Kit/Interiors/Frames Windows Stairs & Doors/Window_Frame_Apt_01.prefab", "Window Frame", win, 0.45f, 90f,
            new Vector3(0.175f, 0f, D - 0.04f), false);
        if (winGo != null)
        {
            SnapBottom(winGo, 1.0f);
            foreach (var r in winGo.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].name.Contains("Glass")) ms[i] = glassMat;
                r.sharedMaterials = ms;
            }
        }
        var outside = Group("Outside", root);
        Quad("Sky", outside, new Vector3(0, 2.85f, 7.0f), new Vector2(10f, 3f), sky);
        Quad("Trees", outside, new Vector3(0, 0.7f, 6.95f), new Vector2(10f, 1.4f), trees);
        TBox("Blind", win, new Vector3(0.75f, 1.55f, D - 0.06f), new Vector3(0.2f, 1.1f, 0.03f), blind, 1f);

        // ---- bed (kit single bed, head toward the far wall, along the left wall)
        var bed = Place(Pre + "Furniture/Bedroom/Bed_Apt_01_02.prefab", "Bed", root, KitScale, 90f,
            new Vector3(-W / 2 + 0.502f, 0f, 3.5f), true);
        Recolor(bed, birch);
        if (bed != null)
            foreach (var r in bed.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].name.StartsWith("Fabric") && !r.name.StartsWith("Pillow")) ms[i] = mattressMat;
                r.sharedMaterials = ms;
            }
        // boxy storage base under the mattress, like the photo
        TBox("Bed Base", root, new Vector3(-W / 2 + 0.502f, 0.17f, 3.5f), new Vector3(0.94f, 0.34f, 1.94f), birch, 1f);

        // ---- desk set (desk + monitor + chair) against the right wall at the far end
        var desk = Place(Pre + "Furniture/Living Room/Table_Computer_01_Setup.prefab", "Desk Set", root, KitScale, 180f,
            new Vector3(W / 2 - 0.65f, 0f, 4.1f), true);
        Recolor(desk, birch);
        DeskCleanup.RemoveComputer(desk); // no computer on the desk

        // ---- wall shelf above the desk
        var shelf = Place(Pre + "Furniture/Living Room/WallShelf_Apt_02.prefab", "Wall Shelf", root, KitScale, 180f,
            new Vector3(W / 2 - 0.22f, 0f, 4.1f), false);
        if (shelf != null) { SnapBottom(shelf, 1.45f); Recolor(shelf, birch); }

        // ---- wardrobe near the entrance, right wall
        var ward = Place(Pre + "Furniture/Bedroom/Dresser Closet_01.prefab", "Wardrobe", root, KitScale, 180f,
            new Vector3(W / 2 - 0.384f, 0f, 1.0f), true);
        FaceDoors(ward, Vector3.left); // doors toward the room, not the wall
        var wardWhite = Flat("WardrobeBeige", new Color(0.82f, 0.78f, 0.7f), false);
        wardWhite.SetFloat("_Smoothness", 0.2f);
        if (ward != null)
            foreach (var r in ward.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) if (ms[i] != null && (ms[i].name.StartsWith("Plastic") || ms[i].name.StartsWith("Mirror"))) ms[i] = wardWhite;
                r.sharedMaterials = ms;
            }

        // ---- ceiling light panel (kit lamp) + lights
        var lights = Group("Lights", root);
        // square flat ceiling panel (frame + glowing face) like the photo
        var panelGlow = Flat("PanelGlow", new Color(0.97f, 0.97f, 0.95f), false);
        panelGlow.EnableKeyword("_EMISSION");
        panelGlow.SetColor("_EmissionColor", new Color(1.1f, 1.1f, 1.05f));
        var panelFrame = Flat("PanelFrame", new Color(0.85f, 0.85f, 0.85f), false);
        TBox("Ceiling Panel Frame", lights, new Vector3(0.1f, H - 0.01f, 2.5f), new Vector3(0.72f, 0.02f, 0.72f), panelFrame, 1f);
        TBox("Ceiling Panel", lights, new Vector3(0.1f, H - 0.022f, 2.5f), new Vector3(0.62f, 0.01f, 0.62f), panelGlow, 1f);
        AddLight("Ceiling Light", lights, new Vector3(0.1f, H - 0.35f, 2.5f), new Color(1f, 0.97f, 0.92f), 3.2f, 9f, LightShadows.Soft);
        AddLight("Window Fill", lights, new Vector3(0.2f, 1.5f, 4.4f), new Color(0.85f, 0.92f, 1f), 1.4f, 4f, LightShadows.None);
        AddLight("Entrance Fill", lights, new Vector3(0f, 2.0f, 0.8f), new Color(1f, 0.97f, 0.92f), 1.0f, 5f, LightShadows.None);

        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(35f, 200f, 0f);
            var l = sun.GetComponent<Light>();
            l.intensity = 1.1f * LightTuning.SunScale; l.color = new Color(1f, 0.98f, 0.94f); l.shadows = LightShadows.Soft;
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = LightTuning.Amb(new Color(0.5f, 0.5f, 0.49f));

        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            cam.transform.SetPositionAndRotation(new Vector3(0.05f, 1.5f, 0.3f), Quaternion.Euler(3f, 0f, 0f));
            cam.GetComponent<Camera>().fieldOfView = 65f;
        }
    }

    // -------------------------------------------------------------- placing kit prefabs

    // Instantiates a kit prefab, scales/rotates it, then moves it so its bounds centre sits at 'xz' (and bottom on the floor).
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
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials) if (m != null) Log.AppendLine($"   {r.name}: {m.name} [{m.shader.name}]");
        return go;
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

    // swap dark wood style materials for the light birch one
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

    // -------------------------------------------------------------- helpers

    static Transform Group(string name, Transform parent)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    // box with UVs scaled to world metres (tile = metres per texture repeat)
    static GameObject TBox(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, float tile, bool swapUV = false)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.AddComponent<MeshFilter>().sharedMesh = MeshStore.Box(size, tile, swapUV);
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    // swapUV rotates the texture 90 degrees (wood planks running along the room length)
    static Mesh BoxMesh(Vector3 s, float tile, bool swapUV = false)
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
        if (swapUV) for (int k = 0; k < uvs.Count; k++) uvs[k] = new Vector2(uvs[k].y, uvs[k].x);
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetNormals(nrm); m.SetTriangles(tris, 0);
        m.RecalculateBounds(); m.RecalculateTangents();
        return m;
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
            else Log.AppendLine("MISSING normal " + normal);
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
        if (main != null) Shoot("1_main", main.GetComponent<Camera>(), 620, 660, dir);
        Shoot("2_back", TempCam(new Vector3(0.2f, 1.5f, 4.7f), Quaternion.Euler(5f, 180f, 0f), 70f), 800, 600, dir);
        var side = TempCam(new Vector3(-0.8f, 1.5f, 1.5f), Quaternion.identity, 70f);
        side.transform.LookAt(new Vector3(0.7f, 1.0f, 4.2f));
        Shoot("3_side", side, 800, 600, dir);
        var desk = TempCam(new Vector3(-0.3f, 1.4f, 2.8f), Quaternion.identity, 70f);
        desk.transform.LookAt(new Vector3(1.0f, 1.0f, 4.3f));
        Shoot("4_desk", desk, 800, 600, dir);
        var wardCam = TempCam(new Vector3(-0.3f, 1.3f, 1.0f), Quaternion.identity, 70f);
        wardCam.transform.LookAt(new Vector3(0.75f, 1.1f, 1.0f));
        Shoot("6_ward", wardCam, 700, 700, dir);
        var top = TempCam(new Vector3(0f, 6f, 2.5f), Quaternion.Euler(90f, 0f, 0f), 50f);
        var ceilGo = GameObject.Find("Ceiling");
        var cr = ceilGo != null ? ceilGo.GetComponent<Renderer>() : null;
        if (cr != null) cr.enabled = false;
        Shoot("5_top", top, 700, 900, dir);
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
