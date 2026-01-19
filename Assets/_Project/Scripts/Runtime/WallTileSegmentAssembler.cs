using UnityEngine;

[ExecuteAlways]
public class WallTileSegmentAssembler : MonoBehaviour
{
    [Header("Prefab of one wall segment (WallSegment_Medieval)")]
    public GameObject segmentPrefab;

    [Header("Wall data")]
    public WallTileType tileType = WallTileType.Straight;

    [Range(0, 5)] public int rotSteps;
    public float yRotationOffsetDeg = 0f;

    [Header("Auto rebuild")]
    public bool rebuildInEdit = true;
    public bool rebuildInPlay = true;

    const string ChildRootName = "__WallSegments";
    Transform _root;

    WallTileType _lastType;
    int _lastRot = int.MinValue;
    float _lastOff = float.NaN;
    GameObject _lastPrefab;

    bool _pendingRebuild;

    void OnEnable()
    {
        EnsureRoot();
        MarkDirty();
    }

    void OnValidate()
    {
        EnsureRoot();
        MarkDirty();
    }

    void Update()
    {
        bool should =
            (Application.isPlaying && rebuildInPlay) ||
            (!Application.isPlaying && rebuildInEdit);

        if (!should) return;

        if (_pendingRebuild ||
            _lastType != tileType || _lastRot != rotSteps || _lastOff != yRotationOffsetDeg || _lastPrefab != segmentPrefab)
        {
            DoRebuild();
        }
    }

    void MarkDirty() => _pendingRebuild = true;

    void EnsureRoot()
    {
        if (_root != null) return;

        var t = transform.Find(ChildRootName);
        if (t != null) _root = t;
        else
        {
            var go = new GameObject(ChildRootName);
            go.transform.SetParent(transform, false);
            _root = go.transform;
        }
    }

    [ContextMenu("Force Rebuild")]
    public void ForceRebuild()
    {
        EnsureRoot();
        MarkDirty();
    }

    static int BaseMask(WallTileType type)
    {
        // База – как ты фиксировал раньше:
        // Straight: A-D (0,3)
        // SmallCurve: D-C (3,2)
        // StrongCurve: B-D (1,3)
        // Split: B-D-F (1,3,5)
        switch (type)
        {
            case WallTileType.Straight:    return (1 << 0) | (1 << 3);
            case WallTileType.SmallCurve:  return (1 << 3) | (1 << 2);
            case WallTileType.StrongCurve: return (1 << 1) | (1 << 3);
            case WallTileType.Split:       return (1 << 1) | (1 << 3) | (1 << 5);
            default:                       return 0;
        }
    }

    static int RotateMask(int mask, int rot)
    {
        rot = ((rot % 6) + 6) % 6;
        int outMask = 0;
        for (int i = 0; i < 6; i++)
        {
            if (((mask >> i) & 1) == 0) continue;
            int j = (i + rot) % 6;
            outMask |= (1 << j);
        }
        return outMask;
    }

    void DoRebuild()
    {
        EnsureRoot();

        _pendingRebuild = false;

        _lastType = tileType;
        _lastRot = rotSteps;
        _lastOff = yRotationOffsetDeg;
        _lastPrefab = segmentPrefab;

        for (int i = _root.childCount - 1; i >= 0; i--)
        {
            var c = _root.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(c);
            else Destroy(c);
#else
            Destroy(c);
#endif
        }

        if (segmentPrefab == null) return;

        int mask = RotateMask(BaseMask(tileType), rotSteps);

        for (int dir = 0; dir < 6; dir++)
        {
            if (((mask >> dir) & 1) == 0) continue;

            float ang = 60f * dir + yRotationOffsetDeg;

#if UNITY_EDITOR
            GameObject seg = !Application.isPlaying
                ? (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(segmentPrefab, _root)
                : Instantiate(segmentPrefab, _root);
#else
            GameObject seg = Instantiate(segmentPrefab, _root);
#endif

            seg.name = $"Seg_{dir}";
            seg.transform.localPosition = Vector3.zero;
            seg.transform.localRotation = Quaternion.Euler(0f, ang, 0f);
            seg.transform.localScale = Vector3.one;
        }
    }
}
