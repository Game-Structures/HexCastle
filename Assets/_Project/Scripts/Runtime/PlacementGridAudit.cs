using System.Collections.Generic;
using UnityEngine;

public sealed class PlacementGridAudit : MonoBehaviour
{
    private void Start()
    {
        var all = FindObjectsByType<HexCellView>(FindObjectsSortMode.None);

        var map = new Dictionary<Vector2Int, List<HexCellView>>();
        foreach (var c in all)
        {
            var key = new Vector2Int(c.q, c.r);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<HexCellView>();
                map[key] = list;
            }
            list.Add(c);
        }

        int dupCount = 0;
        foreach (var kv in map)
        {
            if (kv.Value.Count <= 1) continue;

            dupCount++;
            Debug.LogWarning($"[GridAudit] DUPLICATE q={kv.Key.x} r={kv.Key.y} count={kv.Value.Count}");
            for (int i = 0; i < kv.Value.Count; i++)
            {
                var c = kv.Value[i];
                Debug.LogWarning($"    - {c.name} pos={c.transform.position}");
            }
        }

        Debug.Log($"[GridAudit] cells={all.Length}, uniqueKeys={map.Count}, duplicates={dupCount}");
    }
}
