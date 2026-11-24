using System.Collections.Generic;
using UnityEngine;

// Icons for the runtime-built buttons (back arrow, view from above, ...). Drawn into small textures once with
// anti-aliased shapes, so there are no image assets to import and they stay sharp on every screen size.
public enum UiIcon { Back, TopView, InRoom, Bath, Door }

public static class UiIcons
{
    const int N = 128;
    static readonly Dictionary<UiIcon, Sprite> cache = new Dictionary<UiIcon, Sprite>();
    static Sprite disc;

    // round button background; has 9-slice borders so the same sprite makes a pill (Image.Type = Sliced)
    public static Sprite Disc()
    {
        if (disc != null) return disc;
        const int n = 64;
        var t = NewTexture(n);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(n / 2f - d + 0.5f));
            }
        t.SetPixels(px); t.Apply();
        disc = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                             new Vector4(31, 31, 31, 31));
        return disc;
    }

    public static Sprite Get(UiIcon icon)
    {
        if (cache.TryGetValue(icon, out var s) && s != null) return s;
        var t = NewTexture(N);
        var px = new Color[N * N];
        float aa = 1f / N;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                var p = new Vector2((x + 0.5f) / N, (y + 0.5f) / N); // 0..1, y up
                px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01(Alpha(icon, p, aa)));
            }
        t.SetPixels(px); t.Apply();
        s = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        cache[icon] = s;
        return s;
    }

    static Texture2D NewTexture(int n) => new Texture2D(n, n, TextureFormat.RGBA32, false)
    {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp,
    };

    // coverage 0..1 of the icon at point p
    static float Alpha(UiIcon icon, Vector2 p, float aa)
    {
        const float w = 0.055f; // stroke half width
        switch (icon)
        {
            case UiIcon.Back: // arrow pointing left
                return Stroke(Mathf.Min(
                    Seg(p, V(0.24f, 0.5f), V(0.76f, 0.5f)),
                    Seg(p, V(0.24f, 0.5f), V(0.47f, 0.27f)),
                    Seg(p, V(0.24f, 0.5f), V(0.47f, 0.73f))), w, aa);

            case UiIcon.TopView: // a floor plan seen from above: outer walls + two inner walls
                return Stroke(Mathf.Min(
                    Seg(p, V(0.22f, 0.22f), V(0.78f, 0.22f)), Seg(p, V(0.78f, 0.22f), V(0.78f, 0.78f)),
                    Seg(p, V(0.78f, 0.78f), V(0.22f, 0.78f)), Seg(p, V(0.22f, 0.78f), V(0.22f, 0.22f)),
                    Seg(p, V(0.5f, 0.22f), V(0.5f, 0.56f)), Seg(p, V(0.5f, 0.56f), V(0.78f, 0.56f))), w * 0.9f, aa);

            case UiIcon.InRoom: // an eye: the view from inside the room
            {
                float u = (p.x - 0.5f) / 0.34f, v = (p.y - 0.5f) / 0.21f;
                float ring = Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 1f) * 0.21f; // ~distance to the outline
                float outline = Stroke(ring, w * 0.9f, aa);
                float pupil = Fill(Vector2.Distance(p, V(0.5f, 0.5f)) - 0.1f, aa);
                return Mathf.Max(outline, pupil);
            }

            case UiIcon.Bath: // a water drop
            {
                float circle = Vector2.Distance(p, V(0.5f, 0.4f)) - 0.2f;
                float tri = PolySd(p, new[] { V(0.5f, 0.84f), V(0.31f, 0.5f), V(0.69f, 0.5f) });
                return Fill(Mathf.Min(circle, tri), aa);
            }

            default: // Door: frame + knob
            {
                float frame = Stroke(Mathf.Min(
                    Seg(p, V(0.3f, 0.18f), V(0.7f, 0.18f)), Seg(p, V(0.7f, 0.18f), V(0.7f, 0.84f)),
                    Seg(p, V(0.7f, 0.84f), V(0.3f, 0.84f)), Seg(p, V(0.3f, 0.84f), V(0.3f, 0.18f))), w * 0.9f, aa);
                float knob = Fill(Vector2.Distance(p, V(0.59f, 0.5f)) - 0.045f, aa);
                return Mathf.Max(frame, knob);
            }
        }
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);
    static float Stroke(float dist, float half, float aa) => (half - dist) / aa + 0.5f;
    static float Fill(float signedDist, float aa) => -signedDist / aa + 0.5f;

    static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * k);
    }

    // signed distance to a (convex or concave) polygon: negative inside
    static float PolySd(Vector2 p, Vector2[] poly)
    {
        float best = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            best = Mathf.Min(best, Seg(p, poly[j], poly[i]));
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        return inside ? -best : best;
    }
}
