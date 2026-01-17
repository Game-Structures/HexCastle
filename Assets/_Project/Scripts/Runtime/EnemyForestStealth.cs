using System.Collections.Generic;
using UnityEngine;
using HexCastle.Map;

public sealed class EnemyForestStealth : MonoBehaviour
{
    public bool IsHidden { get; private set; }

    [Header("Hide rules")]
    public bool hideInForest = true;
    public bool hideInFog = true;

    [Tooltip("Выключать визуал врага, когда он скрыт.")]
    public bool hideRenderers = true;

    private Dictionary<Vector2Int, HexCellView> cellsByAxial;
    private float timer;

    private Renderer[] rends;
    private bool lastHidden;

    private void Awake()
    {
        BuildCache();
        rends = GetComponentsInChildren<Renderer>(true);
        lastHidden = IsHidden;
        ApplyVisual();
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

        bool hidden = false;

        if (cell != null)
        {
            // 1) Forest
            if (hideInForest)
            {
                var tag = cell.GetComponent<MapTerrainTag>();
                if (tag != null && tag.type == MapTerrainType.Forest)
                    hidden = true;
            }

            // 2) Fog (если клетка не раскрыта)
            if (!hidden && hideInFog)
            {
                var fog = cell.GetComponent<MapTileFogLink>();
                if (fog != null && !fog.Revealed)
                    hidden = true;
            }
        }

        IsHidden = hidden;

        if (IsHidden != lastHidden)
        {
            lastHidden = IsHidden;
            ApplyVisual();
        }
    }

    private void ApplyVisual()
    {
        if (!hideRenderers || rends == null) return;

        bool show = !IsHidden;
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null)
                rends[i].enabled = show;
        }
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
