using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public sealed class GridPlacementProbe : MonoBehaviour
{
    [SerializeField] private float delaySeconds = 0.5f;

    private TilePlacement tp;
    private IDictionary cellsDict; // private Dictionary<Vector2Int, HexCellView>

    private void Start()
    {
        StartCoroutine(CoInit());
    }

    private IEnumerator CoInit()
    {
        yield return new WaitForSeconds(delaySeconds);

        tp = FindFirstObjectByType<TilePlacement>();
        var allCells = FindObjectsByType<HexCellView>(FindObjectsSortMode.None);

        Debug.Log($"[Probe] HexCellView in scene: {allCells.Length}");

        if (tp == null)
        {
            Debug.LogError("[Probe] TilePlacement not found in scene.");
            yield break;
        }

        // принудительно пересоберём кеш (не меняет логику, просто обновляет dictionary)
        tp.RebuildCellCache();

        // достаём private поле cells через reflection
        var f = typeof(TilePlacement).GetField("cells", BindingFlags.Instance | BindingFlags.NonPublic);
        cellsDict = f != null ? (IDictionary)f.GetValue(tp) : null;

        Debug.Log($"[Probe] TilePlacement cache cells: {(cellsDict != null ? cellsDict.Count : -1)}");

        // найдём клетку под замком (рейкаст вниз)
        var castle = GameObject.Find("CastlePrefab") ?? GameObject.Find("Castle") ?? GameObject.FindWithTag("Castle");
        if (castle != null)
        {
            var origin = castle.transform.position + Vector3.up * 5f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 50f))
            {
                var cell = hit.collider.GetComponentInParent<HexCellView>();
                if (cell != null)
                    Debug.Log($"[Probe] Cell under castle: q={cell.q} r={cell.r} hit={hit.collider.name}");
                else
                    Debug.LogWarning($"[Probe] Raycast under castle hit {hit.collider.name} but HexCellView not found.");
            }
            else Debug.LogWarning("[Probe] Raycast under castle did not hit anything.");
        }
        else Debug.LogWarning("[Probe] Castle object not found by name/tag (ok, just skip).");
    }

    private void Update()
    {
        if (tp == null || cellsDict == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            var cam = Camera.main;
            if (cam == null) return;

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 200f)) return;

            var cell = hit.collider.GetComponentInParent<HexCellView>();
            if (cell == null)
            {
                Debug.Log($"[Probe] Click hit {hit.collider.name}, but no HexCellView in parents.");
                return;
            }

            var key = new Vector2Int(cell.q, cell.r);
            bool inCache = cellsDict.Contains(key);

            Debug.Log($"[Probe] Click cell q={cell.q} r={cell.r} inTilePlacementCache={inCache} hit={hit.collider.name} obj={cell.gameObject.name}");
        }
    }
}
