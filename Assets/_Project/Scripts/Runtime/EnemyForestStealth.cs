using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyForestStealth : MonoBehaviour
{
    public bool IsHidden { get; private set; }

    private Dictionary<Vector2Int, HexCellView> cellsByAxial;
    private float timer;

    private void Awake()
    {
        BuildCache();
    }

    private void BuildCache()
    {
        cellsByAxial = new Dictionary<Vector2Int, HexCellView>(512);
        var cells = FindObjectsByType<HexCellView>(FindObjectsSortMode.None);
        for (int i = 0; i < cells.Length; i++)
        {
            var k = new Vector2Int(cells[i].q, cells[i].r);
            if (!cellsByAxial.ContainsKey(k))
                cellsByAxial.Add(k, cells[i]);
        }
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = 0.15f;

        if (cellsByAxial == null || cellsByAxial.Count == 0)
            BuildCache();

        var cell = FindNearestCell(transform.position);
        if (cell == null)
        {
            IsHidden = false;
            return;
        }

        var tag = cell.GetComponent<HexCastle.Map.MapTerrainTag>();
        IsHidden = (tag != null && tag.type == HexCastle.Map.MapTerrainType.Forest);
    }

    private HexCellView FindNearestCell(Vector3 worldPos)
    {
        HexCellView best = null;
        float bestD = float.MaxValue;

        foreach (var kv in cellsByAxial)
        {
            var c = kv.Value;
            if (c == null) continue;

            float d = (c.transform.position - worldPos).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = c;
            }
        }

        return best;
    }
}
