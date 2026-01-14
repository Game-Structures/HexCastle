using UnityEngine;

public sealed class CastleWallRingPerimeter : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private Material wallMaterial;

    [Header("Size (relative to innerRadius)")]
    [SerializeField] private float thicknessK = 0.10f; // толщина
    [SerializeField] private float heightK = 0.28f;    // высота

    [Header("Fit")]
    [SerializeField] private bool autoFitFromTileBounds = true;
    [SerializeField] private float manualInnerRadius = 0.40f;
    [SerializeField] private float innerRadiusPadding = 0.02f;

    [Header("Placement")]
    [SerializeField] private float yOffset = 0.02f;
    [Tooltip("0 = по граням, 30 = по вершинам")]
    [SerializeField] private float rotationOffsetDeg = 0f;

    [Tooltip("Push segments inward by half thickness so they sit on hex border (recommended).")]
    [SerializeField] private bool insetByHalfThickness = true;

    [Tooltip("Extra radial offset (world units). Negative -> further inside.")]
    [SerializeField] private float radialOffset = 0f;

    [Tooltip("Small overlap to avoid tiny seams between segments.")]
    [SerializeField] private float lengthOverlap = 0.02f;

    private Transform ringRoot;

    private void Start()
    {
        Rebuild();
    }

    public void Rebuild()
    {
        Clear();

        float innerR = autoFitFromTileBounds ? TryGetInnerRadiusFromAnyTile() : manualInnerRadius;
        innerR = Mathf.Max(0.05f, innerR - innerRadiusPadding);

        // outer radius R = innerR / cos30, side length = R
        const float COS30 = 0.8660254f;
        float outerR = innerR / COS30;
        float sideLen = outerR;

        float thickness = Mathf.Max(0.02f, innerR * thicknessK);
        float height = Mathf.Max(0.05f, innerR * heightK);

        var rootGo = new GameObject("CastleWallRing");
        rootGo.transform.SetParent(transform, false);
        rootGo.transform.localPosition = new Vector3(0f, yOffset, 0f);
        rootGo.transform.localRotation = Quaternion.identity;
        rootGo.transform.localScale = Vector3.one;
        ringRoot = rootGo.transform;

        // делаем 6 сегментов по сторонам гекса
        for (int i = 0; i < 6; i++)
        {
            float ang = rotationOffsetDeg + i * 60f;

            // normal смотрит наружу
            Quaternion rotNormal = Quaternion.Euler(0f, ang, 0f);
            // вдоль ребра
            Quaternion rotAlongSide = rotNormal * Quaternion.Euler(0f, 90f, 0f);

            // центр ребра на расстоянии innerR от центра
            float centerDist = innerR + radialOffset;
            if (insetByHalfThickness) centerDist -= thickness * 0.5f;

            Vector3 sideCenter = rotNormal * Vector3.forward * centerDist;

            // контейнер сегмента (удобно для локальных координат)
            var segRoot = new GameObject($"RingSeg_{i}");
            segRoot.transform.SetParent(ringRoot, false);
            segRoot.transform.localPosition = sideCenter;
            segRoot.transform.localRotation = rotAlongSide;
            segRoot.transform.localScale = Vector3.one;
            segRoot.layer = 2; // Ignore Raycast

            float baseLen = sideLen + lengthOverlap;

            // BASE
            var baseCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseCube.name = "Base";
            baseCube.transform.SetParent(segRoot.transform, false);
            baseCube.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            baseCube.transform.localRotation = Quaternion.identity;
            baseCube.transform.localScale = new Vector3(baseLen, height, thickness);
            baseCube.layer = 2;

            var baseCol = baseCube.GetComponent<Collider>();
            if (baseCol != null) Destroy(baseCol);

            var baseMr = baseCube.GetComponent<MeshRenderer>();
            if (baseMr != null && wallMaterial != null) baseMr.sharedMaterial = wallMaterial;
        }
    }

    private void Clear()
    {
        if (ringRoot != null)
        {
            Destroy(ringRoot.gameObject);
            ringRoot = null;
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

        return (innerR <= 0.001f) ? manualInnerRadius : innerR;
    }
}
