using UnityEngine;

public sealed class CastleWallAnchorSpawner : MonoBehaviour
{
    [Header("Prefab (same as in TilePlacement: Wall3D_Prefab)")]
    [SerializeField] private GameObject wall3DPrefab;

    [Header("Fit (auto from any tile MeshRenderer bounds)")]
    [SerializeField] private bool autoFitFromTileBounds = true;

    [Tooltip("Used if autoFitFromTileBounds=false OR no tile found")]
    [SerializeField] private float manualInnerRadius = 0.40f;

    [Tooltip("Inner radius padding (world units).")]
    [SerializeField] private float innerRadiusPadding = 0.02f;

    [Header("Placement")]
    [SerializeField] private float yOffset = 0.02f;

    [Header("Visibility")]
    [Tooltip("Hide renderers so anchor is invisible (keeps it as 'reference').")]
    [SerializeField] private bool hideRenderers = true;

    [Header("Safety")]
    [Tooltip("Disable all colliders on spawned anchor (visual only).")]
    [SerializeField] private bool disableAllColliders = true;

    private GameObject spawned;

    private void Start()
    {
        SpawnOrRebuild();
    }

    public void SpawnOrRebuild()
    {
        if (wall3DPrefab == null)
        {
            Debug.LogWarning("[CastleWallAnchor] wall3DPrefab is not set.");
            return;
        }

        if (spawned != null)
        {
            Destroy(spawned);
            spawned = null;
        }

        float innerR = autoFitFromTileBounds ? TryGetInnerRadiusFromAnyTile() : manualInnerRadius;
        innerR = Mathf.Max(0.05f, innerR - innerRadiusPadding);

        spawned = Instantiate(wall3DPrefab, transform);
        spawned.name = "CastleWallAnchor_3D";
        spawned.transform.localPosition = new Vector3(0f, yOffset, 0f);
        spawned.transform.localRotation = Quaternion.identity;
        spawned.transform.localScale = Vector3.one;

        var vis = spawned.GetComponent<Wall3DVisual>();
        if (vis == null) vis = spawned.GetComponentInChildren<Wall3DVisual>(true);

        if (vis == null)
        {
            Debug.LogError("[CastleWallAnchor] Wall3DVisual not found on prefab instance.");
            return;
        }

        const int allDirsMask = 63; // 0b111111
        const int rotSteps = 0;

        vis.Build(allDirsMask, rotSteps, innerR);

        if (disableAllColliders)
        {
            var cols = spawned.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
        }

        if (hideRenderers)
        {
            var rends = spawned.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++) rends[i].enabled = false;
        }
    }

    private float TryGetInnerRadiusFromAnyTile()
    {
        var anyCell = FindFirstObjectByType<HexCellView>();
        if (anyCell == null) return manualInnerRadius;

        var mr = anyCell.GetComponentInChildren<MeshRenderer>(true);
        if (mr == null) return manualInnerRadius;

        var b = mr.bounds;
        float sizeX = b.size.x;
        float sizeZ = b.size.z;

        const float SQRT3 = 1.7320508f;
        const float COS30 = 0.8660254f;

        float R_pointy_fromZ = sizeZ * 0.5f;
        float R_pointy_fromX = sizeX / SQRT3;
        float errPointy = Mathf.Abs(R_pointy_fromZ - R_pointy_fromX);
        float R_pointy = (R_pointy_fromZ + R_pointy_fromX) * 0.5f;

        float R_flat_fromX = sizeX * 0.5f;
        float R_flat_fromZ = sizeZ / SQRT3;
        float errFlat = Mathf.Abs(R_flat_fromX - R_flat_fromZ);
        float R_flat = (R_flat_fromX + R_flat_fromZ) * 0.5f;

        float R = (errPointy <= errFlat) ? R_pointy : R_flat;
        float innerR = R * COS30;

        if (innerR <= 0.001f) return manualInnerRadius;
        return innerR;
    }
}
