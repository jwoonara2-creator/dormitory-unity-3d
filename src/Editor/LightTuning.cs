using UnityEngine;

// One place to tune how bright every room is. All room builders multiply their lights by these factors,
// so rebuilding a room keeps the toned-down lighting.
//   PointScale   : ceiling / fill lights inside the rooms
//   SunScale     : the Directional Light (only reaches rooms through the windows)
//   AmbientScale : flat ambient light
public static class LightTuning
{
    public const float PointScale = 0.6f;
    public const float SunScale = 0.7f;
    public const float AmbientScale = 0.8f;

    public static Color Amb(Color c) { return new Color(c.r * AmbientScale, c.g * AmbientScale, c.b * AmbientScale, c.a); }
}
