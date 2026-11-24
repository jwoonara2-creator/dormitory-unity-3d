using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Automatic geometry audit of every room scene and prefab.
//   1. missing mesh / material / error shader / missing script         (per scene + per prefab)
//   2. coplanar overlapping box faces with different materials          (z-fighting / flicker)
//   3. kit furniture penetrating walls or other furniture
//   4. kit furniture floating above / sunk below the floor
//   5. furniture that almost touches a wall (gap 4 mm .. 20 cm)
// Writes audit_report.txt and close-up renders of the first findings into -captureDir.
// Run: Unity -batchmode -projectPath <proj> -executeMethod SceneAudit.Run -captureDir <dir>
public static class SceneAudit
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Corridor.unity", "Assets/Scenes/FloorView.unity",
        "Assets/Scenes/WomenCorridor.unity", "Assets/Scenes/WomenFloorView.unity",
    };

    // prefabs that have no scene of their own: instantiated into a throw-away scene and audited like a scene
    static readonly string[] PrefabsAsScenes =
    {
        "DormRoom1Person", "TwoPersonDormRoom", "AccessibleSingleRoom", "Bathroom", "RestLounge",
        "RoomWithBath_1Person", "RoomWithBath_2Person", "RoomWithBath_Accessible",
    };

    class Finding { public string kind, text; public Bounds focus; }

    static StringBuilder report = new StringBuilder();
    static StringBuilder raw = new StringBuilder();
    static string outDir;

    public static void Run()
    {
        var args = System.Environment.GetCommandLineArgs();
        outDir = args[System.Array.IndexOf(args, "-captureDir") + 1];
        Directory.CreateDirectory(outDir);

        AuditPrefabs();
        foreach (var s in Scenes) AuditScene(s);
        foreach (var p in PrefabsAsScenes) AuditPrefabInScene("Assets/Prefabs/" + p + ".prefab");
        File.WriteAllText(Path.Combine(outDir, "audit_report.txt"), report.ToString());
        File.WriteAllText(Path.Combine(outDir, "audit_raw.txt"), raw.ToString());
    }

    // ------------------------------------------------------------------ prefabs

    static void AuditPrefabs()
    {
        report.AppendLine("=== PREFABS ===");
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            int nullMesh = 0, nullMat = 0, errShader = 0, missingScripts = 0, total = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                var mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh == null) nullMesh++;
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                total++;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { nullMat++; continue; }
                    if (m.shader == null || m.shader.name.Contains("InternalError")) errShader++;
                }
            }
            report.AppendLine($"{Path.GetFileName(path)}: renderers={total} nullMesh={nullMesh} nullMaterial={nullMat} errorShader={errShader} missingScripts={missingScripts}");
            PrefabUtility.UnloadPrefabContents(root);
        }
        report.AppendLine();
    }

    // ------------------------------------------------------------------ scenes

    static void AuditScene(string path)
    {
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        AuditOpenScene(Path.GetFileNameWithoutExtension(path));
    }

    static void AuditPrefabInScene(string prefabPath)
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        AuditOpenScene("prefab " + Path.GetFileNameWithoutExtension(prefabPath));
    }

    static void AuditOpenScene(string sname)
    {
        report.AppendLine("=== SCENE " + sname + " ===");
        var findings = new List<Finding>();

        var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>() != null).ToList();

        // 1. broken references
        foreach (var r in all)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf.sharedMesh == null) findings.Add(F("missing-mesh", "missing mesh on " + P(r.transform), r.bounds));
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) findings.Add(F("missing-material", "null material on " + P(r.transform), r.bounds));
                else if (m.shader == null || m.shader.name.Contains("InternalError")) findings.Add(F("pink-material", "error shader on " + P(r.transform) + " (" + m.name + ")", r.bounds));
            }
        }

        // 2. coplanar same-direction faces with different materials (axis-aligned box meshes only)
        var boxes = all.Where(r => IsBox(r) && !IsGlassLike(r)).ToList();
        var seen = new HashSet<string>();
        for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
            {
                var a = boxes[i].bounds; var b = boxes[j].bounds;
                if (!a.Intersects(b)) continue;
                if (boxes[i].sharedMaterial == boxes[j].sharedMaterial) continue;
                if (IsShellPart(boxes[i].name) && IsShellPart(boxes[j].name)) continue;
                string axis = CoplanarAxis(a, b);
                if (axis == null) continue;
                string key = Generic(boxes[i].name) + "|" + Generic(boxes[j].name) + "|" + axis;
                if (!seen.Add(key)) continue;
                findings.Add(F("z-fighting", $"coplanar faces ({axis}) '{P(boxes[i].transform)}' <-> '{P(boxes[j].transform)}'", Merge(a, b)));
            }

        // 3-5. kit furniture checks
        var furn = new List<Transform>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
            string src = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
            if (src.Contains("Brick Project Studio") && t.GetComponentInChildren<Renderer>() != null) furn.Add(t);
        }
        var walls = boxes.Where(r => IsWall(r.name)).ToList();
        foreach (var f in furn)
        {
            var fb = BoundsOf(f);
            string fn = f.name;
            bool wallMounted = fn.Contains("Shelf") || fn.Contains("Lamp") || fn.Contains("Window") || fn.Contains("Mirror") || fn.Contains("Shower Head") || fn.Contains("Holder");
            if (!wallMounted)
            {
                if (fb.min.y > 0.03f) findings.Add(F("floating", $"'{P(f)}' floats {fb.min.y:F2} m above the floor", fb));
                if (fb.min.y < -0.05f) findings.Add(F("sunk", $"'{P(f)}' sinks {-fb.min.y:F2} m below the floor", fb));
            }
            foreach (var w in walls)
            {
                var wb = w.bounds;
                float d = PenetrationDepth(fb, wb);
                if (d > 0.04f && !fn.Contains("Window"))
                    findings.Add(F("wall-penetration", $"'{P(f)}' pokes {d:F2} m into '{P(w.transform)}'", Merge(fb, wb)));
                if (fn.Contains("Bed") || fn.Contains("Wardrobe") || fn.Contains("Desk") || fn.Contains("Toilet"))
                {
                    float g = NearGap(fb, wb);
                    if (g > 0.004f && g < 0.2f) findings.Add(F("near-gap", $"'{P(f)}' is {g * 100f:F1} cm from '{P(w.transform)}'", Merge(fb, wb)));
                }
            }
        }
        for (int i = 0; i < furn.Count; i++)
            for (int j = i + 1; j < furn.Count; j++)
            {
                float d = PenetrationDepth(BoundsOf(furn[i]), BoundsOf(furn[j]));
                if (d > 0.08f) findings.Add(F("furniture-overlap", $"'{P(furn[i])}' overlaps '{P(furn[j])}' by {d:F2} m", Merge(BoundsOf(furn[i]), BoundsOf(furn[j]))));
            }

        // report: group by kind, collapse repeats (same names in many rooms)
        report.AppendLine($"renderers={all.Count} boxes={boxes.Count} kitFurniture={furn.Count} findings={findings.Count}");
        var grouped = findings.GroupBy(x => x.kind + "|" + Collapse(x.text)).ToList();
        foreach (var g in grouped.OrderBy(g => g.First().kind))
            report.AppendLine($"  [{g.First().kind}] x{g.Count()}  {Collapse(g.First().text)}");
        report.AppendLine();
        foreach (var x in findings.Where(x => x.kind != "z-fighting").Take(60)) raw.AppendLine("  [" + sname + "][" + x.kind + "] " + x.text);

        // renders for the first distinct findings
        var picks = grouped.Select(g => g.First()).Where(x => x.kind != "near-gap").Take(7)
            .Concat(grouped.Select(g => g.First()).Where(x => x.kind == "near-gap").Take(3)).ToList();
        int n = 0;
        foreach (var f in picks) CloseUp(sname + "_" + (n++) + "_" + f.kind, f.focus);
    }

    // ------------------------------------------------------------------ geometry helpers

    static bool IsBox(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;
        string mn = mf.sharedMesh.name;
        if (!(mn.StartsWith("box_") || mn == "Cube")) return false;
        var t = r.transform;
        return AxisOk(t.right) && AxisOk(t.up) && AxisOk(t.forward);
    }

    static bool AxisOk(Vector3 v) { return Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)) > 0.9995f; }
    static bool IsShellPart(string n) { return n.StartsWith("Wall") || n.StartsWith("Floor") || n.StartsWith("Ceiling") || n.StartsWith("Back ") || n.StartsWith("Far ") || n == "Pier" || n.StartsWith("Baseboard") || n.StartsWith("Partition") || n.StartsWith("Outer Wall") || n.StartsWith("Hall "); }
    static bool IsGlassLike(Renderer r) { var m = r.sharedMaterial; return m != null && m.name.StartsWith("Glass"); }
    static bool IsWall(string n) { return n.StartsWith("Wall") || n.StartsWith("Partition") || n.StartsWith("Outer Wall") || n.StartsWith("Back ") || n.StartsWith("Far ") || n == "Pier"; }

    // returns "x"/"y"/"z" if the two boxes share a same-direction face plane and overlap on that face
    static string CoplanarAxis(Bounds a, Bounds b)
    {
        const float eps = 0.0015f;
        for (int ax = 0; ax < 3; ax++)
        {
            bool sameMax = Mathf.Abs(a.max[ax] - b.max[ax]) < eps;
            bool sameMin = Mathf.Abs(a.min[ax] - b.min[ax]) < eps && !(ax == 1 && a.min[ax] < 0.06f); // bottoms resting on the floor are never seen
            if (!sameMax && !sameMin) continue;
            float along = Mathf.Min(a.max[ax], b.max[ax]) - Mathf.Max(a.min[ax], b.min[ax]);
            if (along < 0.002f) continue;
            bool ok = true;
            for (int o = 0; o < 3; o++)
            {
                if (o == ax) continue;
                if (Mathf.Min(a.max[o], b.max[o]) - Mathf.Max(a.min[o], b.min[o]) < 0.01f) ok = false;
            }
            if (ok) return "xyz"[ax].ToString();
        }
        return null;
    }

    static float PenetrationDepth(Bounds a, Bounds b)
    {
        float d = float.MaxValue;
        for (int ax = 0; ax < 3; ax++)
        {
            float o = Mathf.Min(a.max[ax], b.max[ax]) - Mathf.Max(a.min[ax], b.min[ax]);
            if (o <= 0) return 0f;
            d = Mathf.Min(d, o);
        }
        return d;
    }

    // horizontal gap between furniture and a wall box (only when they face each other: overlapping on y and one horizontal axis)
    static float NearGap(Bounds f, Bounds w)
    {
        float oy = Mathf.Min(f.max.y, w.max.y) - Mathf.Max(f.min.y, w.min.y);
        if (oy < 0.2f) return -1f;
        float ox = Mathf.Min(f.max.x, w.max.x) - Mathf.Max(f.min.x, w.min.x);
        float oz = Mathf.Min(f.max.z, w.max.z) - Mathf.Max(f.min.z, w.min.z);
        float best = -1f;
        if (oz > 0.3f) { float g = Mathf.Max(w.min.x - f.max.x, f.min.x - w.max.x); if (g > 0) best = g; }
        if (ox > 0.3f) { float g = Mathf.Max(w.min.z - f.max.z, f.min.z - w.max.z); if (g > 0 && (best < 0 || g < best)) best = g; }
        return best;
    }

    static Bounds BoundsOf(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Bounds Merge(Bounds a, Bounds b) { a.Encapsulate(b); return a; }
    static Finding F(string kind, string text, Bounds focus) { return new Finding { kind = kind, text = text, focus = focus }; }

    static string P(Transform t)
    {
        string s = t.name;
        for (int i = 0; i < 2 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    static string Generic(string n) { return System.Text.RegularExpressions.Regex.Replace(n, @"[\d\.\-\(\)]+|\s[LR]$|\s(Left|Right)$", "").Trim(); }

    // collapse room-specific numbers so 25 identical rooms report once
    static string Collapse(string s) { return System.Text.RegularExpressions.Regex.Replace(s, @"A\d{3}|\d+(\.\d+)?", "#"); }

    // ------------------------------------------------------------------ close-up render

    static void CloseUp(string name, Bounds focus)
    {
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.name == "Ceiling" || r.name == "Hall Ceiling") r.enabled = false;
        float size = Mathf.Max(focus.size.x, focus.size.y, focus.size.z, 0.6f);
        var g = new GameObject("_audit");
        var c = g.AddComponent<Camera>();
        c.fieldOfView = 45f; c.nearClipPlane = 0.02f; c.farClipPlane = 100f;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.1f, 0.11f, 0.13f);
        g.AddComponent<UniversalAdditionalCameraData>();
        g.transform.position = focus.center + new Vector3(0.7f, 0.9f, -0.7f).normalized * size * 2.0f;
        g.transform.LookAt(focus.center);
        var rt = new RenderTexture(900, 650, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        c.targetTexture = rt; c.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(900, 650, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 900, 650), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null; c.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(g);
    }
}
