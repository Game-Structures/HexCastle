using UnityEngine;

public sealed class Wall3DVisual : MonoBehaviour
{
    [Header("Materials (optional)")]
    [SerializeField] private Material wallMaterial;

    [Header("Dimensions (relative to innerRadius)")]
    [Range(0.02f, 0.5f)] [SerializeField] private float thicknessK = 0.12f;
    [Range(0.05f, 1.0f)] [SerializeField] private float heightK = 0.35f;

    [Header("Medieval segments (NEW)")]
    [Tooltip("Assign WallSegment_Medieval prefab here. If null – fallback to old cube walls.")]
    [SerializeField] private GameObject wallSegmentPrefab;

    // overrides (absolute values in world units) – used for castle ring only
    private bool _useAbsDims;
    private float _absThickness;
    private float _absHeight;

    private Transform _root;

    // dir order: 0..5, rotation 60° steps around Y.
    static Vector3 DirXZ(int d)
    {
        float ang = Mathf.Deg2Rad * (60f * d);

        // ВАЖНО: инвертируем Z, чтобы направления совпали с ориентацией гекс-сетки в проекте
        return new Vector3(Mathf.Cos(ang), 0f, -Mathf.Sin(ang));
    }

    public void SetDimensions(float thicknessAbs, float heightAbs)
    {
        _useAbsDims = true;
        _absThickness = Mathf.Max(0.01f, thicknessAbs);
        _absHeight = Mathf.Max(0.05f, heightAbs);
    }

    public void ClearDimensionsOverride() => _useAbsDims = false;

    private void EnsureRoot()
    {
        if (_root != null) return;

        var t = transform.Find("_Wall3D");
        if (t != null) { _root = t; return; }

        var go = new GameObject("_Wall3D");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        _root = go.transform;
    }

    private void ClearRoot()
    {
        EnsureRoot();

        for (int i = _root.childCount - 1; i >= 0; i--)
        {
            var child = _root.GetChild(i).gameObject;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private GameObject SpawnCube(string name, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;

        if (wallMaterial != null)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = wallMaterial;
        }

        return go;
    }

    private static int CountBits(int m)
    {
        int c = 0;
        for (int i = 0; i < 6; i++)
            if ((m & (1 << i)) != 0) c++;
        return c;
    }

    private void SpawnMedievalSegment(int d, float innerRadius, float thickness, float height)
    {
        if (wallSegmentPrefab == null) return;

        var seg = Instantiate(wallSegmentPrefab, _root);
        seg.name = $"Seg_{d}";
        seg.transform.localPosition = Vector3.zero;

        Vector3 dir = DirXZ(d);
        // Сегмент построен вдоль +X (от 0 до L), значит разворачиваем так, чтобы +X смотрел в dir
        seg.transform.localRotation = Quaternion.FromToRotation(Vector3.right, dir);
        seg.transform.localScale = Vector3.one;

        // Принудительно задаём размеры сегмента под текущий innerRadius
        var mesh = seg.GetComponent<MedievalWallSegmentMesh>();
        if (mesh != null)
        {
            mesh.autoLengthFromSceneTiles = false;
            mesh.length = innerRadius;
            mesh.thickness = thickness;
            mesh.height = height;
            mesh.Rebuild();
        }
    }

    /// <param name="connectedMask">6-bit: which directions have confirmed connections.</param>
    /// <param name="rotationSteps">0..5 visual rotation.</param>
    /// <param name="innerRadius">distance from hex center to edge midpoint in world units.</param>
    public void Build(int connectedMask, int rotationSteps, float innerRadius)
    {
        if (innerRadius <= 0.001f) innerRadius = 1.0f;

        EnsureRoot();
        ClearRoot();

        float thickness = _useAbsDims ? _absThickness : (innerRadius * thicknessK);
        float height = _useAbsDims ? _absHeight : (innerRadius * heightK);

        thickness = Mathf.Max(0.01f, thickness);
        height = Mathf.Max(0.05f, height);

        // ВАЖНО: крутим только внутренний root, а не внешний объект
        _root.localRotation = Quaternion.Euler(0f, rotationSteps * 60f, 0f);

        // NEW: если задан префаб сегмента – строим стену из сегментов
        if (wallSegmentPrefab != null)
        {
            for (int d = 0; d < 6; d++)
            {
                if ((connectedMask & (1 << d)) == 0) continue;
                SpawnMedievalSegment(d, innerRadius, thickness, height);
            }
            return;
        }

        // ---- Fallback: старые кубики (на случай если prefab не назначен) ----
        float hubSize = thickness * 1.4f;
        float baseY = height * 0.5f;

        int connCount = CountBits(connectedMask);

        if (connCount >= 2)
        {
            SpawnCube("Hub", _root,
                new Vector3(0f, baseY, 0f),
                Quaternion.identity,
                new Vector3(hubSize, height, hubSize));
        }

        for (int d = 0; d < 6; d++)
        {
            if ((connectedMask & (1 << d)) == 0) continue;

            Vector3 dir = DirXZ(d);
            float armLen = innerRadius;
            Vector3 centerPos = dir * (armLen * 0.5f);

            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            SpawnCube($"Arm_{d}", _root,
                centerPos + new Vector3(0f, baseY, 0f),
                rot,
                new Vector3(thickness, height, armLen));
        }
    }

    /// <summary>
    /// 6 сегментов по периметру гекса (по сторонам тайла).
    /// innerRadius: апофема (центр → середина ребра).
    /// </summary>
    public void BuildPerimeterRing(float innerRadius)
{
    if (innerRadius <= 0.001f) innerRadius = 1.0f;

    EnsureRoot();
    ClearRoot();

    float thickness = _useAbsDims ? _absThickness : (innerRadius * thicknessK);
    float height    = _useAbsDims ? _absHeight    : (innerRadius * heightK);

    thickness = Mathf.Max(0.01f, thickness);
    height    = Mathf.Max(0.05f, height);

    float baseY = height * 0.5f;

    // длина стороны = 2 * innerR * tan(30°)
    float sideLen = 2f * innerRadius * 0.577350269f;

    _root.localRotation = Quaternion.identity;

    // NEW: если назначен сегмент-префаб (WallSegment_Medieval / WallSegment_CastleSide) – строим из него
    if (wallSegmentPrefab != null)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector3 n = DirXZ(i);                         // нормаль к стороне (к её середине)
            Vector3 tangent = new Vector3(-n.z, 0f, n.x); // вдоль стороны

            var segGo = Instantiate(wallSegmentPrefab, _root);
            segGo.name = $"PerimSeg_{i}";

            // КЛЮЧЕВОЕ: ставим в центр стороны (без сдвига на halfLen)
            segGo.transform.localPosition = n * innerRadius;
            segGo.transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
            segGo.transform.localScale = Vector3.one;

            var mesh = segGo.GetComponent<MedievalWallSegmentMesh>();
            if (mesh == null) mesh = segGo.GetComponentInChildren<MedievalWallSegmentMesh>(true);

            if (mesh != null)
            {
                // ожидается, что у сегмента включен CenterOnX (тогда pivot по центру длины)
                mesh.length = sideLen;
                mesh.thickness = thickness;
                mesh.height = height;
                mesh.Rebuild();
            }
        }
        return;
    }

    // fallback: кубы
    for (int i = 0; i < 6; i++)
    {
        Vector3 n = DirXZ(i);
        Vector3 tangent = new Vector3(-n.z, 0f, n.x);
        Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);

        SpawnCube($"PerimSeg_{i}", _root,
            n * innerRadius + new Vector3(0f, baseY, 0f),
            rot,
            new Vector3(thickness, height, sideLen));
    }
}


}
