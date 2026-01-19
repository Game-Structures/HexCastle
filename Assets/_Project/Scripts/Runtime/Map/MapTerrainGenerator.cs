using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HexCastle.Map
{
    public sealed class MapTerrainGenerator : MonoBehaviour
    {
        [Header("Timing")]
        public float delaySeconds = 0.25f;

        [Header("Castle / exclusion")]
        [Tooltip("Имя объекта замка в Hierarchy.")]
        public string castleObjectName = "Castle";
        [Tooltip("Не размещать террейн ближе этого расстояния (в клетках) к замку.")]
        public int minDistanceFromCastle = 3;

        [Header("Fill targets (percent of all cells)")]
        [Range(0, 60)] public int waterPercent = 8;
        [Range(0, 60)] public int mountainPercent = 6;
        [Range(0, 60)] public int forestPercent = 12;

        [Header("Cluster sizes")]
        public Vector2Int waterClusterSize = new Vector2Int(1, 5);
        public Vector2Int mountainClusterSize = new Vector2Int(1, 3);
        public Vector2Int forestClusterSize = new Vector2Int(1, 5);

        [Header("Seed")]
        public bool useRandomSeed = true;
        public int seed = 12345;

        private readonly Dictionary<Vector2Int, HexCellView> byAxial = new Dictionary<Vector2Int, HexCellView>();

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(delaySeconds);

            var cells = FindObjectsByType<HexCellView>(FindObjectsSortMode.None);
            if (cells == null || cells.Length == 0)
            {
                Debug.LogWarning("[TerrainGen] No HexCellView found.");
                yield break;
            }

            byAxial.Clear();
            for (int i = 0; i < cells.Length; i++)
            {
                var key = new Vector2Int(cells[i].q, cells[i].r);
                if (!byAxial.ContainsKey(key))
                    byAxial.Add(key, cells[i]);
            }

            // Найдём "клетку замка" по ближайшей клетке к объекту замка (или к (0,0,0))
            Vector3 castlePos = Vector3.zero;
            var castleGo = GameObject.Find(castleObjectName);
            if (castleGo != null) castlePos = castleGo.transform.position;

            HexCellView castleCell = cells[0];
            float best = float.MaxValue;
            for (int i = 0; i < cells.Length; i++)
            {
                float d = (cells[i].transform.position - castlePos).sqrMagnitude;
                if (d < best) { best = d; castleCell = cells[i]; }
            }

            // Сбросим прошлые теги
            for (int i = 0; i < cells.Length; i++)
            {
                var tag = cells[i].GetComponent<MapTerrainTag>();
                if (tag == null) tag = cells[i].gameObject.AddComponent<MapTerrainTag>();
                tag.type = MapTerrainType.Normal;
            }

            int total = cells.Length;
            int waterTarget = Mathf.RoundToInt(total * (waterPercent / 100f));
            int mountTarget = Mathf.RoundToInt(total * (mountainPercent / 100f));
            int forestTarget = Mathf.RoundToInt(total * (forestPercent / 100f));

            var rng = useRandomSeed ? new System.Random(System.Environment.TickCount) : new System.Random(seed);

            int waterPlaced = PlaceTerrain(rng, castleCell, MapTerrainType.Water, waterTarget, waterClusterSize);
            int mountPlaced = PlaceTerrain(rng, castleCell, MapTerrainType.Mountain, mountTarget, mountainClusterSize);
            int forestPlaced = PlaceTerrain(rng, castleCell, MapTerrainType.Forest, forestTarget, forestClusterSize);

            Debug.Log($"[TerrainGen] total={total} water={waterPlaced}/{waterTarget} mountain={mountPlaced}/{mountTarget} forest={forestPlaced}/{forestTarget}");
        }

        private int PlaceTerrain(System.Random rng, HexCellView castleCell, MapTerrainType type, int targetCount, Vector2Int clusterSizeRange)
        {
            if (targetCount <= 0) return 0;

            int placed = 0;
            int guard = 100000;

            var allKeys = new List<Vector2Int>(byAxial.Keys);

            while (placed < targetCount && guard-- > 0)
            {
                // выбираем случайную стартовую клетку под кластер
                var startKey = allKeys[rng.Next(allKeys.Count)];
                if (!IsAllowedStart(startKey, castleCell, type)) continue;

                int wantSize = rng.Next(clusterSizeRange.x, clusterSizeRange.y + 1);
                placed += GrowCluster(rng, castleCell, startKey, type, wantSize, targetCount - placed);
            }

            return placed;
        }

        private bool IsAllowedStart(Vector2Int key, HexCellView castleCell, MapTerrainType type)
        {
            var cell = byAxial[key];
            var tag = cell.GetComponent<MapTerrainTag>();
            if (tag == null || tag.type != MapTerrainType.Normal) return false;

            int distToCastle = AxialDistance(castleCell.q, castleCell.r, cell.q, cell.r);
            if (distToCastle < minDistanceFromCastle) return false;

            return true;
        }

        private int GrowCluster(System.Random rng, HexCellView castleCell, Vector2Int startKey, MapTerrainType type, int wantSize, int maxCanPlace)
        {
            int placed = 0;

            var frontier = new List<Vector2Int> { startKey };
            var visited = new HashSet<Vector2Int>();

            while (frontier.Count > 0 && placed < wantSize && placed < maxCanPlace)
            {
                int idx = rng.Next(frontier.Count);
                var key = frontier[idx];
                frontier.RemoveAt(idx);

                if (visited.Contains(key)) continue;
                visited.Add(key);

                if (!byAxial.TryGetValue(key, out var cell)) continue;

                var tag = cell.GetComponent<MapTerrainTag>();
                if (tag == null || tag.type != MapTerrainType.Normal) continue;

                int distToCastle = AxialDistance(castleCell.q, castleCell.r, cell.q, cell.r);
                if (distToCastle < minDistanceFromCastle) continue;

                tag.type = type;
                placed++;

                // расширяем кластер в соседей
                var n = GetNeighbors(key);
                for (int i = 0; i < n.Count; i++)
                {
                    if (byAxial.ContainsKey(n[i]) && !visited.Contains(n[i]))
                        frontier.Add(n[i]);
                }
            }

            return placed;
        }

        private static List<Vector2Int> GetNeighbors(Vector2Int a)
        {
            // axial neighbors (q,r)
            return new List<Vector2Int>
            {
                new Vector2Int(a.x + 1, a.y + 0),
                new Vector2Int(a.x + 1, a.y - 1),
                new Vector2Int(a.x + 0, a.y - 1),
                new Vector2Int(a.x - 1, a.y + 0),
                new Vector2Int(a.x - 1, a.y + 1),
                new Vector2Int(a.x + 0, a.y + 1),
            };
        }

        private static int AxialDistance(int aq, int ar, int bq, int br)
        {
            int ax = aq, az = ar, ay = -ax - az;
            int bx = bq, bz = br, by = -bx - bz;
            return (Mathf.Abs(ax - bx) + Mathf.Abs(ay - by) + Mathf.Abs(az - bz)) / 2;
        }
    }
}
