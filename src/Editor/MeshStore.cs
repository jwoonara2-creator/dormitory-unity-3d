using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

// Box meshes with UVs scaled to world metres. They are saved as .asset files (de-duplicated by size/tile),
// so prefabs that use them keep their geometry when instantiated in other scenes.
public static class MeshStore
{
    const string Dir = "Assets/Meshes/Generated";

    public static Mesh Box(Vector3 s, float tile, bool swapUV = false)
    {
        var ci = CultureInfo.InvariantCulture;
        string key = string.Format(ci, "box_{0:F3}_{1:F3}_{2:F3}_t{3:F2}_{4}", s.x, s.y, s.z, tile, swapUV ? 1 : 0);
        string path = Dir + "/" + key + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        var m = Build(s, tile, swapUV);
        m.name = key;
        Directory.CreateDirectory(Dir);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Mesh Build(Vector3 s, float tile, bool swapUV)
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
        if (swapUV) for (int k = 0; k < uvs.Count; k++) uvs[k] = new Vector2(uvs[k].y, uvs[k].x);
        var m = new Mesh();
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetNormals(nrm); m.SetTriangles(tris, 0);
        m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }
}
