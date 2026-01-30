using System.Collections.Generic;
using UnityEngine;

public sealed class HexGridSpawner : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int radius = 6;
    [SerializeField] private float hexSize = 1f;
    [SerializeField] private GameObject hexPrefab;

    [Header("Castle")]
    [SerializeField] private GameObject castlePrefab;

    public int Radius => radius;

    public Transform CastleTransform { get; private set; }
    public List<HexCellView> EdgeCells { get; private set; } = new List<HexCellView>();

    private void Start()
    {
        if (hexPrefab == null)
        {
            Debug.LogError("HexGridSpawner: hexPrefab is not assigned.");
            return;
        }

        SpawnGrid();
        SpawnCastle();
    }

    private void SpawnGrid()
    {
        EdgeCells.Clear();

        for (int q = -radius; q <= radius; q++)
        {
            int rMin = Mathf.Max(-radius, -q - radius);
            int rMax = Mathf.Min(radius, -q + radius);

            for (int r = rMin; r <= rMax; r++)
            {
                Vector3 pos = AxialToWorld(q, r, hexSize);
                GameObject go = Instantiate(hexPrefab, pos, Quaternion.identity, transform);
                go.name = $"Hex_{q}_{r}";

                var cell = go.GetComponent<HexCellView>();
                if (cell == null) cell = go.AddComponent<HexCellView>();
                cell.q = q;
                cell.r = r;

                bool isEdge =
                    Mathf.Abs(q) == radius ||
                    Mathf.Abs(r) == radius ||
                    Mathf.Abs(q + r) == radius;

                if (isEdge)
                    EdgeCells.Add(cell);
            }
        }
    }

    private void SpawnCastle()
    {
        if (castlePrefab == null)
        {
            Debug.LogWarning("HexGridSpawner: castlePrefab is not assigned.");
            return;
        }

        Vector3 pos = AxialToWorld(0, 0, hexSize);
        var castle = Instantiate(castlePrefab, pos + new Vector3(0f, 0.6f, 0f), Quaternion.identity, transform);
        castle.name = "Castle";
        CastleTransform = castle.transform;

        if (castle.GetComponent<CastleHealth>() == null)
            castle.AddComponent<CastleHealth>();

        var shooter = castle.GetComponent<CastleShooter>();
        if (shooter == null) shooter = castle.AddComponent<CastleShooter>();
        shooter.SetHexSize(hexSize);
    }

    private static Vector3 AxialToWorld(int q, int r, float size)
    {
        float x = size * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) * 0.5f * r);
        float z = size * (1.5f * r);
        return new Vector3(x, 0f, z);
    }
}
