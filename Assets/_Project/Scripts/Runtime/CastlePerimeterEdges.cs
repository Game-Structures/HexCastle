using System.Collections.Generic;
using UnityEngine;

public sealed class CastlePerimeterEdges : MonoBehaviour
{
    [Header("Auto: will use HexCellView q=0 r=0 if not set")]
    [SerializeField] private HexCellView centerCellOverride;

    [Header("Prefab of one wall side (your WallSegment_CastleSide / WallSegment_Medieval copy)")]
    [SerializeField] private GameObject segmentPrefab;

    [Header("Segment orientation (depends how your segment mesh is built)")]
    [Tooltip("If your segment length goes along local X (you used 'Center On X'), keep true. If along Z - set false.")]
    [SerializeField] private bool lengthAxisIsX = true;

    [Header("Size (world units)")]
[SerializeField] private float lengthMul = 1.10f; // 1.0 = ровно по ребру, >1 = чуть длиннее

    [SerializeField] private float thickness = 0.35f;
    [SerializeField] private float height = 0.8f;

    [Header("Placement")]
    [SerializeField] private float yOffset = 0.02f;
    [Tooltip("Optional: pushes segments slightly outward from the tile edge.")]
    [SerializeField] private float outwardOffset = 0.0f;

    [Header("Materials (optional)")]
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material crenelsMaterial;

    private Transform _root;

    private void Start()
    {
        if (Application.isPlaying)
            Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (segmentPrefab == null)
        {
            Debug.LogWarning("[CastlePerimeterEdges] segmentPrefab is not set.");
            return;
        }

        var cell = ResolveCenterCell();
        if (cell == null)
        {
            Debug.LogError("[CastlePerimeterEdges] Center hex cell (q=0,r=0) not found.");
            return;
        }

        var mf = cell.GetComponentInChildren<MeshFilter>(true);
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("[CastlePerimeterEdges] MeshFilter/sharedMesh not found on center cell.");
            return;
        }

        EnsureRoot();
        ClearRoot();

        if (!TryGetTopRing6(mf.sharedMesh, out var ringLocal))
        {
            Debug.LogError("[CastlePerimeterEdges] Could not extract 6 top ring vertices from mesh.");
            return;
        }

        // Build 6 segments along mesh edges
        for (int i = 0; i < 6; i++)
        {
            Vector3 v0 = ringLocal[i];
            Vector3 v1 = ringLocal[(i + 1) % 6];

            Vector3 midLocal = (v0 + v1) * 0.5f;
            Vector3 edgeDirLocal = (v1 - v0);
            float edgeLen = edgeDirLocal.magnitude;
            if (edgeLen < 0.0001f) continue;
            edgeDirLocal /= edgeLen;

            // outward normal in tile local space
            Vector3 outLocal = Vector3.Cross(Vector3.up, edgeDirLocal).normalized;
            if (Vector3.Dot(midLocal, outLocal) < 0f) outLocal = -outLocal;

            Vector3 posWorld =
                cell.transform.TransformPoint(midLocal + outLocal * outwardOffset) +
                Vector3.up * yOffset;

            Vector3 edgeDirWorld = cell.transform.TransformDirection(edgeDirLocal).normalized;

            Quaternion rotWorld = lengthAxisIsX
                ? Quaternion.FromToRotation(Vector3.right, edgeDirWorld)
                : Quaternion.FromToRotation(Vector3.forward, edgeDirWorld);

            var seg = Instantiate(segmentPrefab, _root);
            seg.name = $"CastleEdgeSeg_{i}";
            seg.transform.position = posWorld;
            seg.transform.rotation = rotWorld;
            seg.transform.localScale = Vector3.one;

            // Configure mesh builder (if exists)
            var mesh = seg.GetComponent<MedievalWallSegmentMesh>();
            if (mesh == null) mesh = seg.GetComponentInChildren<MedievalWallSegmentMesh>(true);

            if (mesh != null)
            {
                mesh.length = edgeLen * Mathf.Max(0.5f, lengthMul);
                mesh.thickness = thickness;
                mesh.height = height;
                mesh.Rebuild();
            }

            // Apply materials (optional)
            var mr = seg.GetComponent<MeshRenderer>();
            if (mr == null) mr = seg.GetComponentInChildren<MeshRenderer>(true);

            if (mr != null && (wallMaterial != null || crenelsMaterial != null))
            {
                var mats = mr.sharedMaterials;
                if (mats == null || mats.Length < 2)
                    mats = new Material[2];

                if (wallMaterial != null) mats[0] = wallMaterial;
                if (crenelsMaterial != null) mats[1] = crenelsMaterial;

                mr.sharedMaterials = mats;
            }
        }
    }

    private HexCellView ResolveCenterCell()
    {
        if (centerCellOverride != null) return centerCellOverride;

        var all = FindObjectsByType<HexCellView>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].q == 0 && all[i].r == 0)
                return all[i];
        }
        return null;
    }

    private static bool TryGetTopRing6(Mesh m, out Vector3[] ring)
    {
        ring = null;
        var v = m.vertices;
        if (v == null || v.Length < 6) return false;

        // Take vertices with max Y (top face), ignore near-zero center
        float maxY = float.MinValue;
        for (int i = 0; i < v.Length; i++)
            if (v[i].y > maxY) maxY = v[i].y;

        const float yEps = 0.0005f;

        var top = new List<Vector3>(12);
        for (int i = 0; i < v.Length; i++)
        {
            if (Mathf.Abs(v[i].y - maxY) > yEps) continue;
            if (v[i].sqrMagnitude < 0.000001f) continue; // skip center
            top.Add(v[i]);
        }

        if (top.Count < 6) return false;

        // Pick 6 farthest from center (protect from duplicates)
        top.Sort((a, b) => b.sqrMagnitude.CompareTo(a.sqrMagnitude));
        var pick = new List<Vector3>(6);

        const float dupEps = 0.0001f;
        for (int i = 0; i < top.Count && pick.Count < 6; i++)
        {
            Vector3 p = top[i];
            bool dup = false;
            for (int j = 0; j < pick.Count; j++)
                if ((pick[j] - p).sqrMagnitude < dupEps) { dup = true; break; }
            if (!dup) pick.Add(p);
        }

        if (pick.Count != 6) return false;

        // Sort around center by angle
        pick.Sort((a, b) =>
        {
            float aa = Mathf.Atan2(a.z, a.x);
            float bb = Mathf.Atan2(b.z, b.x);
            return aa.CompareTo(bb);
        });

        ring = pick.ToArray();
        return true;
    }

    private void EnsureRoot()
    {
        if (_root != null) return;

        var t = transform.Find("_CastlePerimeterEdgesRoot");
        if (t != null) { _root = t; return; }

        var go = new GameObject("_CastlePerimeterEdgesRoot");
        go.transform.SetParent(transform, false);
        _root = go.transform;
    }

    private void ClearRoot()
    {
        if (_root == null) return;

        for (int i = _root.childCount - 1; i >= 0; i--)
        {
            var child = _root.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }
}
