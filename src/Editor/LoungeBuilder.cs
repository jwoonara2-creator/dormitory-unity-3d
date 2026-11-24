using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Dorm rest lounge (휴게실) built from the reference photos:
//   - frameless glass storefront with steel frame, glass door standing open (photo 1)
//   - beige vinyl floor, concrete ceiling with recessed downlights
//   - right wall cream with posted notices; left side + most of the back wall are black railings
//     with black grid windows
//   - two white round cafe tables with white chairs
//   - big glass-door shared fridge (blue "UNICOOLA" header) against the right wall near the back,
//     cabinet with microwave + router and a water dispenser beside it (photo 2)
// Everything sits under one "RestLounge" root, saved as Assets/Prefabs/RestLounge.prefab.
// Run: Unity -batchmode -projectPath <proj> -executeMethod LoungeBuilder.BuildAndCapture -captureDir <dir>
public static class LoungeBuilder
{
    const string PrefabPath = "Assets/Prefabs/RestLounge.prefab";
    const string MatDir = "Assets/Materials/Lounge";
    const string TexDir = "Assets/Textures/Lounge/";
    const string Kit = "Assets/Brick Project Studio/Apartment Kit/";
    const string RootName = "RestLounge";

    // interior: x in [-W/2, W/2], z in [0 (storefront), D (back)], y in [0, H]
    const float W = 3.6f, D = 5.0f, H = 2.5f;
    const float DoorHalf = 0.5f;          // door opening x in [-0.5, 0.5]
    const float CreamBackX0 = 0.9f;       // back wall: black windows for x < this, cream wall for x >= this

    public static void BuildAndCapture()
    {
        // work scene only: used to build the prefab, never saved (no scene file is created)
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Build();
        SaveAsPrefab();
        AssetDatabase.SaveAssets();
        CaptureAll(Arg("-captureDir") ?? "Captures");
    }

    static string Arg(string key)
    {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
        return null;
    }

    static void SaveAsPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var root = GameObject.Find(RootName);
        bool ok;
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction, out ok);
        if (!ok) Debug.LogError("LoungeBuilder: failed to save prefab " + PrefabPath);
    }

    // ---------------------------------------------------------------- build

    static void Build()
    {
        Directory.CreateDirectory(MatDir);
        GameObject old;
        while ((old = GameObject.Find(RootName)) != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;

        // ---- materials
        var floor = TexMat("FloorVinyl", TexDir + "FloorVinyl.png", new Color(1f, 1f, 1f), 0.45f);
        var ceil = TexMat("CeilingConcrete", TexDir + "CeilingConcrete.png", new Color(1f, 1f, 1f), 0.05f);
        var cream = Flat("WallCream", new Color(0.8f, 0.77f, 0.6f)); cream.SetFloat("_Smoothness", 0.1f);
        var black = Flat("Black", new Color(0.05f, 0.05f, 0.06f)); black.SetFloat("_Smoothness", 0.35f);
        var steel = Flat("Steel", new Color(0.72f, 0.73f, 0.75f)); steel.SetFloat("_Metallic", 0.85f); steel.SetFloat("_Smoothness", 0.6f);
        var hallFloor = Flat("HallFloor", new Color(0.3f, 0.3f, 0.32f));
        var glassClear = GlassMat("GlassClear", new Color(0.82f, 0.92f, 0.95f, 0.1f));
        var glassDark = GlassMat("GlassDark", new Color(0.04f, 0.06f, 0.08f, 0.6f));
        var night = Flat("Outside", new Color(0.1f, 0.12f, 0.15f), true);
        var white = Flat("White", new Color(0.93f, 0.93f, 0.92f)); white.SetFloat("_Smoothness", 0.5f);
        var grayLeg = Flat("ChairLeg", new Color(0.55f, 0.56f, 0.58f)); grayLeg.SetFloat("_Metallic", 0.8f); grayLeg.SetFloat("_Smoothness", 0.6f);
        var birch = TexMat("Birch", Kit + "Common/Textures/Surface/White Wood.png", new Color(0.9f, 0.8f, 0.66f), 0.3f);
        var light = Flat("DownlightGlow", new Color(1f, 1f, 0.95f));
        light.EnableKeyword("_EMISSION"); light.SetColor("_EmissionColor", new Color(2.2f, 2.2f, 2.0f));
        var header = TexMat("FridgeHeader", TexDir + "FridgeHeader.png", Color.white, 0.5f);
        var fridgeSide = Flat("FridgeSide", new Color(0.92f, 0.92f, 0.93f)); fridgeSide.SetFloat("_Smoothness", 0.5f);
        var shelfMat = Flat("FridgeShelf", new Color(0.85f, 0.87f, 0.88f)); shelfMat.SetFloat("_Metallic", 0.4f);
        var fridgeInside = Flat("FridgeInside", new Color(0.8f, 0.82f, 0.84f));
        var notice1 = TexMat("Notice1", TexDir + "Notice1.png", Color.white, 0.2f);
        var notice2 = TexMat("Notice2", TexDir + "Notice2.png", Color.white, 0.2f);
        var microDark = Flat("MicroDark", new Color(0.03f, 0.03f, 0.04f)); microDark.SetFloat("_Smoothness", 0.8f);

        // ---- shell
        var shell = Group("Shell", root);
        float cz = D / 2f;
        TBox("Floor", shell, new Vector3(0, -0.05f, cz), new Vector3(W, 0.1f, D), floor, 1.2f);
        TBox("Ceiling", shell, new Vector3(0, H + 0.05f, cz), new Vector3(W + 0.2f, 0.1f, D + 0.2f), ceil, 1.5f);
        TBox("Wall Right", shell, new Vector3(W / 2 + 0.05f, H / 2, cz), new Vector3(0.1f, H, D + 0.2f), cream, 1f);
        TBox("Wall Back Cream", shell, new Vector3((CreamBackX0 + W / 2) / 2, H / 2, D + 0.05f), new Vector3(W / 2 - CreamBackX0 + 0.1f, H, 0.1f), cream, 1f);
        TBox("Baseboard Right", shell, new Vector3(W / 2 - 0.01f, 0.05f, cz), new Vector3(0.02f, 0.1f, D), Flat("Baseboard", new Color(0.7f, 0.67f, 0.52f)), 1f);

        // railings + black grid windows: left wall (running along z) and back wall (running along x)
        RailWall("Left Windows", shell, new Vector3(-W / 2 - 0.02f, 0, 0), -90f, D, black, glassDark);
        RailWall("Back Windows", shell, new Vector3(-W / 2, 0, D + 0.02f), 0f, CreamBackX0 + W / 2, black, glassDark);
        var outside = Group("Outside", root);
        Quad("Night Left", outside, new Vector3(-W / 2 - 1.4f, 1.2f, cz), 270f, new Vector2(9f, 3f), night);
        Quad("Night Back", outside, new Vector3(-0.4f, 1.2f, D + 1.4f), 0f, new Vector2(9f, 3f), night);

        // ---- storefront (glass + steel frame) with an open glass door
        var front = Group("Storefront", root);
        float postW = 0.07f;
        foreach (float px in new[] { -W / 2, -DoorHalf, DoorHalf, W / 2 })
            TBox("Post", front, new Vector3(px, (H - 0.01f) / 2, 0f), new Vector3(postW, H - 0.01f, 0.12f), steel, 1f);
        TBox("Header Beam", front, new Vector3(0, 2.28f, 0f), new Vector3(W, 0.44f, 0.14f), steel, 1f);
        TBox("Kick Rail L", front, new Vector3((-W / 2 - DoorHalf) / 2, 0.06f, 0f), new Vector3(W / 2 - DoorHalf, 0.12f, 0.12f), steel, 1f);
        TBox("Kick Rail R", front, new Vector3((W / 2 + DoorHalf) / 2, 0.06f, 0f), new Vector3(W / 2 - DoorHalf, 0.12f, 0.12f), steel, 1f);
        TBox("Threshold", front, new Vector3(0, 0.01f, 0f), new Vector3(DoorHalf * 2, 0.02f, 0.14f), steel, 1f);
        TBox("Glass L", front, new Vector3((-W / 2 - DoorHalf) / 2, 1.04f, 0f), new Vector3(W / 2 - DoorHalf - postW, 2.0f, 0.012f), glassClear, 1f);
        TBox("Glass R", front, new Vector3((W / 2 + DoorHalf) / 2, 1.04f, 0f), new Vector3(W / 2 - DoorHalf - postW, 2.0f, 0.012f), glassClear, 1f);
        // door leaf hinged at the left post, swung inward
        var leaf = Group("Glass Door", front);
        leaf.localPosition = new Vector3(-DoorHalf + 0.04f, 0f, 0.1f);
        leaf.localEulerAngles = new Vector3(0, -72f, 0);
        TBox("Door Glass", leaf, new Vector3(0.45f, 1.05f, 0f), new Vector3(0.88f, 2.05f, 0.014f), glassClear, 1f);
        TBox("Door Top Rail", leaf, new Vector3(0.45f, 2.08f, 0f), new Vector3(0.9f, 0.06f, 0.04f), steel, 1f);
        TBox("Door Bottom Rail", leaf, new Vector3(0.45f, 0.04f, 0f), new Vector3(0.9f, 0.08f, 0.04f), steel, 1f);
        Cyl("Handle A", leaf, new Vector3(0.78f, 1.05f, 0.05f), 0.014f, 0.6f, steel);
        Cyl("Handle B", leaf, new Vector3(0.78f, 1.05f, -0.05f), 0.014f, 0.6f, steel);
        // hallway outside
        TBox("Hall Floor", front, new Vector3(0, -0.05f, -1.8f), new Vector3(W + 1f, 0.1f, 3.6f), hallFloor, 1f);
        TBox("Hall Ceiling", front, new Vector3(0, H + 0.05f, -1.8f), new Vector3(W + 1f, 0.1f, 3.6f), ceil, 1.5f);

        // ---- notices on the right wall (near the entrance)
        var notices = Group("Notices", root);
        Quad("Notice 1", notices, new Vector3(W / 2 - 0.006f, 1.62f, 1.25f), 90f, new Vector2(0.46f, 0.38f), notice1);
        Quad("Notice 2", notices, new Vector3(W / 2 - 0.006f, 1.2f, 1.25f), 90f, new Vector2(0.46f, 0.44f), notice2);
        Quad("Notice 3", notices, new Vector3(W / 2 - 0.006f, 1.45f, 1.85f), 90f, new Vector2(0.3f, 0.4f), notice2);
        TBox("Switch Plate", notices, new Vector3(W / 2 - 0.012f, 1.0f, 1.55f), new Vector3(0.024f, 0.09f, 0.09f), black, 1f);

        // ---- cafe tables + white chairs
        var furn = Group("Furniture", root);
        BuildTable(furn, "Table A", new Vector3(-0.45f, 0f, 1.9f), white, grayLeg);
        BuildChair(furn, "Chair A1", new Vector3(-0.95f, 0f, 1.75f), 65f, white, grayLeg);
        BuildChair(furn, "Chair A2", new Vector3(-0.2f, 0f, 1.35f), 200f, white, grayLeg);
        BuildChair(furn, "Chair A3", new Vector3(-0.2f, 0f, 2.45f), 335f, white, grayLeg);
        BuildTable(furn, "Table B", new Vector3(0.1f, 0f, 3.5f), white, grayLeg);
        BuildChair(furn, "Chair B1", new Vector3(-0.4f, 0f, 3.75f), 80f, white, grayLeg);
        BuildChair(furn, "Chair B2", new Vector3(0.1f, 0f, 4.05f), 180f, white, grayLeg);

        // ---- shared fridge (right wall, near the back), door faces the room (-x)
        BuildFridge(root, new Vector3(W / 2 - 0.4f, 0f, 2.85f), black, fridgeSide, shelfMat, fridgeInside, glassClear, header, notice2);

        // ---- cabinet + microwave + router, and water dispenser, along the back wall
        var corner = Group("Cabinet And Dispenser", root);
        corner.localPosition = new Vector3(W / 2 - 0.23f, 0f, 4.06f); // back of the cabinet touches the right wall
        corner.localEulerAngles = new Vector3(0, 90f, 0);              // front (microwave) faces into the room (-x)
        float cx = 0f, cabZ = 0f;
        TBox("Cabinet Lower", corner, new Vector3(cx, 0.3f, cabZ), new Vector3(0.82f, 0.6f, 0.46f), birch, 1f);
        TBox("Cabinet Door Seam", corner, new Vector3(cx, 0.3f, cabZ - 0.232f), new Vector3(0.006f, 0.56f, 0.006f), black, 1f);
        TBox("Cabinet Handle L", corner, new Vector3(cx - 0.05f, 0.5f, cabZ - 0.236f), new Vector3(0.05f, 0.02f, 0.01f), steel, 1f);
        TBox("Cabinet Handle R", corner, new Vector3(cx + 0.05f, 0.5f, cabZ - 0.236f), new Vector3(0.05f, 0.02f, 0.01f), steel, 1f);
        // open niche (front left open) so the microwave is visible: two sides, top, bottom, back
        TBox("Niche Side L", corner, new Vector3(cx - 0.395f, 0.8f, cabZ), new Vector3(0.03f, 0.4f, 0.46f), white, 1f);
        TBox("Niche Side R", corner, new Vector3(cx + 0.395f, 0.8f, cabZ), new Vector3(0.03f, 0.4f, 0.46f), white, 1f);
        TBox("Niche Top", corner, new Vector3(cx, 0.985f, cabZ), new Vector3(0.82f, 0.03f, 0.46f), white, 1f);
        TBox("Niche Bottom", corner, new Vector3(cx, 0.615f, cabZ), new Vector3(0.82f, 0.03f, 0.46f), white, 1f);
        TBox("Niche Back", corner, new Vector3(cx, 0.8f, cabZ + 0.22f), new Vector3(0.82f, 0.4f, 0.02f), fridgeInside, 1f);
        TBox("Cabinet Top", corner, new Vector3(cx, 1.015f, cabZ), new Vector3(0.84f, 0.03f, 0.48f), birch, 1f);
        // microwave in the niche
        TBox("Microwave", corner, new Vector3(cx - 0.06f, 0.78f, cabZ - 0.05f), new Vector3(0.46f, 0.28f, 0.34f), white, 1f);
        TBox("Microwave Window", corner, new Vector3(cx - 0.12f, 0.78f, cabZ - 0.224f), new Vector3(0.28f, 0.2f, 0.01f), microDark, 1f);
        TBox("Microwave Panel", corner, new Vector3(cx + 0.11f, 0.78f, cabZ - 0.224f), new Vector3(0.1f, 0.24f, 0.01f), black, 1f);
        // router on top
        TBox("Router", corner, new Vector3(cx + 0.1f, 1.045f, cabZ + 0.02f), new Vector3(0.2f, 0.03f, 0.13f), black, 1f);
        Cyl("Antenna L", corner, new Vector3(cx + 0.03f, 1.12f, cabZ + 0.04f), 0.006f, 0.13f, black, new Vector3(0, 0, 8f));
        Cyl("Antenna R", corner, new Vector3(cx + 0.17f, 1.12f, cabZ + 0.04f), 0.006f, 0.13f, black, new Vector3(0, 0, -8f));
        // water dispenser (slim, white with black stripe)
        float dx = -0.7f, dz = 0.08f; // dispenser next to the cabinet, toward the back corner
        TBox("Dispenser Body", corner, new Vector3(dx, 0.52f, dz), new Vector3(0.3f, 1.04f, 0.3f), white, 1f);
        TBox("Dispenser Stripe", corner, new Vector3(dx + 0.145f, 0.52f, dz), new Vector3(0.025f, 1.0f, 0.28f), black, 1f);
        TBox("Dispenser Panel", corner, new Vector3(dx - 0.02f, 0.82f, dz - 0.152f), new Vector3(0.2f, 0.22f, 0.012f), fridgeInside, 1f);
        TBox("Dispenser Tray", corner, new Vector3(dx - 0.02f, 0.7f, dz - 0.17f), new Vector3(0.2f, 0.02f, 0.05f), grayLeg, 1f);
        TBox("Dispenser Spout", corner, new Vector3(dx - 0.05f, 0.77f, dz - 0.16f), new Vector3(0.025f, 0.02f, 0.03f), black, 1f);
        Cyl("Bottle", corner, new Vector3(0.1f + cx - 0.1f, 1.19f, cabZ - 0.08f), 0.035f, 0.16f, Flat("BottleGreen", new Color(0.15f, 0.4f, 0.25f)));

        // ---- recessed downlights + lights
        var lights = Group("Lights", root);
        foreach (float lx in new[] { -1.0f, 0.2f, 1.2f })
            foreach (float lz in new[] { 0.9f, 2.5f, 4.1f })
                Cyl("Downlight", lights, new Vector3(lx, H - 0.004f, lz), 0.065f, 0.01f, light);
        AddLight("Light Front", lights, new Vector3(0f, H - 0.3f, 1.2f), new Color(1f, 0.97f, 0.9f), 1.5f, 7f, LightShadows.Soft);
        AddLight("Light Mid", lights, new Vector3(0.2f, H - 0.3f, 3.0f), new Color(1f, 0.97f, 0.9f), 1.5f, 7f, LightShadows.Soft);
        AddLight("Light Back", lights, new Vector3(0.6f, H - 0.3f, 4.4f), new Color(1f, 0.97f, 0.9f), 1.3f, 6f, LightShadows.None);

        var sun = GameObject.Find("Directional Light");
        if (sun != null) { var l = sun.GetComponent<Light>(); l.intensity = 0.1f * LightTuning.SunScale; l.shadows = LightShadows.None; }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = LightTuning.Amb(new Color(0.36f, 0.36f, 0.35f));

        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            cam.transform.SetPositionAndRotation(new Vector3(0.1f, 1.45f, -1.2f), Quaternion.identity);
            cam.transform.LookAt(new Vector3(0.1f, 1.2f, 3.5f));
            var c = cam.GetComponent<Camera>();
            c.fieldOfView = 72f; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
        }
    }

    // black railing (lower) + black grid window (upper). Group runs along local +x, thickness along local z.
    static void RailWall(string name, Transform parent, Vector3 pos, float yaw, float len, Material black, Material glassDark)
    {
        var g = Group(name, parent);
        g.localPosition = pos;
        g.localEulerAngles = new Vector3(0, yaw, 0);
        TBox("Curb", g, new Vector3(len / 2, 0.06f, 0), new Vector3(len, 0.12f, 0.1f), black, 1f);
        int bars = Mathf.FloorToInt(len / 0.11f);
        for (int i = 0; i <= bars; i++)
            TBox("Bar", g, new Vector3(i * (len / bars), 0.57f, 0), new Vector3(0.018f, 0.9f, 0.018f), black, 1f);
        TBox("Rail Top", g, new Vector3(len / 2, 1.03f, 0), new Vector3(len, 0.045f, 0.06f), black, 1f);
        TBox("Rail Low", g, new Vector3(len / 2, 0.4f, 0), new Vector3(len, 0.02f, 0.02f), black, 1f);
        // window grid above the railing
        TBox("Sill", g, new Vector3(len / 2, 1.1f, 0), new Vector3(len, 0.08f, 0.12f), black, 1f);
        TBox("Head", g, new Vector3(len / 2, H - 0.05f, 0), new Vector3(len, 0.1f, 0.12f), black, 1f);
        TBox("Mid Rail", g, new Vector3(len / 2, 1.85f, 0), new Vector3(len, 0.05f, 0.08f), black, 1f);
        int mull = Mathf.Max(2, Mathf.RoundToInt(len / 0.95f));
        for (int i = 0; i <= mull; i++)
            TBox("Mullion", g, new Vector3(i * (len / mull), (1.14f + H - 0.1f) / 2f, 0), new Vector3(0.06f, H - 1.24f, 0.1f), black, 1f);
        TBox("Window Glass", g, new Vector3(len / 2, (1.14f + H - 0.1f) / 2f, 0), new Vector3(len, H - 1.24f, 0.01f), glassDark, 1f);
    }

    static void BuildTable(Transform parent, string name, Vector3 pos, Material white, Material leg)
    {
        var t = Group(name, parent);
        t.localPosition = pos;
        Cyl("Top", t, new Vector3(0, 0.74f, 0), 0.38f, 0.03f, white);
        Cyl("Pole", t, new Vector3(0, 0.37f, 0), 0.03f, 0.72f, white);
        Cyl("Base", t, new Vector3(0, 0.012f, 0), 0.22f, 0.024f, white);
    }

    // stand-in for the white shell chair: seat, tilted back, 4 splayed legs
    static void BuildChair(Transform parent, string name, Vector3 pos, float yaw, Material white, Material leg)
    {
        var c = Group(name, parent);
        c.localPosition = pos;
        c.localEulerAngles = new Vector3(0, yaw, 0);
        TBox("Seat", c, new Vector3(0, 0.45f, 0), new Vector3(0.4f, 0.04f, 0.4f), white, 1f);
        var back = TBox("Back", c, new Vector3(0, 0.7f, 0.2f), new Vector3(0.38f, 0.32f, 0.035f), white, 1f);
        back.transform.localEulerAngles = new Vector3(-10f, 0, 0);
        foreach (var o in new[] { new Vector2(-0.16f, -0.16f), new Vector2(0.16f, -0.16f), new Vector2(-0.16f, 0.16f), new Vector2(0.16f, 0.16f) })
        {
            var l = Cyl("Leg", c, new Vector3(o.x * 1.1f, 0.22f, o.y * 1.1f), 0.01f, 0.45f, leg);
            l.transform.localEulerAngles = new Vector3(o.y > 0 ? -6f : 6f, 0, o.x > 0 ? 6f : -6f);
        }
    }

    // large glass-door display fridge; front (door) faces -x
    static void BuildFridge(Transform root, Vector3 pos, Material black, Material side, Material shelf, Material inside, Material glass, Material header, Material notice)
    {
        var f = Group("Shared Fridge", root);
        f.localPosition = pos;
        float dx = 0.7f, wz = 1.25f; // depth (x) and width (z)
        float y0 = 0.2f, y1 = 1.8f;
        TBox("Base Vents", f, new Vector3(0, 0.1f, 0), new Vector3(dx, 0.2f, wz), black, 1f);
        for (int i = 0; i < 4; i++)
            TBox("Vent Slat", f, new Vector3(-dx / 2 - 0.004f, 0.04f + i * 0.045f, 0), new Vector3(0.01f, 0.025f, wz - 0.1f), Flat("VentSlat", new Color(0.02f, 0.02f, 0.02f)), 1f);
        // shell: white sides, back, top; interior visible through the glass
        TBox("Side Left", f, new Vector3(0, (y0 + y1) / 2, -wz / 2 + 0.015f), new Vector3(dx, y1 - y0, 0.03f), side, 1f);
        TBox("Side Right", f, new Vector3(0, (y0 + y1) / 2, wz / 2 - 0.015f), new Vector3(dx, y1 - y0, 0.03f), side, 1f);
        TBox("Back", f, new Vector3(dx / 2 - 0.015f, (y0 + y1) / 2, 0), new Vector3(0.03f, y1 - y0, wz), inside, 1f);
        TBox("Top", f, new Vector3(0, y1 - 0.015f, 0), new Vector3(dx, 0.03f, wz), side, 1f);
        TBox("Bottom", f, new Vector3(0, y0 + 0.015f, 0), new Vector3(dx, 0.03f, wz), inside, 1f);
        // front frame (black) + two sliding glass doors
        float fx = -dx / 2 + 0.02f;
        TBox("Frame Top", f, new Vector3(fx - 0.006f, y1 - 0.0515f, 0), new Vector3(0.052f, 0.097f, wz - 0.006f), black, 1f);
        TBox("Frame Bottom", f, new Vector3(fx - 0.006f, y0 + 0.0515f, 0), new Vector3(0.052f, 0.097f, wz - 0.006f), black, 1f);
        foreach (float zz in new[] { -wz / 2 + 0.03f, 0f, wz / 2 - 0.03f })
            TBox("Frame Stile", f, new Vector3(fx, (y0 + y1) / 2, zz * 0.995f), new Vector3(0.05f, y1 - y0 - 0.2f, 0.056f), black, 1f);
        TBox("Glass Door 1", f, new Vector3(fx - 0.01f, (y0 + y1) / 2, -wz / 4), new Vector3(0.012f, y1 - y0 - 0.2f, wz / 2 - 0.08f), glass, 1f);
        TBox("Glass Door 2", f, new Vector3(fx - 0.03f, (y0 + y1) / 2, wz / 4), new Vector3(0.012f, y1 - y0 - 0.2f, wz / 2 - 0.08f), glass, 1f);
        // shelves + goods
        var rnd = new System.Random(11);
        var goods = new[] { new Color(0.8f, 0.15f, 0.12f), new Color(0.2f, 0.4f, 0.8f), new Color(0.85f, 0.85f, 0.8f), new Color(0.2f, 0.55f, 0.3f), new Color(0.9f, 0.7f, 0.2f), new Color(0.7f, 0.7f, 0.72f) };
        int gi = 0;
        foreach (float sy in new[] { 0.55f, 0.9f, 1.25f, 1.58f })
        {
            TBox("Shelf", f, new Vector3(0.02f, sy, 0), new Vector3(dx - 0.1f, 0.012f, wz - 0.1f), shelf, 1f);
            for (int i = 0; i < 6; i++)
            {
                var m = Flat("Goods" + (gi++ % goods.Length), goods[gi % goods.Length]);
                float zz = -wz / 2 + 0.12f + (float)rnd.NextDouble() * (wz - 0.24f);
                float xx = -0.15f + (float)rnd.NextDouble() * 0.3f;
                if (rnd.NextDouble() < 0.5)
                    Cyl("Can", f, new Vector3(xx, sy + 0.07f, zz), 0.032f, 0.13f, m);
                else
                    TBox("Box", f, new Vector3(xx, sy + 0.08f, zz), new Vector3(0.14f + (float)rnd.NextDouble() * 0.06f, 0.14f, 0.12f), m, 1f);
            }
        }
        // blue header sign on top (texture on the front face)
        var sign = TBox("Header Sign", f, new Vector3(0, y1 + 0.15f, 0), new Vector3(dx + 0.02f, 0.3f, wz + 0.02f), black, 1f);
        Quad("Header Face", f, new Vector3(-dx / 2 - 0.02f, y1 + 0.15f, 0), 90f, new Vector2(wz, 0.3f), header);
        // notice sheet on the door glass
        Quad("Fridge Notice", f, new Vector3(-dx / 2 - 0.04f, 1.55f, wz / 4), 90f, new Vector2(0.4f, 0.34f), notice);
    }

    // -------------------------------------------------------------- helpers

    static Transform Group(string name, Transform parent)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    static GameObject TBox(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, float tile, Vector3? euler = null)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.AddComponent<MeshFilter>().sharedMesh = MeshStore.Box(size, tile);
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
        return g;
    }

    static Mesh BoxMesh(Vector3 s, float tile)
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

    // yaw: 0 = faces -z, 90 = faces -x, 270 = faces +x
    static GameObject Quad(string name, Transform parent, Vector3 pos, float yaw, Vector2 size, Material mat)
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

    static Material TexMat(string name, string albedo, Color tint, float smooth)
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

    static Material Flat(string name, Color c, bool unlit = false)
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

    static Material GlassMat(string name, Color c)
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

        var chair = TempCam(new Vector3(-1.4f, 1.15f, 1.9f), Quaternion.identity, 74f);
        chair.transform.LookAt(new Vector3(1.4f, 0.9f, 3.6f));
        Shoot("2_from_chair", chair, 700, 900, dir);

        var back = TempCam(new Vector3(1.0f, 1.5f, D - 0.5f), Quaternion.identity, 72f);
        back.transform.LookAt(new Vector3(-0.2f, 1.1f, 0.3f));
        Shoot("3_toward_door", back, 800, 600, dir);

        var close = TempCam(new Vector3(-0.1f, 1.35f, 3.2f), Quaternion.identity, 62f);
        close.transform.LookAt(new Vector3(1.5f, 0.85f, 4.2f));
        Shoot("4_cabinet", close, 800, 600, dir);

        var top = TempCam(new Vector3(0f, 7.5f, D / 2f), Quaternion.Euler(90f, 0f, 0f), 50f);
        var ceilGo = GameObject.Find("Ceiling");
        var hallCeil = GameObject.Find("Hall Ceiling");
        if (ceilGo != null) ceilGo.GetComponent<Renderer>().enabled = false;
        if (hallCeil != null) hallCeil.GetComponent<Renderer>().enabled = false;
        Shoot("5_top", top, 800, 900, dir);
        if (ceilGo != null) ceilGo.GetComponent<Renderer>().enabled = true;
        if (hallCeil != null) hallCeil.GetComponent<Renderer>().enabled = true;

        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (c.name == "_CaptureCam") Object.DestroyImmediate(c.gameObject);
    }

    static Camera TempCam(Vector3 pos, Quaternion rot, float fov)
    {
        var g = new GameObject("_CaptureCam");
        g.transform.SetPositionAndRotation(pos, rot);
        var c = g.AddComponent<Camera>();
        c.fieldOfView = fov; c.nearClipPlane = 0.03f; c.farClipPlane = 50f;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
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
