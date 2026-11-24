using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Small fixes applied to every scene that loads (called from AppBridge):
//  1. every legacy Text gets the bundled Hangul font (WebGL has no OS fonts, so Korean titles were blank)
//  2. floor / corridor scenes get a back arrow so the user can get back to the Main map
//  3. runtime-built buttons are round icon buttons (back arrow, view from above, ...) instead of text boxes
public static class AppUiFixes
{
    const string MainScene = "Main";

    // floor / corridor views and the top views of the laundry / gym / seminar rooms have no way back to Main
    static readonly string[] ScenesWithoutBack =
    {
        "FloorView", "WomenFloorView", "Corridor", "WomenCorridor",
        "LaundryRoomView", "GymRoomView", "StudyRoomView",
    };

    static readonly string[] BedroomScenes = { "RoomView_1Person", "RoomView_2Person", "RoomView_Accessible" };

    // laundry / gym / seminar: "<name>" is the first-person room, "<name>View" is the same room seen from above
    static readonly string[] FacilityRooms = { "LaundryRoom", "GymRoom", "StudyRoom" };

    // look of every round button
    static readonly Color ButtonColor = new Color(0.1f, 0.12f, 0.16f, 0.82f);
    const float RoundSize = 96f;   // in the 720x1280 reference canvas: big enough for a thumb

    static Font bundledFont;
    static bool fontLoaded;

    public static void Apply(Scene scene)
    {
        ApplyFont();
        if (System.Array.IndexOf(ScenesWithoutBack, scene.name) >= 0 && Application.CanStreamedLevelBeLoaded(MainScene))
            CreateBackToMain();
        if (System.Array.IndexOf(BedroomScenes, scene.name) >= 0)
            RoomTopView.Create();
        SetUpFacilityToggle(scene.name);

        // every time the user enters a room (not the top views): a 2-second "how to use" hint
        if (System.Array.IndexOf(BedroomScenes, scene.name) >= 0 || System.Array.IndexOf(FacilityRooms, scene.name) >= 0)
            FirstEntryHint.Show("터치로 화면 제어");
    }

    // Same look as the bedrooms: a round icon button at the top right (view from above / back inside) instead of
    // the team's camera icon next to the title. That icon (its "Camera" canvas) is hidden; the button switches scenes.
    static void SetUpFacilityToggle(string sceneName)
    {
        string room = null; bool fromAbove = false;
        foreach (var f in FacilityRooms)
        {
            if (sceneName == f) { room = f; break; }
            if (sceneName == f + "View") { room = f; fromAbove = true; break; }
        }
        if (room == null) return;

        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.gameObject.name == "Camera") c.gameObject.SetActive(false);

        string target = fromAbove ? room : room + "View";
        if (!Application.CanStreamedLevelBeLoaded(target)) return;
        CreateTopRightButton(null, fromAbove ? UiIcon.InRoom : UiIcon.TopView, () => SceneManager.LoadScene(target));
    }

    // Round icon button in the top-right corner (mirror of the back button on the left).
    // parent == null -> its own root canvas; returns the icon so callers can swap it (e.g. TopView <-> InRoom).
    public static Image CreateTopRightButton(Transform parent, UiIcon icon, UnityEngine.Events.UnityAction onClick)
    {
        var safe = CreateCornerCanvas("AppTopRightCanvas", parent);
        return CreateRoundButton(safe, "TopRightButton", new Vector2(1f, 1f), new Vector2(-20, -20), icon, onClick).icon;
    }

    static void ApplyFont()
    {
        if (!fontLoaded) { bundledFont = Resources.Load<Font>("KoreanFont"); fontLoaded = true; }
        if (bundledFont == null) return;
        foreach (var t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            t.font = bundledFont;
    }

    static void CreateBackToMain()
    {
        CreateBackButton("메인", () => SceneManager.LoadScene(MainScene));
    }

    // Round back-arrow button in the top-left corner on its OWN canvas (so it can never end up under another canvas
    // that gets hidden or scaled differently - the cause of the missing back button in GymRoom / StudyRoom).
    // `label` is not drawn; it is kept as an invisible caption (name of where the button leads).
    public static Button CreateBackButton(string label, UnityEngine.Events.UnityAction onClick)
    {
        var safe = CreateCornerCanvas("AppBackCanvas", null);
        var made = CreateRoundButton(safe, "BackToMain", new Vector2(0f, 1f), new Vector2(20, -20), UiIcon.Back, onClick);
        AddHiddenCaption(made.button.transform, label);
        return made.button;
    }

    // Turns a button that is already in a scene ("화장실" / "방으로") into a round-cornered text pill, same dark look.
    public static void StyleActionButton(Button button, string text)
    {
        if (button == null) return;
        var bg = button.targetGraphic as Image;
        if (bg != null)
        {
            bg.sprite = UiIcons.Disc();
            bg.type = Image.Type.Sliced;
            bg.color = ButtonColor;
        }
        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            if (!fontLoaded) { bundledFont = Resources.Load<Font>("KoreanFont"); fontLoaded = true; }
            if (bundledFont != null) label.font = bundledFont;
            label.color = Color.white;
            label.text = text;
        }
    }

    // Turns a button that is already in a scene (the old text "‹ 층 보기" back button) into the same round icon button.
    // Its text stays as an invisible caption.
    public static void StyleAsRoundIcon(Button button, UiIcon icon)
    {
        if (button == null) return;
        var bg = button.targetGraphic as Image;
        if (bg != null)
        {
            bg.sprite = UiIcons.Disc();
            bg.type = Image.Type.Simple;
            bg.color = ButtonColor;
        }
        var rt = (RectTransform)button.transform;
        rt.sizeDelta = new Vector2(RoundSize, RoundSize);
        foreach (var t in button.GetComponentsInChildren<Text>(true)) t.enabled = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(button.transform, false);
        var irt = (RectTransform)iconGo.transform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(RoundSize * 0.62f, RoundSize * 0.62f);
        var img = iconGo.GetComponent<Image>();
        img.sprite = UiIcons.Get(icon);
        img.raycastTarget = false;
        img.preserveAspect = true;
    }

    // ---------------------------------------------------------------------------------------------------- helpers

    static Transform CreateCornerCanvas(string name, Transform parent)
    {
        UiUtil.EnsureEventSystem();
        var canvasGo = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (parent != null) canvasGo.transform.SetParent(parent, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720, 1280);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // keep clear of notches / system bars
        var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
        safe.transform.SetParent(canvasGo.transform, false);
        return safe.transform;
    }

    static (Button button, Image icon) CreateRoundButton(Transform parent, string name, Vector2 corner, Vector2 offset,
                                                         UiIcon icon, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = corner;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(RoundSize, RoundSize);

        var bg = go.GetComponent<Image>();
        bg.sprite = UiIcons.Disc();
        bg.color = ButtonColor;
        var button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        // a little darker while pressed
        var colors = button.colors;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(onClick);

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        var irt = (RectTransform)iconGo.transform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(RoundSize * 0.62f, RoundSize * 0.62f);
        var img = iconGo.GetComponent<Image>();
        img.sprite = UiIcons.Get(icon);
        img.raycastTarget = false;
        img.preserveAspect = true;
        return (button, img);
    }

    static void AddHiddenCaption(Transform button, string label)
    {
        if (!fontLoaded) { bundledFont = Resources.Load<Font>("KoreanFont"); fontLoaded = true; }
        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(button, false);
        var text = textGo.GetComponent<Text>();
        text.font = bundledFont != null ? bundledFont : UiUtil.CreateKoreanFont(32);
        text.text = label;
        text.raycastTarget = false;
        text.enabled = false; // kept only as the button's caption, never drawn
    }
}
