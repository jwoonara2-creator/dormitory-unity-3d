using UnityEngine;
using UnityEngine.InputSystem;

// Orbit camera for the floor cutaway view.
//   mouse right-drag : up/down = tilt (0 = straight down), left/right = rotate around the floor
//   mouse left-drag  : pan        mouse wheel : zoom        R : reset
//   touch            : one finger = pan, two fingers = pinch zoom, two-finger drag = tilt / rotate
// A press that starts on a UI button never moves the camera.
public class FloorViewCamera : MonoBehaviour
{
    public Vector3 target = new Vector3(0f, 0f, 21.7f);
    [Range(0f, 75f)] public float tilt = 20f;      // degrees from straight down
    public float yaw = 0f;                          // degrees, 0 = plan orientation
    public Vector3 planUp = new Vector3(-1f, 0f, 0f); // world direction shown as 'up' on screen in plan view
    public float distance = 38f;

    public float minTilt = 0f, maxTilt = 70f;
    public float minDistance = 8f, maxDistance = 70f;
    public float tiltSpeed = 0.25f, yawSpeed = 0.25f, zoomSpeed = 0.01f, panSpeed = 0.0025f, smoothing = 14f;
    public Vector2 panLimitX = new Vector2(-25f, 25f), panLimitZ = new Vector2(-5f, 50f);

    float curTilt, curYaw, curDist, lastPinch; Vector3 curTarget;
    float defTilt, defYaw, defDist; Vector3 defTarget;

    void Awake()
    {
        defTilt = tilt; defYaw = yaw; defDist = distance; defTarget = target;
        SnapToGoal();
    }

    void OnValidate() { if (Application.isPlaying == false) { SnapToGoal(); } }

    public void SnapToGoal() { curTilt = tilt; curYaw = yaw; curDist = distance; curTarget = target; Apply(); }

    void Update()
    {
        var ts = Touchscreen.current;
        int touching = 0;
        if (ts != null) foreach (var t in ts.touches) if (t.press.isPressed) touching++;
        bool blocked = PointerGate.Blocked;

        if (touching >= 1)
        {
            if (!blocked)
            {
                Vector2 sum = Vector2.zero, p0 = default, p1 = default; int n = 0;
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    sum += t.delta.ReadValue();
                    if (n == 0) p0 = t.position.ReadValue(); else if (n == 1) p1 = t.position.ReadValue();
                    n++;
                }
                Vector2 d = sum / Mathf.Max(1, n);
                if (n == 1)
                {
                    Pan(d);
                    lastPinch = 0f;
                }
                else
                {
                    tilt = Mathf.Clamp(tilt - d.y * tiltSpeed, minTilt, maxTilt);
                    yaw += d.x * yawSpeed;
                    float dist = Vector2.Distance(p0, p1);
                    if (lastPinch > 0f && dist > 0f) distance = Mathf.Clamp(distance * (lastPinch / dist), minDistance, maxDistance);
                    lastPinch = dist;
                }
            }
        }
        else
        {
            lastPinch = 0f;
            var mouse = Mouse.current;
            if (mouse != null && !blocked)
            {
                Vector2 d = mouse.delta.ReadValue();
                if (mouse.rightButton.isPressed)
                {
                    tilt = Mathf.Clamp(tilt - d.y * tiltSpeed, minTilt, maxTilt);
                    yaw += d.x * yawSpeed;
                }
                if (mouse.leftButton.isPressed) Pan(d);
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) distance = Mathf.Clamp(distance * (1f - wheel * zoomSpeed), minDistance, maxDistance);
            }
        }
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ResetView();

        float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        curTilt = Mathf.Lerp(curTilt, tilt, k);
        curYaw = Mathf.Lerp(curYaw, yaw, k);
        curDist = Mathf.Lerp(curDist, distance, k);
        curTarget = Vector3.Lerp(curTarget, target, k);
        Apply();
    }

    // drag the floor with the pointer (screen-up = planUp, screen-right = up x planUp)
    void Pan(Vector2 d)
    {
        Vector3 up = Quaternion.Euler(0, curYaw, 0) * planUp;
        Vector3 right = Vector3.Cross(Vector3.up, up);
        target -= (right * d.x + up * d.y) * (panSpeed * curDist);
        target.x = Mathf.Clamp(target.x, panLimitX.x, panLimitX.y);
        target.z = Mathf.Clamp(target.z, panLimitZ.x, panLimitZ.y);
    }

    public void ResetView() { tilt = defTilt; yaw = defYaw; distance = defDist; target = defTarget; }

    void Apply()
    {
        Quaternion around = Quaternion.Euler(0f, curYaw, 0f);
        float r = curTilt * Mathf.Deg2Rad;
        Vector3 offset = (around * (-planUp * Mathf.Sin(r)) + Vector3.up * Mathf.Cos(r)) * curDist;
        transform.position = curTarget + offset;
        transform.LookAt(curTarget, around * planUp);
    }
}
