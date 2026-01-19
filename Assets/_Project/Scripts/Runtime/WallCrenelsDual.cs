using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class WallCrenelsDual : MonoBehaviour
{
    [Header("Optional (leave empty)")]
    [SerializeField] private MeshFilter sourceMeshFilter; // if empty, auto-picks biggest wall mesh

    [Header("Crenels Renderer (separate material)")]
    [SerializeField] private Material crenelsMaterial;

    [Header("Layout (relative to source mesh bounds)")]
    [Range(0.01f, 0.5f)] public float crenelWidthK = 0.08f;      // along wall length
    [Range(0.0f, 0.5f)]  public float gapK = 0.06f;             // along wall length
    [Range(0.05f, 1.0f)] public float crenelDepthK = 0.22f;     // along wall thickness
    [Range(0.05f, 1.0f)] public float crenelHeightK = 0.25f;    // above top
    [Range(0.0f, 0.45f)] public float edgeInsetK = 0.08f;       // inset from outer edge
    [Range(0.0f, 0.45f)] public float endPaddingK = 0.12f;      // keep clear near ends (for joints)

    [Header("Behaviour")]
    public bool rebuildInPlayOnStart = true;
    public bool rebuildInEditOnValidate = true;

    const string ChildName = "__CrenelsDual";
    Transform _child;
    MeshFilter _mf;
    MeshRenderer _mr;

    void OnEnable()
    {
        EnsureChild();
        if (Application.isPlaying && rebuildInPlayOnStart)
            StartCoroutine(RebuildNextFrame());
        else
            Rebuild();
    }

    void OnValidate()
    {
        if (!enabled) return;
        EnsureChild();
        if (!Application.isPlaying && rebuildInEditOnValidate)
            Rebuild();
    }

    IEnumerator RebuildNextFrame()
    {
        yield return null; // let Wall3DVisual generate meshes first
        Rebuild();
    }

    void EnsureChild()
    {
        if (_child == null)
        {
            var t = transform.Find(ChildName);
            _child = t != null ? t : null;
        }

        if (_child == null)
        {
            var go = new GameObject(ChildName);
            go.transform.SetParent(transform, false);
            _child = go.transform;
        }

        _mf = _child.GetComponent<MeshFilter>();
        if (_mf == null) _mf = _child.gameObject.AddComponent<MeshFilter>();

        _mr = _child.GetComponent<MeshRenderer>();
        if (_mr == null) _mr = _child.gameObject.AddComponent<MeshRenderer>();
    }

    MeshFilter PickBestWallMeshFilter()
    {
        // Prefer explicit
        if (sourceMeshFilter != null && sourceMeshFilter.sharedMesh != null)
            return sourceMeshFilter;

        // Pick biggest mesh (most likely the main wall body)
        MeshFilter best = null;
        float bestScore = 0f;

        var mfs = GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in mfs)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.transform == _child) continue;
            if (mf.name == ChildName) continue;

            var b = mf.sharedMesh.bounds;
            float s = b.size.x * b.size.y * b.size.z;

            // include transform scale (approx)
            var ls = mf.transform.lossyScale;
            float scaleK = Mathf.Abs(ls.x * ls.y * ls.z);
            float score = s * Mathf.Max(0.0001f, scaleK);

            if (score > bestScore)
            {
                bestScore = score;
                best = mf;
            }
        }

        return best;
    }

    static float GetAxis(Vector3 v, int axis) => axis == 0 ? v.x : (axis == 1 ? v.y : v.z);
    static Vector3 SetAxis(Vector3 v, int axis, float value)
    {
        if (axis == 0) v.x = value;
        else if (axis == 1) v.y = value;
        else v.z = value;
        return v;
    }

    public void Rebuild()
    {
        EnsureChild();

        var src = PickBestWallMeshFilter();
        if (src == null || src.sharedMesh == null)
        {
            _mf.sharedMesh = null;
            return;
        }

        // Keep crenels mesh in SAME local space as source mesh
        if (_child.parent != src.transform)
        {
            _child.SetParent(src.transform, false);
            _child.localPosition = Vector3.zero;
            _child.localRotation = Quaternion.identity;
            _child.localScale = Vector3.one;
        }

        if (crenelsMaterial != null)
            _mr.sharedMaterial = crenelsMaterial;

        var mesh = src.sharedMesh;
        var b = mesh.bounds; // local bounds of source mesh

        float sizeX = Mathf.Max(0.001f, b.size.x);
        float sizeZ = Mathf.Max(0.001f, b.size.z);
        float sizeY = Mathf.Max(0.001f, b.size.y);

        // Decide which axis is "length": X or Z (ignore Y)
        int lengthAxis = (sizeX >= sizeZ) ? 0 : 2;
        int thickAxis  = (lengthAxis == 0) ? 2 : 0;

        float len = Mathf.Max(0.001f, GetAxis(b.size, lengthAxis));
        float thick = Mathf.Max(0.001f, GetAxis(b.size, thickAxis));

        float topY = b.center.y + b.extents.y;

        float crenelW = Mathf.Clamp(len * crenelWidthK, 0.01f, len);
        float gap = Mathf.Clamp(len * gapK, 0.0f, len);
        float step = Mathf.Max(0.01f, crenelW + gap);

        float pad = Mathf.Clamp(len * endPaddingK, 0.0f, len * 0.45f);
        float start = (GetAxis(b.center, lengthAxis) - len * 0.5f) + pad;
        float end   = (GetAxis(b.center, lengthAxis) + len * 0.5f) - pad;

        float depth = Mathf.Clamp(thick * crenelDepthK, 0.01f, thick);
        float inset = Mathf.Clamp(thick * edgeInsetK, 0.0f, thick * 0.45f);

        float tMin = (GetAxis(b.center, thickAxis) - thick * 0.5f) + inset + depth * 0.5f;
        float tMax = (GetAxis(b.center, thickAxis) + thick * 0.5f) - inset - depth * 0.5f;

        float crenelH = Mathf.Clamp(sizeY * crenelHeightK, 0.01f, sizeY * 2f);

        var verts = new List<Vector3>(1024);
        var tris  = new List<int>(2048);
        var norms = new List<Vector3>(1024);
        var uvs   = new List<Vector2>(1024);

        void AddBox(Vector3 c, Vector3 s)
        {
            int v0 = verts.Count;

            float hx = s.x * 0.5f;
            float hy = s.y * 0.5f;
            float hz = s.z * 0.5f;

            // +X
            verts.Add(new Vector3(c.x + hx, c.y - hy, c.z - hz));
            verts.Add(new Vector3(c.x + hx, c.y - hy, c.z + hz));
            verts.Add(new Vector3(c.x + hx, c.y + hy, c.z + hz));
            verts.Add(new Vector3(c.x + hx, c.y + hy, c.z - hz));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.right);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            // -X
            verts.Add(new Vector3(c.x - hx, c.y - hy, c.z + hz));
            verts.Add(new Vector3(c.x - hx, c.y - hy, c.z - hz));
            verts.Add(new Vector3(c.x - hx, c.y + hy, c.z - hz));
            verts.Add(new Vector3(c.x - hx, c.y + hy, c.z + hz));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.left);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            // +Y
            verts.Add(new Vector3(c.x - hx, c.y + hy, c.z - hz));
            verts.Add(new Vector3(c.x + hx, c.y + hy, c.z - hz));
            verts.Add(new Vector3(c.x + hx, c.y + hy, c.z + hz));
            verts.Add(new Vector3(c.x - hx, c.y + hy, c.z + hz));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.up);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            // -Y
            verts.Add(new Vector3(c.x - hx, c.y - hy, c.z + hz));
            verts.Add(new Vector3(c.x + hx, c.y - hy, c.z + hz));
            verts.Add(new Vector3(c.x + hx, c.y - hy, c.z - hz));
            verts.Add(new Vector3(c.x - hx, c.y - hy, c.z - hz));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.down);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            // +Z
            verts.Add(new Vector3(c.x + hx, c.y - hy, c.z + hz));
            verts.Add(new Vector3(c.x - hx, c.y - hy, c.z + hz));
            verts.Add(new Vector3(c.x - hx, c.y + hy, c.z + hz));
            verts.Add(new Vector3(c.x + hx, c.y + hy, c.z + hz));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.forward);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            // -Z
            verts.Add(new Vector3(c.x - hx, c.y - hy, c.z - hz));
            verts.Add(new Vector3(c.x + hx, c.y - hy, c.z - hz));
            verts.Add(new Vector3(c.x + hx, c.y + hy, c.z - hz));
            verts.Add(new Vector3(c.x - hx, c.y + hy, c.z - hz));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.back);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            for (int f = 0; f < 6; f++)
            {
                int o = v0 + f * 4;
                tris.Add(o + 0); tris.Add(o + 2); tris.Add(o + 1);
                tris.Add(o + 0); tris.Add(o + 3); tris.Add(o + 2);
            }
        }

        float x = start;
        int safety = 0;

        while (x + crenelW * 0.5f <= end && safety++ < 1000)
        {
            float cLen = x + crenelW * 0.5f;
            float cy = topY + crenelH * 0.5f;

            // build sizes aligned to chosen axes
            Vector3 size = Vector3.one;
            size = SetAxis(size, lengthAxis, crenelW);
            size.y = crenelH;
            size = SetAxis(size, thickAxis, depth);

            // left row
            Vector3 c1 = b.center;
            c1 = SetAxis(c1, lengthAxis, cLen);
            c1.y = cy;
            c1 = SetAxis(c1, thickAxis, tMin);
            AddBox(c1, size);

            // right row
            Vector3 c2 = b.center;
            c2 = SetAxis(c2, lengthAxis, cLen);
            c2.y = cy;
            c2 = SetAxis(c2, thickAxis, tMax);
            AddBox(c2, size);

            x += step;
        }

        var outMesh = new Mesh();
        outMesh.name = "CrenelsDualMesh";
        outMesh.SetVertices(verts);
        outMesh.SetNormals(norms);
        outMesh.SetUVs(0, uvs);
        outMesh.SetTriangles(tris, 0);
        outMesh.RecalculateBounds();

        _mf.sharedMesh = outMesh;
    }
}
