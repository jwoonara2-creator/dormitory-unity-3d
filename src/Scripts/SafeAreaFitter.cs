using UnityEngine;

// Keeps a UI root inside the device safe area (notches, rounded corners, system bars).
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    Rect last;
    Vector2Int lastSize;

    void Awake() { Apply(); }
    void Update() { Apply(); }

    void Apply()
    {
        var sa = Screen.safeArea;
        if (sa == last && lastSize.x == Screen.width && lastSize.y == Screen.height) return;
        last = sa; lastSize = new Vector2Int(Screen.width, Screen.height);
        var rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
