using UnityEngine;

public sealed class CastlePerimeterWall : MonoBehaviour
{
    [Header("Wall 3D Prefab (Wall3D_Prefab)")]
    [SerializeField] private GameObject wall3DPrefab;

    [Header("Look (optional)")]
    [SerializeField] private Material wallMaterial;

    [Header("Wall Size (applied via Wall3DVisual.SetDimensions)")]
    [SerializeField] private float thickness = 0.35f;
    [SerializeField] private float height = 0.8f;

    [Header("Perimeter Settings")]
    [Tooltip("Inner radius (apothem) used for ring placement. Ты ставил ~0.75.")]
    [SerializeField] private float innerRadius = 0.75f;

    [Tooltip("Сдвиг внутрь/наружу (положительное – чуть дальше от центра).")]
    [SerializeField] private float offset = 0.0f;

    [Header("Placement")]
    [SerializeField] private float yOffset = 0.02f;

    private Transform _root;

    private void Start()
    {
        Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (wall3DPrefab == null)
        {
            Debug.LogWarning("[CastlePerimeterWall] Wall3D Prefab is not set.");
            return;
        }

        EnsureRoot();
        ClearRoot();

        var go = Instantiate(wall3DPrefab, _root);
        go.name = "CastlePerimeterRing_Visual";
        go.transform.localPosition = new Vector3(0f, yOffset, 0f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var vis = go.GetComponent<Wall3DVisual>();
        if (vis == null) vis = go.AddComponent<Wall3DVisual>();

        // Абсолютные размеры (в world units), чтобы совпадало с Wall3D_Prefab
        vis.SetDimensions(thickness, height);

        float r = Mathf.Max(0.05f, innerRadius + offset);

        // ВАЖНО: именно кольцо по контуру гекса, не лучи из центра
        vis.BuildPerimeterRing(r);

        // Материал (если указан)
        if (wallMaterial != null)
        {
            var mrs = go.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < mrs.Length; i++)
                mrs[i].sharedMaterial = wallMaterial;
        }

        Debug.Log($"[CastlePerimeterWall] Built PERIMETER RING r={r:0.###} under {_root.name}");
    }

    private void EnsureRoot()
    {
        if (_root != null) return;

        var t = transform.Find("_CastlePerimeterWallRoot");
        if (t != null) { _root = t; return; }

        var rootGo = new GameObject("_CastlePerimeterWallRoot");
        rootGo.transform.SetParent(transform, false);
        _root = rootGo.transform;
    }

    private void ClearRoot()
    {
        if (_root == null) return;

        for (int i = _root.childCount - 1; i >= 0; i--)
            Destroy(_root.GetChild(i).gameObject);
    }
}
