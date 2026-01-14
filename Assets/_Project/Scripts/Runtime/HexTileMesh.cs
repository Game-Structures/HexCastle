using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class HexTileMesh : MonoBehaviour
{
    [SerializeField] private float radius = 1f;
    [SerializeField] private float thickness = 0.05f;
    [SerializeField] private bool pointyTop = true;

    private MeshFilter mf;
    private MeshCollider mc;

#if UNITY_EDITOR
    private bool rebuildQueued;
#endif

    private void Awake()
    {
        Ensure();

        // В Edit Mode не перестраиваем меш прямо в Awake (чтобы не ловить warning)
        if (Application.isPlaying) RebuildNow();
        else QueueRebuild();
    }

    private void OnEnable()
    {
        Ensure();

        if (Application.isPlaying) RebuildNow();
        else QueueRebuild();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Ensure();
        QueueRebuild();
    }

    private void QueueRebuild()
    {
        if (rebuildQueued) return;
        rebuildQueued = true;

        EditorApplication.delayCall += DelayedRebuild;
    }

    private void DelayedRebuild()
    {
        // этот callback может прийти, когда объект уже удалён
        if (this == null) return;

        rebuildQueued = false;

        // если уже пошёл Play Mode — пусть перестройка идёт через Awake/OnEnable
        if (Application.isPlaying) return;

        Ensure();
        RebuildNow();
    }
#endif

    private void Ensure()
    {
        if (mf == null) mf = GetComponent<MeshFilter>();

        if (mc == null) mc = GetComponent<MeshCollider>();
        if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
    }

    // Оставляем публичный метод, если ты дергаешь его извне
    public void Rebuild()
    {
        RebuildNow();
    }

    private void RebuildNow()
    {
        if (mf == null) return;

        var mesh = BuildHex(radius, thickness, pointyTop);
        mesh.name = "HexTileMesh_Runtime";

        mf.sharedMesh = mesh;

        if (mc != null)
        {
            mc.sharedMesh = null;
            mc.sharedMesh = mesh;
        }
    }

    private static Mesh BuildHex(float r, float t, bool pointy)
    {
        Vector3[] top = new Vector3[6];
        Vector3[] bot = new Vector3[6];

        float angleOffset = pointy ? -90f : 30f;

        for (int i = 0; i < 6; i++)
        {
            float ang = Mathf.Deg2Rad * (angleOffset + i * 60f);
            float x = Mathf.Cos(ang) * r;
            float z = Mathf.Sin(ang) * r;
            top[i] = new Vector3(x, 0f, z);
            bot[i] = new Vector3(x, -t, z);
        }

        var v = new Vector3[14];
        for (int i = 0; i < 6; i++)
        {
            v[i] = top[i];
            v[i + 6] = bot[i];
        }
        v[12] = Vector3.zero;
        v[13] = new Vector3(0f, -t, 0f);

        var tris = new System.Collections.Generic.List<int>(6 * 3 * 4);

        for (int i = 0; i < 6; i++)
        {
            int a = i;
            int b = (i + 1) % 6;
            tris.Add(12); tris.Add(a); tris.Add(b);
        }

        for (int i = 0; i < 6; i++)
        {
            int a = i + 6;
            int b = ((i + 1) % 6) + 6;
            tris.Add(13); tris.Add(b); tris.Add(a);
        }

        for (int i = 0; i < 6; i++)
        {
            int topA = i;
            int topB = (i + 1) % 6;
            int botA = i + 6;
            int botB = ((i + 1) % 6) + 6;

            tris.Add(topA); tris.Add(botA); tris.Add(botB);
            tris.Add(topA); tris.Add(botB); tris.Add(topB);
        }

        var m = new Mesh();
        m.vertices = v;
        m.triangles = tris.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
