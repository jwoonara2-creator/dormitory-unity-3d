using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// "View from above" for the bedroom scenes (RoomView_1Person / 2Person / Accessible), like GymRoomView:
// a top-right button hides the ceiling and moves the camera straight above the room; pressing it again
// glides back to the first-person view. Built at runtime (see AppUiFixes), no scene editing needed.
public class RoomTopView : MonoBehaviour
{
    const float Duration = 0.45f;
    const float Margin = 1.12f;

    Camera cam;
    RoomLookCamera look;
    RoomViewController controller;
    Image icon;   // the button's icon: TopView while inside the room, InRoom while seen from above
    readonly List<Renderer> hiddenCeiling = new List<Renderer>();
    Vector3 savedPos;
    Quaternion savedRot;
    float savedFov;
    bool top;
    Coroutine anim;

    public static void Create()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var look = cam.GetComponent<RoomLookCamera>();
        if (look == null) return; // only the room views have the first-person camera

        var go = new GameObject("RoomTopView");
        var tv = go.AddComponent<RoomTopView>();
        tv.cam = cam;
        tv.look = look;
        tv.controller = FindFirstObjectByType<RoomViewController>();
        tv.BuildButton();
    }

    void BuildButton()
    {
        // same button as the laundry / gym / seminar rooms (see AppUiFixes)
        icon = AppUiFixes.CreateTopRightButton(transform, UiIcon.TopView, Toggle);
    }

    void Toggle()
    {
        if (anim != null) return; // ignore taps while gliding
        anim = StartCoroutine(top ? GoBack() : GoTop());
    }

    IEnumerator GoTop()
    {
        savedPos = cam.transform.position;
        savedRot = cam.transform.rotation;
        savedFov = cam.fieldOfView;
        look.enabled = false;
        HideCeiling(true);
        if (controller != null) controller.SetButtonsHidden(true);

        ComputeTopPose(out var pos, out var rot);
        yield return Glide(pos, rot, savedFov);
        top = true;
        icon.sprite = UiIcons.Get(UiIcon.InRoom);
        anim = null;
    }

    IEnumerator GoBack()
    {
        yield return Glide(savedPos, savedRot, savedFov);
        HideCeiling(false);
        look.enabled = true;
        if (controller != null) controller.SetButtonsHidden(false);
        top = false;
        icon.sprite = UiIcons.Get(UiIcon.TopView);
        anim = null;
    }

    IEnumerator Glide(Vector3 pos, Quaternion rot, float fov)
    {
        var p0 = cam.transform.position;
        var r0 = cam.transform.rotation;
        var f0 = cam.fieldOfView;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Duration)
        {
            float k = t * t * (3f - 2f * t); // smoothstep
            cam.transform.position = Vector3.Lerp(p0, pos, k);
            cam.transform.rotation = Quaternion.Slerp(r0, rot, k);
            cam.fieldOfView = Mathf.Lerp(f0, fov, k);
            yield return null;
        }
        cam.transform.position = pos;
        cam.transform.rotation = rot;
        cam.fieldOfView = fov;
    }

    void HideCeiling(bool hide)
    {
        if (hide)
        {
            hiddenCeiling.Clear();
            foreach (var tr in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!IsCeilingName(tr.name)) continue;
                foreach (var r in tr.GetComponentsInChildren<Renderer>(false))
                {
                    if (!r.enabled) continue;
                    r.enabled = false;
                    hiddenCeiling.Add(r);
                }
            }
        }
        else
        {
            foreach (var r in hiddenCeiling) if (r != null) r.enabled = true;
            hiddenCeiling.Clear();
        }
    }

    // "Ceiling", "Ceiling Panel", "Ceiling Panel Frame", "Hall Ceiling" are meshes; "Ceiling Light" is only a light.
    // "Sky" / "Outside" are the big backdrop planes seen through the window - they look wrong from above and
    // are far larger than the room, so they are hidden too and ignored when framing the room.
    static bool IsCeilingName(string n)
    {
        if (n == "Hall Ceiling" || n == "Sky" || n == "Outside") return true;
        return n.StartsWith("Ceiling") && !n.StartsWith("Ceiling Light");
    }

    // straight-down camera that fits the whole room (and the attached bathroom) on screen
    void ComputeTopPose(out Vector3 pos, out Quaternion rot)
    {
        bool any = false;
        Bounds b = default;
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any)
        {
            pos = cam.transform.position + Vector3.up * 8f;
            rot = Quaternion.Euler(90f, 0f, 0f);
            return;
        }

        float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float aspect = Mathf.Max(0.3f, cam.aspect);
        Vector3 e = b.extents;
        // yaw 0: screen-up = +Z, screen-right = +X ; yaw 90: screen-up = +X
        float d0 = Mathf.Max(e.z / tanV, e.x / (tanV * aspect));
        float d90 = Mathf.Max(e.x / tanV, e.z / (tanV * aspect));
        bool turn = d90 < d0;
        float dist = Mathf.Min(d0, d90) * Margin;
        pos = new Vector3(b.center.x, b.max.y + dist, b.center.z);
        rot = Quaternion.Euler(90f, turn ? 90f : 0f, 0f);
    }
}
