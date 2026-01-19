using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MedievalWallSegmentMesh : MonoBehaviour
{
    [Header("Main size")]
    [Tooltip("Длина сегмента от центра тайла до середины ребра (inner radius / apothem). Если autoLength включен – это значение игнорируется.")]
    public float length = 1.0f;
    public float thickness = 0.25f;
    public float height = 0.45f;

    [Header("Auto length (from any tile bounds)")]
    public bool autoLengthFromSceneTiles = true;
    [Tooltip("Используется, если в сцене не найден ни один тайл/рендерер.")]
    public float autoLengthFallback = 1.0f;

    [Header("Crenels (two rows on edges)")]
    public bool crenels = true;
    [Range(0.02f, 0.4f)] public float crenelWidthK = 0.125f;
    [Range(0.00f, 0.4f)] public float crenelGapK = 0.08f;
    [Range(0.05f, 0.8f)] public float crenelDepthK = 0.20f;
    [Range(0.05f, 0.8f)] public float crenelHeightK = 0.20f;
    [Range(0.00f, 0.45f)] public float edgeInsetK = 0.05f;
    [Range(0.00f, 0.45f)] public float endPaddingK = 0.12f;

    [Header("Materials")]
    public Material wallMaterial;
    public Material crenelsMaterial;

    MeshFilter _mf;
    MeshRenderer _mr;

    void OnEnable()
    {
        Ensure();
        Rebuild();
    }

    void OnValidate()
    {
        Ensure();
        Rebuild();
    }

    void Ensure()
    {
        if (_mf == null) _mf = GetComponent<MeshFilter>();
        if (_mr == null) _mr = GetComponent<MeshRenderer>();
        ApplyMaterials();
    }

    void ApplyMaterials()
    {
        bool useCrenelsSubmesh = crenels && crenelsMaterial != null && wallMaterial != null;

        if (useCrenelsSubmesh)
            _mr.sharedMaterials = new[] { wallMaterial, crenelsMaterial };
        else if (wallMaterial != null)
            _mr.sharedMaterials = new[] { wallMaterial };
        else if (crenelsMaterial != null)
            _mr.sharedMaterials = new[] { crenelsMaterial };
    }

    float ResolveLength(float fallback)
    {
        if (!autoLengthFromSceneTiles)
            return fallback;

        float innerR = TryGetInnerRadiusFromAnyTile();
        if (innerR > 0.001f)
            return innerR;

        return Mathf.Max(0.001f, autoLengthFallback > 0f ? autoLengthFallback : fallback);
    }

    // Берём bounds любого тайла и вычисляем inner radius (apothem) как в CastleWallAnchorSpawner
    float TryGetInnerRadiusFromAnyTile()
    {
        var anyCell = FindFirstObjectByType<HexCellView>();
        if (anyCell == null) return 0f;

        var mr = anyCell.GetComponentInChildren<MeshRenderer>(true);
        if (mr == null) return 0f;

        var b = mr.bounds;
        float sizeX = b.size.x;
        float sizeZ = b.size.z;

        const float SQRT3 = 1.7320508f;
        const float COS30 = 0.8660254f;

        // Оценка outer radius R по двум осям для pointy/flat, потом выбираем более согласованную.
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

        return innerR;
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        Ensure();

        float L = ResolveLength(Mathf.Max(0.001f, length));
        float T = Mathf.Max(0.001f, thickness);
        float H = Mathf.Max(0.001f, height);

        bool useCrenelsSubmesh = crenels && crenelsMaterial != null && wallMaterial != null;

        var verts = new List<Vector3>(2048);
        var norms = new List<Vector3>(2048);
        var uvs = new List<Vector2>(2048);
        var tris0 = new List<int>(4096); // корпус
        var tris1 = new List<int>(4096); // зубчики

        void AddBox(float x0, float x1, float y0, float y1, float z0, float z1, bool toCrenels)
        {
            int v0 = verts.Count;

            // +X
            verts.Add(new Vector3(x1, y0, z0)); verts.Add(new Vector3(x1, y0, z1)); verts.Add(new Vector3(x1, y1, z1)); verts.Add(new Vector3(x1, y1, z0));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.right);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            // -X
            verts.Add(new Vector3(x0, y0, z1)); verts.Add(new Vector3(x0, y0, z0)); verts.Add(new Vector3(x0, y1, z0)); verts.Add(new Vector3(x0, y1, z1));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.left);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            // +Y
            verts.Add(new Vector3(x0, y1, z0)); verts.Add(new Vector3(x1, y1, z0)); verts.Add(new Vector3(x1, y1, z1)); verts.Add(new Vector3(x0, y1, z1));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.up);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            // -Y
            verts.Add(new Vector3(x0, y0, z1)); verts.Add(new Vector3(x1, y0, z1)); verts.Add(new Vector3(x1, y0, z0)); verts.Add(new Vector3(x0, y0, z0));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.down);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            // +Z
            verts.Add(new Vector3(x1, y0, z1)); verts.Add(new Vector3(x0, y0, z1)); verts.Add(new Vector3(x0, y1, z1)); verts.Add(new Vector3(x1, y1, z1));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.forward);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            // -Z
            verts.Add(new Vector3(x0, y0, z0)); verts.Add(new Vector3(x1, y0, z0)); verts.Add(new Vector3(x1, y1, z0)); verts.Add(new Vector3(x0, y1, z0));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.back);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));

            void AddTris(List<int> t, int baseIndex)
            {
                for (int f = 0; f < 6; f++)
                {
                    int o = baseIndex + f * 4;
                    t.Add(o + 0); t.Add(o + 2); t.Add(o + 1);
                    t.Add(o + 0); t.Add(o + 3); t.Add(o + 2);
                }
            }

            AddTris(toCrenels ? tris1 : tris0, v0);
        }

        // Корпус: от центра тайла (0) к ребру (+X)
        AddBox(0f, L, 0f, H, -T * 0.5f, T * 0.5f, false);

        // Зубчики: 2 ряда по краям толщины
        if (crenels)
        {
            float w = Mathf.Clamp(L * crenelWidthK, 0.02f, L);
            float g = Mathf.Clamp(L * crenelGapK, 0.00f, L);
            float step = Mathf.Max(0.01f, w + g);

            float pad = Mathf.Clamp(L * endPaddingK, 0f, L * 0.45f);
            float xStart = 0f + pad;
            float xEnd = L - pad;

            float d = Mathf.Clamp(T * crenelDepthK, 0.02f, T);
            float inset = Mathf.Clamp(T * edgeInsetK, 0f, T * 0.45f);

            float zLeft0 = -T * 0.5f + inset;
            float zLeft1 = zLeft0 + d;

            float zRight1 = T * 0.5f - inset;
            float zRight0 = zRight1 - d;

            float h2 = Mathf.Clamp(H * crenelHeightK, 0.02f, H * 2f);
            float y0 = H;
            float y1 = H + h2;

            float x = xStart;
            int safety = 0;
            while (x + w <= xEnd && safety++ < 2000)
            {
                bool toCrenels = useCrenelsSubmesh;

                AddBox(x, x + w, y0, y1, zLeft0, zLeft1, toCrenels);
                AddBox(x, x + w, y0, y1, zRight0, zRight1, toCrenels);

                x += step;
            }
        }

        var m = new Mesh();
        m.name = "MedievalWallSegment";
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);

        if (useCrenelsSubmesh)
        {
            m.subMeshCount = 2;
            m.SetTriangles(tris0, 0);
            m.SetTriangles(tris1, 1);
        }
        else
        {
            m.subMeshCount = 1;
            m.SetTriangles(tris0, 0);
        }

        m.RecalculateBounds();
        _mf.sharedMesh = m;

        ApplyMaterials();
    }
    [ContextMenu("Rebuild Mesh")]
private void RebuildMeshMenu()
{
    Rebuild();
}

}
