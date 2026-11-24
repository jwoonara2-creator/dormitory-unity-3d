using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 2-person dorm room built from the Brick Project Studio Apartment Kit (kit prefabs + tiled textures).
// A bed along each side wall; behind each bed head a desk set (no computer) + wall shelf.
// Root "TwoPersonRoom" -> Assets/Prefabs/TwoPersonDormRoom.prefab (no scene file).
public static class Room2AssetBuilder
{
    const string MatDir = "Assets/Materials/Room2Asset";
    const string PrefabPath = "Assets/Prefabs/TwoPersonDormRoom.prefab";
    const string RootName = "TwoPersonRoom";
    const string Kit = "Assets/Brick Project Studio/Apartment Kit/";
    const string Pre = Kit + "_Prefabs/";
    const float KitScale = 0.6f;

    const float W = 3.0f, D = 5.0f, H = 2.4f;
    const float BedZ0 = 1.15f, BedLen = 2.0f;
    const float DeskZ = 4.1f;

    public static void BuildAndCapture()
    {
        // work scene only: used to build the prefab, never saved (no scene file is created)
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = Build();
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        bool ok;
        PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction, out ok);
        if (!ok) Debug.LogError("Room2AssetBuilder: failed to save prefab");
        SetupCamera();
        AssetDatabase.SaveAssets();
        CaptureAll(Arg("-captureDir") ?? "Captures");
    }

    static string Arg(string key)
    {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
        return null;
    }

    static Transform Build()
    {
        Directory.CreateDirectory(MatDir);
        GameObject old;
        while ((old = GameObject.Find(RootName)) != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;

        var wallLight = Flat("WallPaintWhite", new Color(0.91f, 0.9f, 0.87f)); wallLight.SetFloat("_Smoothness", 0.08f);
        var wallGray = Flat("WallPaintGray", new Color(0.55f, 0.54f, 0.52f)); wallGray.SetFloat("_Smoothness", 0.08f);
        var ceil = Flat("CeilingWhite", new Color(0.95f, 0.95f, 0.94f));
        var floor = TexMat("FloorWood", Kit + "Common/Textures/Surface/White Wood.png", Kit + "Common/Textures/Surface/White Wood_normal.png", new Color(0.58f, 0.56f, 0.53f), 0.3f, 0.5f);
        var birch = TexMat("Birch", Kit + "Common/Textures/Surface/White Wood.png", Kit + "Common/Textures/Surface/White Wood_normal.png", new Color(0.9f, 0.8f, 0.66f), 0.3f, 0.5f);
        var trim = Flat("Trim", new Color(0.95f, 0.95f, 0.93f));
        var mattressMat = TexMat("MattressTaupe", Kit + "Common/Textures/Surface/Fabric_01_A.png", Kit + "Common/Textures/Surface/Fabric_01_N.png", new Color(0.62f, 0.58f, 0.58f), 0.1f, 0.4f);
        var sky = Flat("OutsideSky", new Color(0.8f, 0.86f, 0.92f), true);
        var trees = Flat("OutsideTrees", new Color(0.42f, 0.52f, 0.4f), true);
        var glassMat = GlassMat("WindowGlassClear");
        var panelGlow = Flat("PanelGlow", new Color(0.97f, 0.97f, 0.95f));
        panelGlow.EnableKeyword("_EMISSION"); panelGlow.SetColor("_EmissionColor", new Color(1.1f, 1.1f, 1.05f));
        var panelFrame = Flat("PanelFrame", new Color(0.85f, 0.85f, 0.85f));

        var shell = Group("Shell", root);
        float cz = D / 2f;
        TBox("Floor", shell, new Vector3(0, -0.05f, cz), new Vector3(W + 0.3f, 0.1f, D + 0.3f), floor, 1.6f, true);
        TBox("Ceiling", shell, new Vector3(0, H + 0.05f, cz), new Vector3(W + 0.3f, 0.1f, D + 0.3f), ceil, 2f);
        TBox("Wall Left", shell, new Vector3(-W / 2 - 0.075f, H / 2, cz), new Vector3(0.15f, H + 0.2f, D + 0.3f), wallGray, 2f);
        TBox("Wall Right", shell, new Vector3(W / 2 + 0.075f, H / 2, cz), new Vector3(0.15f, H + 0.2f, D + 0.3f), wallGray, 2f);
        TBox("Wall Front L", shell, new Vector3(-1.0f, H / 2, -0.075f), new Vector3(1.0f, H + 0.2f, 0.15f), wallLight, 2f);
        TBox("Wall Front R", shell, new Vector3(1.0f, H / 2, -0.075f), new Vector3(1.0f, H + 0.2f, 0.15f), wallLight, 2f);
        TBox("Wall Front Lintel", shell, new Vector3(0, 2.25f, -0.075f), new Vector3(1.0f, 0.5f, 0.15f), wallLight, 2f);
        float fz = D + 0.075f;
        TBox("Back Below", shell, new Vector3(0, 0.5f, fz), new Vector3(W + 0.3f, 1.0f, 0.15f), wallLight, 2f);
        TBox("Back Above", shell, new Vector3(0, 2.365f, fz), new Vector3(W + 0.3f, 0.47f, 0.15f), wallLight, 2f);
        TBox("Back Left", shell, new Vector3(-1.0f, 1.565f, fz), new Vector3(1.0f, 1.13f, 0.15f), wallLight, 2f);
        TBox("Back Right", shell, new Vector3(1.0f, 1.565f, fz), new Vector3(1.0f, 1.13f, 0.15f), wallLight, 2f);
        TBox("Baseboard L", shell, new Vector3(-W / 2 + 0.012f, 0.05f, cz), new Vector3(0.024f, 0.1f, D), trim, 1f);
        TBox("Baseboard R", shell, new Vector3(W / 2 - 0.012f, 0.05f, cz), new Vector3(0.024f, 0.1f, D), trim, 1f);
        TBox("Baseboard Back", shell, new Vector3(0, 0.05f, D - 0.012f), new Vector3(W, 0.1f, 0.024f), trim, 1f);
        TBox("Switch", shell, new Vector3(-W / 2 + 0.012f, 1.25f, 0.9f), new Vector3(0.024f, 0.09f, 0.09f), trim, 1f);

        var win = Group("Window", root);
        var winGo = Place(Pre + "Apt Build Kit/Interiors/Frames Windows Stairs & Doors/Window_Frame_Apt_01.prefab", "Window Frame", win, 0.45f, 90f, new Vector3(0f, 0f, D - 0.04f), false);
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
        Quad("Sky", outside, new Vector3(0, 2.9f, D + 3.0f), new Vector2(9f, 2.6f), sky);
        Quad("Trees", outside, new Vector3(0, 1.1f, D + 2.9f), new Vector2(9f, 1.4f), trees);

        BuildSide(root, birch, mattressMat, -1);
        BuildSide(root, birch, mattressMat, +1);

        var lights = Group("Lights", root);
        TBox("Ceiling Panel Frame", lights, new Vector3(0, H - 0.01f, 2.3f), new Vector3(0.72f, 0.02f, 0.72f), panelFrame, 1f);
        TBox("Ceiling Panel", lights, new Vector3(0, H - 0.022f, 2.3f), new Vector3(0.62f, 0.01f, 0.62f), panelGlow, 1f);
        AddLight("Ceiling Light", lights, new Vector3(0, H - 0.35f, 2.3f), new Color(1f, 0.97f, 0.92f), 3.4f, 10f, LightShadows.Soft);
        AddLight("Window Fill", lights, new Vector3(0, 1.5f, D - 0.7f), new Color(0.85f, 0.92f, 1f), 1.4f, 4.5f, LightShadows.None);
        AddLight("Entrance Fill", lights, new Vector3(0, 2.0f, 0.7f), new Color(1f, 0.97f, 0.92f), 1.0f, 5f, LightShadows.None);

        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(38f, 205f, 0f);
            var l = sun.GetComponent<Light>();
            l.intensity = 1.0f * LightTuning.SunScale; l.color = new Color(1f, 0.98f, 0.94f); l.shadows = LightShadows.Soft;
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = LightTuning.Amb(new Color(0.5f, 0.5f, 0.49f));
        return root;
    }

    static void BuildSide(Transform root, Material birch, Material mattressMat, int side)
    {
        string tag = side < 0 ? "Left" : "Right";
        float wallX = side * (W / 2);
        float deskYaw = side > 0 ? 180f : 0f;
        float bz = BedZ0 + BedLen / 2f;
        var bed = Place(Pre + "Furniture/Bedroom/Bed_Apt_01_02.prefab", "Bed " + tag, root, KitScale, 90f, new Vector3(wallX - side * 0.502f, 0f, bz), true);
        Recolor(bed, birch);
        if (bed != null)
            foreach (var r in bed.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                    if (ms[i] != null && ms[i].name.StartsWith("Fabric") && !r.name.StartsWith("Pillow")) ms[i] = mattressMat;
                r.sharedMaterials = ms;
            }
        TBox("Bed Base " + tag, root, new Vector3(wallX - side * 0.502f, 0.17f, bz), new Vector3(0.94f, 0.34f, BedLen - 0.06f), birch, 1f);

        var desk = Place(Pre + "Furniture/Living Room/Table_Computer_01_Setup.prefab", "Desk Set " + tag, root, KitScale, deskYaw, new Vector3(wallX - side * 0.65f, 0f, DeskZ), true);
        DeskCleanup.RemoveComputer(desk);
        Recolor(desk, birch);

        var shelf = Place(Pre + "Furniture/Living Room/WallShelf_Apt_02.prefab", "Wall Shelf " + tag, root, KitScale, deskYaw, new Vector3(wallX - side * 0.22f, 0f, DeskZ), false);
        if (shelf != null) { SnapBottom(shelf, 1.45f); Recolor(shelf, birch); }
    }

    static GameObject Place(string path, string name, Transform parent, float scale, float yaw, Vector3 xz, bool floorSnap)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning("MISSING " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.localScale = Vector3.one * scale;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        go.transform.position = Vector3.zero;
        var b = Bounds(go);
        go.transform.position = new Vector3(xz.x - b.center.x, floorSnap ? -b.min.y : 0f, xz.z - b.center.z);
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
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
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

    static Material Flat(string name, Color c, bool unlit = false)
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

    static void SetupCamera()
    {
        var cam = GameObject.Find("Main Camera");
        if (cam == null) return;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 1.55f, 0.3f), Quaternion.identity);
        cam.transform.LookAt(new Vector3(0f, 1.1f, 3.5f));
        var c = cam.GetComponent<Camera>();
        c.fieldOfView = 62f; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.8f, 0.82f, 0.85f);
    }

    static void CaptureAll(string dir)
    {
        Directory.CreateDirectory(dir);
        var main = GameObject.Find("Main Camera");
        if (main == null) return;
        var cam = main.GetComponent<Camera>();
        var rt = new RenderTexture(900, 620, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(900, 620, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 900, 620), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(dir, "room2_main.png"), tex.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
    }
}
