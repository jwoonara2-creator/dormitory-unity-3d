using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Navigation scenes + UI:
//   RoomView_1Person / _2Person / _Accessible : a room-with-bathroom prefab, look-around camera and
//       buttons "화장실 가기" / "방으로" (camera glides through the bathroom door) and "← 층 보기"
//   FloorView / WomenFloorView : tap a room -> "A201 호실 [들어가기]" panel -> opens RoomView_1Person
// The flow scenes are registered in Build Settings (RegisterScenes) so SceneManager.LoadScene works in a build.
public static class NavBuilder
{
    class RoomType { public string scene, prefab; public float hw; }
    static readonly RoomType[] Types =
    {
        new RoomType { scene = "RoomView_1Person",   prefab = "Assets/Prefabs/RoomWithBath_1Person.prefab",   hw = 1.15f },
        new RoomType { scene = "RoomView_2Person",   prefab = "Assets/Prefabs/RoomWithBath_2Person.prefab",   hw = 1.5f },
        new RoomType { scene = "RoomView_Accessible", prefab = "Assets/Prefabs/RoomWithBath_Accessible.prefab", hw = 1.6f },
    };

    static readonly Color Blue = new Color(0.2f, 0.52f, 1f, 0.96f);
    static readonly Color Dark = new Color(0.07f, 0.09f, 0.14f, 0.95f);
    const string RoundedPath = "Assets/Textures/UI/Rounded.png";

    // ------------------------------------------------------------------ room views

    public static void BuildRoomViews()
    {
        foreach (var t in Types) BuildRoomView(t);
        RegisterScenes();
    }

    static void BuildRoomView(RoomType t)
    {
        string path = "Assets/Scenes/" + t.scene + ".unity";
        Scene scene;
        if (File.Exists(path)) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
        }
        foreach (var n in new[] { "RoomView", "UI", "RoomViewController", "EventSystem" })
        {
            GameObject old;
            while ((old = GameObject.Find(n)) != null) Object.DestroyImmediate(old);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(t.prefab);
        var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        room.name = "RoomView";
        room.transform.position = Vector3.zero;

        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(38f, 205f, 0f);
            var l = sun.GetComponent<Light>(); l.intensity = 1.0f * LightTuning.SunScale; l.color = new Color(1f, 0.98f, 0.94f); l.shadows = LightShadows.Soft;
        }
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = LightTuning.Amb(new Color(0.55f, 0.55f, 0.56f));

        // viewpoints (combo frame: room x in [-hw,hw], z from the entrance wall; bathroom turned 90 degrees at x = hw+0.1, z = 1.05)
        float hw = t.hw, ox = hw + 0.1f, oz = 1.05f;
        var roomPos = new Vector3(0f, 1.5f, 0.45f);
        var bathPos = new Vector3(ox + 0.4f, 1.5f, oz - 0.35f);
        var door1 = new Vector3(hw - 0.5f, 1.5f, oz - 0.3f);
        var door2 = new Vector3(hw + 0.05f, 1.5f, oz - 0.3f);
        float roomYaw = 0f, roomPitch = 6f, bathYaw = 90f, bathPitch = 8f;

        var camObj = GameObject.Find("Main Camera");
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(camObj);
        var cam = camObj.GetComponent<Camera>();
        camObj.transform.SetPositionAndRotation(roomPos, Quaternion.Euler(roomPitch, roomYaw, 0f));
        cam.fieldOfView = 66f; cam.nearClipPlane = 0.03f; cam.farClipPlane = 80f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.1f, 0.11f, 0.13f);
        RoomLookCamera.AttachTo(camObj);
        var look = camObj.GetComponent<RoomLookCamera>();

        // UI (small, rounded, inside the safe area)
        var canvas = MakeCanvas("UI");
        var ui = SafeRoot(canvas);
        var back = MakeButton(ui, "BackButton", "‹  층 보기", 21, Dark, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16, -14), new Vector2(136, 44));
        var titleBg = MakePanel(ui, "TitleBg", Dark, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(190, 44), false);
        var title = MakeText(titleBg.transform, "Title", "원룸", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
        var bath = MakeButton(ui, "BathButton", "화장실 가기", 21, Blue, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18, 20), new Vector2(150, 46));
        var toRoom = MakeButton(ui, "RoomButton", "방으로", 21, Blue, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18, 20), new Vector2(150, 46));
        EnsureEventSystem();

        var ctrlObj = new GameObject("RoomViewController");
        var ctrl = ctrlObj.AddComponent<RoomViewController>();
        ctrl.look = look; ctrl.titleLabel = title; ctrl.bathButton = bath; ctrl.roomButton = toRoom; ctrl.backButton = back;
        ctrl.pathToBath = new[] { door1, door2, bathPos };
        ctrl.pathToRoom = new[] { door2, door1, roomPos };
        ctrl.roomYaw = roomYaw; ctrl.roomPitch = roomPitch; ctrl.bathYaw = bathYaw; ctrl.bathPitch = bathPitch;
        toRoom.gameObject.SetActive(false);

        EditorSceneManager.SaveScene(scene);
        Capture(t, cam, canvas, look, ctrl);
    }

    // ------------------------------------------------------------------ floor views

    // adds the three room-type buttons to the scene that is currently open
    // (floor views and corridor scenes; called by their builders)
    public static void AddRoomEntry()
    {
        foreach (var n in new[] { "UI", "EventSystem" })
        {
            GameObject old;
            while ((old = GameObject.Find(n)) != null) Object.DestroyImmediate(old);
        }
        var camObj = GameObject.Find("Main Camera");
        if (camObj != null) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(camObj);   // leftovers from earlier builds
        var canvas = MakeCanvas("UI");
        var ui = SafeRoot(canvas);

        var panel = MakePanel(ui, "EntryPanel", Dark, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(440, 66), true);
        var b1 = MakeButton(panel.transform, "OnePerson", "1인실", 21, Blue, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10, 0), new Vector2(130, 46));
        var b2 = MakeButton(panel.transform, "TwoPerson", "2인실", 21, Blue, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(130, 46));
        var b3 = MakeButton(panel.transform, "Accessible", "장애인실", 21, Blue, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10, 0), new Vector2(130, 46));
        EnsureEventSystem();

        var entry = canvas.gameObject.AddComponent<RoomEntryButtons>();
        entry.onePerson = b1; entry.twoPerson = b2; entry.accessible = b3;
    }

    // ------------------------------------------------------------------ UI factory

    public static Canvas MakeCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        go.AddComponent<KoreanFont>();
        var safe = new GameObject("SafeArea", typeof(RectTransform));
        safe.transform.SetParent(go.transform, false);
        var srt = (RectTransform)safe.transform;
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
        safe.AddComponent<SafeAreaFitter>();
        return canvas;
    }

    public static Transform SafeRoot(Canvas canvas) { return canvas.transform.Find("SafeArea"); }

    // 9-sliced rounded rectangle sprite (generated PNG, import settings enforced here)
    public static Sprite RoundedSprite()
    {
        var imp = AssetImporter.GetAtPath(RoundedPath) as TextureImporter;
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spriteBorder.x < 1f))
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = new Vector4(100, 100, 100, 100);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedPath);
    }

    static void Round(Image img)
    {
        img.sprite = RoundedSprite();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 3.2f;   // keeps the corner radius around 30 px at the reference size
    }

    static Font DefaultFont { get { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } }

    static RectTransform Place(GameObject go, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    public static Text MakeText(Transform parent, string name, string text, int size, TextAnchor anchor, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Place(go, amin, amax, pivot, pos, sizeDelta);
        var t = go.AddComponent<Text>();
        t.text = text; t.font = DefaultFont; t.fontSize = size; t.alignment = anchor; t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static GameObject MakePanel(Transform parent, string name, Color color, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size, bool blocksRaycast)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Place(go, amin, amax, pivot, pos, size);
        var img = go.AddComponent<Image>();
        Round(img);
        img.color = color; img.raycastTarget = blocksRaycast;
        return go;
    }

    public static Button MakeButton(Transform parent, string name, string label, int fontSize, Color color, Vector2 amin, Vector2 amax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Place(go, amin, amax, pivot, pos, size);
        var img = go.AddComponent<Image>();
        Round(img);
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f); colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); colors.selectedColor = Color.white;
        btn.colors = colors;
        MakeText(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
        return btn;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    // ------------------------------------------------------------------ build settings

    // Build Settings = exactly the scenes of the screen flow (see README). First entry is the start scene.
    public static readonly string[] FlowScenes =
    {
        "FloorView", "WomenFloorView", "Corridor", "WomenCorridor",
        "RoomView_1Person", "RoomView_2Person", "RoomView_Accessible",
    };

    public static void RegisterScenes()
    {
        EditorBuildSettings.scenes = FlowScenes
            .Select(n => "Assets/Scenes/" + n + ".unity")
            .Where(File.Exists)
            .Select(p => new EditorBuildSettingsScene(p, true))
            .ToArray();
    }

    // ------------------------------------------------------------------ verification renders

    // Overlay canvases are not drawn by Camera.Render, so for screenshots the canvas is switched to camera space.
    public static void RenderWithUi(Camera cam, Canvas canvas, string file, int w, int h)
    {
        var font = UiUtil.CreateKoreanFont(32);
        if (font != null) foreach (var t in canvas.GetComponentsInChildren<Text>(true)) t.font = font;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = cam.nearClipPlane + 0.07f;
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prevT = cam.targetTexture; var prevA = RenderTexture.active;
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        cam.targetTexture = prevT; RenderTexture.active = prevA;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    static void Capture(RoomType t, Camera cam, Canvas canvas, RoomLookCamera look, RoomViewController ctrl)
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-captureDir");
        if (i < 0) return;
        string dir = args[i + 1];
        Directory.CreateDirectory(dir);
        var keepPos = cam.transform.position; var keepRot = cam.transform.rotation;
        RenderWithUi(cam, canvas, Path.Combine(dir, "nav_" + t.scene + "_room.png"), 1280, 720);
        RenderWithUi(cam, canvas, Path.Combine(dir, "nav_" + t.scene + "_room_portrait.png"), 540, 960);

        // bathroom viewpoint with the other button visible
        cam.transform.SetPositionAndRotation(ctrl.pathToBath[2], Quaternion.Euler(ctrl.bathPitch, ctrl.bathYaw, 0f));
        ctrl.bathButton.gameObject.SetActive(false); ctrl.roomButton.gameObject.SetActive(true);
        RenderWithUi(cam, canvas, Path.Combine(dir, "nav_" + t.scene + "_bath.png"), 1280, 720);
        ctrl.bathButton.gameObject.SetActive(true); ctrl.roomButton.gameObject.SetActive(false);
        cam.transform.SetPositionAndRotation(keepPos, keepRot);
    }
}
