using UnityEngine;

public sealed class EnemySpawnerSimple : MonoBehaviour
{
    [Header("Legacy (used if Enemy Types is empty)")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private EnemyStats enemyStats;

    [Header("Enemy Types (recommended)")]
    [SerializeField] private EnemyType[] enemyTypes;

    private Transform castle;
    private HexGridSpawner grid;
    private TilePlacement placement;
    private WaveController waves;

    private void Update()
    {
        if (GameState.IsGameOver) return;

        if (grid == null) grid = FindFirstObjectByType<HexGridSpawner>();
        if (placement == null) placement = FindFirstObjectByType<TilePlacement>();
        if (waves == null) waves = FindFirstObjectByType<WaveController>();

        if (grid != null && castle == null)
            castle = grid.CastleTransform;
    }

    // Legacy entry point (if где-то дергается извне)
    public void SpawnOnePublic()
    {
        SpawnOneRandomForCurrentWave();
    }

    // NEW: спавн конкретного типа (для WavePlan / подволн)
    public void SpawnSpecific(EnemyType type)
    {
        if (type == null)
        {
            Debug.LogWarning("[EnemySpawner] SpawnSpecific called with null type.");
            return;
        }

        int wave = waves != null ? waves.WaveNumber : 1;

        GameObject prefab = type.prefab != null ? type.prefab : enemyPrefab;
        EnemyStats stats = type.stats != null ? type.stats : enemyStats;

        SpawnInternal(prefab, stats, type.id, wave);
    }

    private void SpawnOneRandomForCurrentWave()
    {
        int wave = waves != null ? waves.WaveNumber : 1;

        EnemyType picked = PickEnemyTypeForWave(wave);
        GameObject prefab = (picked != null && picked.prefab != null) ? picked.prefab : enemyPrefab;
        EnemyStats stats = (picked != null && picked.stats != null) ? picked.stats : enemyStats;

        string typeId = picked != null ? picked.id : "legacy";
        SpawnInternal(prefab, stats, typeId, wave);
    }

    private void SpawnInternal(GameObject prefab, EnemyStats stats, string typeId, int wave)
    {
        if (grid == null || castle == null || grid.EdgeCells == null || grid.EdgeCells.Count == 0)
        {
            Debug.LogWarning("EnemySpawnerSimple: waiting for grid/castle/edgeCells...");
            return;
        }

        if (prefab == null)
        {
            Debug.LogError("EnemySpawnerSimple: no enemy prefab assigned.");
            return;
        }

        // до 30 попыток найти свободную крайнюю клетку
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var cell = grid.EdgeCells[Random.Range(0, grid.EdgeCells.Count)];
            int q = cell.q;
            int r = cell.r;

            if (placement != null && placement.IsOccupied(q, r))
                continue;

            Vector3 pos = cell.transform.position + new Vector3(0f, 0.3f, 0f);
            GameObject go = Instantiate(prefab, pos, Quaternion.identity);

            go.name = $"Enemy_{typeId}";

            var hp = go.GetComponent<EnemyHealth>();
            if (hp == null) hp = go.AddComponent<EnemyHealth>();
            if (stats != null) hp.SetStats(stats);

            var mover = go.GetComponent<EnemyMover>();
            if (mover == null) mover = go.AddComponent<EnemyMover>();
            mover.SetTarget(castle);
            if (stats != null) mover.SetStats(stats);

            Debug.Log($"[EnemySpawner] Spawned type={typeId} wave={wave} prefab={prefab.name} hp={(stats != null ? stats.maxHp : 100)} speed={(stats != null ? stats.speed : 2f)}");
            return;
        }

        Debug.LogWarning("EnemySpawnerSimple: no free edge cell found (maybe all blocked).");
    }

    private EnemyType PickEnemyTypeForWave(int wave)
    {
        if (enemyTypes == null || enemyTypes.Length == 0) return null;

        float total = 0f;
        for (int i = 0; i < enemyTypes.Length; i++)
        {
            var t = enemyTypes[i];
            if (t == null) continue;
            if (t.prefab == null) continue;
            if (t.weight <= 0f) continue;

            if (wave < t.minWave) continue;
            if (t.maxWave != 0 && wave > t.maxWave) continue;

            total += t.weight;
        }

        if (total <= 0f) return null;

        float roll = Random.value * total;

        EnemyType lastValid = null;
        for (int i = 0; i < enemyTypes.Length; i++)
        {
            var t = enemyTypes[i];
            if (t == null) continue;
            if (t.prefab == null) continue;
            if (t.weight <= 0f) continue;

            if (wave < t.minWave) continue;
            if (t.maxWave != 0 && wave > t.maxWave) continue;

            lastValid = t;
            roll -= t.weight;
            if (roll <= 0f) return t;
        }

        return lastValid;
    }
}
