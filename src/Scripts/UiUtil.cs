using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Small UI helpers shared by the navigation scripts.
public static class UiUtil
{
    static readonly List<RaycastResult> results = new List<RaycastResult>();

    // true if the screen point is over any UI element that blocks raycasts (buttons, panels)
    public static bool IsOverUi(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var ev = new PointerEventData(EventSystem.current) { position = screenPos };
        results.Clear();
        EventSystem.current.RaycastAll(ev, results);
        return results.Count > 0;
    }

    // Font with Hangul glyphs; null if none could be created.
    // WebGL has no OS fonts, so a font file at Assets/Resources/KoreanFont.(ttf|otf) is used first when present.
    public static Font CreateKoreanFont(int size)
    {
        var bundled = Resources.Load<Font>("KoreanFont");
        if (bundled != null) return bundled;
        try
        {
            return Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic", "Roboto" }, size);
        }
        catch { return null; }
    }

    // makes sure a UI input module exists (new Input System)
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }
}

// Tells the camera scripts whether the current press started on a UI element, so dragging a button
// does not also move the camera. Pointer = mouse or first touch.
public static class PointerGate
{
    static bool blocked;
    static int lastFrame = -1;

    public static bool Blocked
    {
        get { Refresh(); return blocked; }
    }

    static void Refresh()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        bool began = false, down = false;
        Vector2 pos = default;
        var m = Mouse.current;
        if (m != null)
        {
            down |= m.leftButton.isPressed || m.rightButton.isPressed;
            if (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame) { began = true; pos = m.position.ReadValue(); }
        }
        var t = Touchscreen.current;
        if (t != null)
        {
            down |= t.primaryTouch.press.isPressed;
            if (t.primaryTouch.press.wasPressedThisFrame) { began = true; pos = t.primaryTouch.position.ReadValue(); }
        }
        if (began) blocked = UiUtil.IsOverUi(pos);
        else if (!down) blocked = false;
    }
}
