using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class UpgradeChevron3D : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField, Range(0.2f, 5f)] private float width = 1.2f;
    [SerializeField, Range(0.05f, 3f)] private float height = 0.6f;
    [SerializeField, Range(0.02f, 1f)] private float thickness = 0.08f;
    [SerializeField, Range(0.02f, 1f)] private float armThickness = 0.18f;

    [Header("Look")]
    [SerializeField] private Color goldColor = new Color(1.0f, 0.78f, 0.15f, 1f);
    [SerializeField] private bool useUnlit = false;

    private MeshFilter mf;
    private MeshRenderer mr;

    private void Awake()
    {
        Ensure();
        Rebuild();
        ApplyMaterial();
    }

    private void OnValidate()
    {
        Ensure();
        Rebuild();
        ApplyMaterial();
    }

    private void Ensure()
    {
        if (mf == null) mf = GetComponent<MeshFilter>();
        if (mr == null) mr = GetComponent<MeshRenderer>();
    }

    private void ApplyMaterial()
    {
        if (mr == null) return;

        Shader sh = Shader.Find(useUnlit ? "Unlit/Color" : "Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Unlit/Color");

        var mat = new Material(sh);

        // URP Lit / Standard
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", goldColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", goldColor);

        // немного “металлик” если есть
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.85f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.75f);

        mr.sharedMaterial = mat;
    }

    private void Rebuild()
    {
        if (mf == null) return;

        width = Mathf.Max(0.2f, width);
        height = Mathf.Max(0.05f, height);
        thickness = Mathf.Max(0.01f, thickness);
        armThickness = Mathf.Clamp(armThickness, 0.02f, Mathf.Min(width * 0.45f, 1f));

        // Строим V-образный “шеврон” как экструзия 2D-полилинии (7 точек, замкнутый контур)
        // Контур в XY, экструзия по Z.
        float w = width * 0.5f;
        float h = height;

        // Направление “рук” V
        Vector2 leftTop = new Vector2(-w, h);
        Vector2 mid = new Vector2(0f, 0f);
        Vector2 rightTop = new Vector2(w, h);

        // Толщина “рук” задаётся как смещение перпендикуляром к сегментам
        Vector2 dL = (mid - leftTop).normalized;   // вниз-вправо
        Vector2 dR = (rightTop - mid).normalized;  // вверх-вправо

        Vector2 nL = new Vector2(-dL.y, dL.x) * (armThickness * 0.5f);
        Vector2 nR = new Vector2(-dR.y, dR.x) * (armThickness * 0.5f);

        // Внешний контур (примерно)
        Vector2 p0 = leftTop + nL;
        Vector2 p1 = mid + nL;
        Vector2 p2 = mid + nR;
        Vector2 p3 = rightTop + nR;

        // Внутренний контур (обратно)
        Vector2 p4 = rightTop - nR;
        Vector2 p5 = mid - nR;
        Vector2 p6 = mid - nL;
        Vector2 p7 = leftTop - nL;

        Vector2[] poly = new[] { p0, p1, p2, p3, p4, p5, p6, p7 };

        // Триангуляция “веером” от центра (достаточно для выпуклого контура; наш контур выпуклый)
        // Экструзия: фронт + бэк + сайды
        var mesh = new Mesh();
        mesh.name = "UpgradeChevron3D";

        int vCountFront = poly.Length;
        int vCountBack = poly.Length;

        Vector3[] verts = new Vector3[vCountFront + vCountBack];
        Vector3[] norms = new Vector3[verts.Length];
        Vector2[] uvs = new Vector2[verts.Length];

        float zF = thickness * 0.5f;
        float zB = -thickness * 0.5f;

        // Front
        for (int i = 0; i < poly.Length; i++)
        {
            verts[i] = new Vector3(poly[i].x, poly[i].y, zF);
            norms[i] = Vector3.forward;
            uvs[i] = new Vector2((poly[i].x / width) + 0.5f, (poly[i].y / height));
        }

        // Back
        for (int i = 0; i < poly.Length; i++)
        {
            int idx = vCountFront + i;
            verts[idx] = new Vector3(poly[i].x, poly[i].y, zB);
            norms[idx] = Vector3.back;
            uvs[idx] = uvs[i];
        }

        // Indices
        // Front fan from vertex 0
        System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>(256);
        for (int i = 1; i < poly.Length - 1; i++)
        {
            tris.Add(0);
            tris.Add(i);
            tris.Add(i + 1);
        }

        // Back fan (reverse winding)
        int off = vCountFront;
        for (int i = 1; i < poly.Length - 1; i++)
        {
            tris.Add(off + 0);
            tris.Add(off + i + 1);
            tris.Add(off + i);
        }

        // Sides
        for (int i = 0; i < poly.Length; i++)
        {
            int ni = (i + 1) % poly.Length;

            int f0 = i;
            int f1 = ni;
            int b0 = off + i;
            int b1 = off + ni;

            // two triangles per edge
            tris.Add(f0);
            tris.Add(f1);
            tris.Add(b1);

            tris.Add(f0);
            tris.Add(b1);
            tris.Add(b0);
        }

        mesh.vertices = verts;
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;
    }
}
