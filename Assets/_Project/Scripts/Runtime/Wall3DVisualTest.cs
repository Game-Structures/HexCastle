using UnityEngine;
using System.Reflection;

public class Wall3DVisualTest : MonoBehaviour
{
    [Header("Auto")]
    public Wall3DVisual target;

    [Header("Auto Fit (use Grid object here)")]
    public bool autoFitToHex = true;
    public GameObject hexSource; // drag Grid here
    public bool radiusIsOuter = true;
    public float fit = 1.0f;

    [Header("Shape (letters match TilePlacement)")]
    [Range(0, 5)] public int rotationSteps = 0;

    // TilePlacement mapping:
    // A=NE(1), B=E(0), C=SE(5), D=SW(4), E=W(3), F=NW(2)
    public bool A; // NE (dir 1)
    public bool B; // E  (dir 0)
    public bool C; // SE (dir 5)
    public bool D; // SW (dir 4)
    public bool E; // W  (dir 3)
    public bool F; // NW (dir 2)

    [Header("Manual Size (if autoFitToHex=false or hexSource not set)")]
    public float innerRadius = 1.0f;

    [Header("Runtime auto rebuild")]
    public bool autoRebuildInPlayMode = true;

    int _lastHash;

    void Awake()
    {
        EnsureTarget();
    }

    void Start()
    {
        Rebuild();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (!autoRebuildInPlayMode) return;

        int h = CalcHash();
        if (h != _lastHash)
        {
            Rebuild();
        }
    }

    [ContextMenu("Rebuild Now")]
    public void RebuildNow_Menu()
    {
        EnsureTarget();
        Rebuild();
    }

    void EnsureTarget()
    {
        if (target == null) target = GetComponent<Wall3DVisual>();
        if (target == null) target = gameObject.AddComponent<Wall3DVisual>();
    }

    int CalcHash()
    {
        int mask = MakeMask();
        float r = ComputeInnerRadius();
        unchecked
        {
            int h = 17;
            h = h * 31 + mask;
            h = h * 31 + rotationSteps;
            h = h * 31 + Mathf.RoundToInt(r * 10000f);
            return h;
        }
    }

    int MakeMask()
    {
        int m = 0;
        // 0:E, 1:NE, 2:NW, 3:W, 4:SW, 5:SE
        if (B) m |= (1 << 0); // B = E
        if (A) m |= (1 << 1); // A = NE
        if (F) m |= (1 << 2); // F = NW
        if (E) m |= (1 << 3); // E = W
        if (D) m |= (1 << 4); // D = SW
        if (C) m |= (1 << 5); // C = SE
        return m;
    }

    float TryGetHexOuterRadiusFromGrid(GameObject go)
    {
        if (go == null) return -1f;

        var monos = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < monos.Length; i++)
        {
            var mb = monos[i];
            if (mb == null) continue;

            var t = mb.GetType();
            if (!t.Name.Contains("HexTileMesh")) continue;

            var f = t.GetField("Radius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                 ?? t.GetField("radius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (f != null && f.FieldType == typeof(float))
                return (float)f.GetValue(mb);

            var p = t.GetProperty("Radius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                 ?? t.GetProperty("radius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (p != null && p.PropertyType == typeof(float))
                return (float)p.GetValue(mb);
        }

        return -1f;
    }

    float ComputeInnerRadius()
    {
        if (!autoFitToHex || hexSource == null)
            return Mathf.Max(0.001f, innerRadius);

        float outerR = TryGetHexOuterRadiusFromGrid(hexSource);
        if (outerR <= 0f)
            return Mathf.Max(0.001f, innerRadius);

        float r = radiusIsOuter ? outerR * 0.8660254f : outerR; // cos(30°)
        r *= Mathf.Max(0.01f, fit);
        return Mathf.Max(0.001f, r);
    }

    void Rebuild()
    {
        if (target == null) return;

        int mask = MakeMask();
        float r = ComputeInnerRadius();
        target.Build(mask, rotationSteps, r);

        _lastHash = CalcHash();
    }
}
