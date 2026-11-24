using UnityEngine;
using UnityEngine.InputSystem;

// First-person "look around" camera for the room scenes (Street View / road-view style).
// The camera position stays fixed while dragging; dragging turns the view.
//   mouse : left-drag = look around, wheel = zoom, R = reset
//   touch : one-finger drag = look around, two-finger pinch = zoom
// Drag is "grab" style: the scene follows the finger (drag right -> the view turns left).
// MoveAlong() glides the camera through waypoints (used by the "go to the bathroom" button).
[RequireComponent(typeof(Camera))]
public class RoomLookCamera : MonoBehaviour
{
    public float yaw, pitch;
    public float minPitch = -80f, maxPitch = 80f;
    public float minFov = 35f, maxFov = 85f;
    public float smoothing = 18f;
    public float moveSmoothing = 3.2f, turnSmoothingWhileMoving = 4f;
    public bool invertDrag = false;
    [SerializeField] float startYaw, startPitch, startFov;

    Camera cam;
    float curYaw, curPitch, curFov, targetFov;
    float lastPinch;
    Vector3 goalPos;
    Vector3[] path; int pathIdx;

    // editor-side helper: put this component on the scene camera and remember its current look direction
    public static void AttachTo(GameObject camObj)
    {
        if (camObj == null) return;
        var c = camObj.GetComponent<RoomLookCamera>();
        if (c == null) c = camObj.AddComponent<RoomLookCamera>();
        var e = camObj.transform.eulerAngles;
        c.yaw = e.y;
        c.pitch = e.x > 180f ? e.x - 360f : e.x;
        c.startYaw = c.yaw; c.startPitch = c.pitch;
        c.startFov = camObj.GetComponent<Camera>().fieldOfView;
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (startFov <= 0f) startFov = cam.fieldOfView;
        curYaw = yaw; curPitch = pitch; curFov = targetFov = cam.fieldOfView;
        goalPos = transform.position;
    }

    void Update()
    {
        Vector2 drag = Vector2.zero;
        float zoom = 0f;                    // positive = zoom in (degrees of fov)
        var ts = Touchscreen.current;
        int touching = 0;
        if (ts != null)
            foreach (var t in ts.touches) if (t.press.isPressed) touching++;

        if (touching >= 2)
        {
            int found = 0; Vector2 p0 = default, p1 = default;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                if (found == 0) p0 = t.position.ReadValue(); else if (found == 1) p1 = t.position.ReadValue();
                found++;
            }
            float d = Vector2.Distance(p0, p1);
            if (lastPinch > 0f) zoom = (d - lastPinch) / Mathf.Max(1f, Screen.height) * (maxFov - minFov) * 1.5f;
            lastPinch = d;
        }
        else
        {
            lastPinch = 0f;
            if (touching == 1)
            {
                foreach (var t in ts.touches) if (t.press.isPressed) { drag = t.delta.ReadValue(); break; }
            }
            else if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.isPressed) drag = Mouse.current.delta.ReadValue();
                zoom = Mouse.current.scroll.ReadValue().y * 0.02f;
            }
        }
        if (PointerGate.Blocked) { drag = Vector2.zero; zoom = 0f; }   // a press that started on a button is not a camera drag
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetView();

        ApplyDrag(drag);
        targetFov = Mathf.Clamp(targetFov - zoom, minFov, maxFov);

        bool moving = path != null && pathIdx < path.Length;
        if (moving)
        {
            goalPos = path[pathIdx];
            if ((transform.position - goalPos).sqrMagnitude < 0.0025f) pathIdx++;
        }
        float dt = Time.unscaledDeltaTime;
        float k = 1f - Mathf.Exp(-(moving ? turnSmoothingWhileMoving : smoothing) * dt);
        curYaw = Mathf.LerpAngle(curYaw, yaw, k);
        curPitch = Mathf.Lerp(curPitch, pitch, k);
        curFov = Mathf.Lerp(curFov, targetFov, 1f - Mathf.Exp(-smoothing * dt));
        cam.fieldOfView = curFov;
        transform.position = Vector3.Lerp(transform.position, goalPos, 1f - Mathf.Exp(-moveSmoothing * dt));
        transform.rotation = Quaternion.Euler(curPitch, curYaw, 0f);
    }

    // drag in screen pixels; one full screen height turns the view by the vertical field of view (1:1 feel)
    public void ApplyDrag(Vector2 pixels)
    {
        if (pixels == Vector2.zero) return;
        if (cam == null) cam = GetComponent<Camera>();
        float s = cam.fieldOfView / Mathf.Max(1f, Screen.height);
        float sign = invertDrag ? 1f : -1f;
        yaw += sign * pixels.x * s;
        pitch = Mathf.Clamp(pitch - sign * pixels.y * s, minPitch, maxPitch);
    }

    // glide through the waypoints (the last one is the destination) while turning to newYaw / newPitch
    public void MoveAlong(Vector3[] waypoints, float newYaw, float newPitch)
    {
        path = waypoints; pathIdx = 0;
        yaw = newYaw; pitch = newPitch;
        if (waypoints != null && waypoints.Length > 0) goalPos = waypoints[0];
    }

    // test hook (edit mode): apply the current yaw/pitch to the transform immediately
    public void ApplyNow()
    {
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public void ResetView() { yaw = startYaw; pitch = startPitch; targetFov = startFov; }
}
