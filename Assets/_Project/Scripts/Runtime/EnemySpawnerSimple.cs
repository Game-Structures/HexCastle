using System.Collections.Generic;
using UnityEngine;

public sealed class EnemySpawnerSimple : MonoBehaviour
{
    private enum HexSide { NE = 0, E = 1, SE = 2, SW = 3, W = 4, NW = 5 }

    [Header("Legacy (used if Enemy Types is empty)")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private EnemyStats enemyStats;

    [Header("Enemy Types (recommended)")]
    [SerializeField] private EnemyType[] enemyTypes;

    [Header("Empowered wave")]
    [SerializeField] private int empoweredWaveEvery = 5;
    [SerializeField] private bool empoweredWaveSingleSide = true;

    [Header("Debug")]
    [SerializeField] private bool logEmpoweredWave = true;

    private Transform castle;
    private HexGridSpawner grid;
    private TilePlacement placement;
    private WaveController waves;

    // empowered-wave cache
    private int cachedForcedWave = -1;
    private HexSide cachedForcedSide;
    private readonly List<HexCellView> cachedSideCells = new List<HexCellView>(64);

    private void Update()
    {
        if (GameState.IsGameOver) return;

        if (grid == null) grid = FindFirstObjectByType<HexGridSpawner>();
        if (placement == null) placement = FindFirstObjectByType<TilePlacement>();
        if (waves == null) waves = FindFirstObjectByType<WaveController>();

        if (grid != null && castle == null)
            castle = grid.CastleTransform;
    }

    // ВЫЗЫВАЕМ В BUILD: заранее фиксируем сторону для волны (чтобы в COMBAT она уже не менялась)
    public bool TryPrepareEmpoweredWave(int wave, out string sideLabel)
    {
        sideLabel = null;

        if (!IsEmpoweredWave(wave))
            return false;

        EnsureForcedSideForWave(wave);
        sideLabel = cachedForcedSide.ToString();
        return true;
    }

    public void SpawnOnePublic()
    {
        SpawnOneRandomForCurrentWave();
    }

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

        SpawnInternal(prefab, stats, type.id, wave, type.targetKind);
    }

    private void SpawnOneRandomForCurrentWave()
    {
        int wave = waves != null ? waves.WaveNumber : 1;

        EnemyType picked = PickEnemyTypeForWave(wave);
        GameObject prefab = (picked != null && picked.prefab != null) ? picked.prefab : enemyPrefab;
        EnemyStats stats = (picked != null && picked.stats != null) ? picked.stats : enemyStats;

        string typeId = picked != null ? picked.id : "legacy";
        EnemyTargetKind kind = picked != null ? picked.targetKind : EnemyTargetKind.Ground;

        SpawnInternal(prefab, stats, typeId, wave, kind);
    }

    private void SpawnInternal(GameObject prefab, EnemyStats stats, string typeId, int wave, EnemyTargetKind kind)
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

        List<HexCellView> spawnPool = grid.EdgeCells;

        if (IsEmpoweredWave(wave))
        {
            // если сторона была подготовлена на BUILD – тут она не перероллится
            EnsureForcedSideForWave(wave);

            if (cachedSideCells.Count > 0)
                spawnPool = cachedSideCells;
        }
        else
        {
            // сбрасываем кеш при выходе из усиленной волны
            if (cachedForcedWave != -1 && cachedForcedWave != wave)
            {
                cachedForcedWave = -1;
                cachedSideCells.Clear();
            }
        }

        for (int attempt = 0; attempt < 30; attempt++)
        {
            var cell = spawnPool[Random.Range(0, spawnPool.Count)];
            int q = cell.q;
            int r = cell.r;

            if (placement != null && placement.IsOccupied(q, r))
                continue;

            Vector3 pos = cell.transform.position + new Vector3(0f, 0.3f, 0f);
            GameObject go = Instantiate(prefab, pos, Quaternion.identity);
            go.name = $"Enemy_{typeId}";

            var hp = go.GetComponent<EnemyHealth>();
            if (hp == null) hp = go.AddComponent<EnemyHealth>();
            hp.SetTargetKind(kind);
            if (stats != null) hp.SetStats(stats);

            var mover = go.GetComponent<EnemyMover>();
            if (mover == null) mover = go.AddComponent<EnemyMover>();
            mover.SetTarget(castle);
            mover.SetTargetKind(kind);
            if (stats != null) mover.SetStats(stats);

            var wallDmg = go.GetComponent<EnemyWallDamage>();
            if (wallDmg == null) wallDmg = go.AddComponent<EnemyWallDamage>();

            wallDmg.enabled = (kind == EnemyTargetKind.Ground);
            if (stats != null) wallDmg.SetStats(stats);

            return;
        }

        Debug.LogWarning("EnemySpawnerSimple: no free edge cell found (maybe all blocked).");
    }

    private bool IsEmpoweredWave(int wave)
    {
        return empoweredWaveSingleSide && empoweredWaveEvery > 0 && wave % empoweredWaveEvery == 0;
    }

    private void EnsureForcedSideForWave(int wave)
    {
        if (grid == null) grid = FindFirstObjectByType<HexGridSpawner>();
        if (grid == null) return;

        if (cachedForcedWave == wave && cachedSideCells.Count > 0)
            return;

        cachedForcedWave = wave;
        cachedForcedSide = (HexSide)Random.Range(0, 6);

        cachedSideCells.Clear();
        int R = Mathf.Max(1, grid.Radius);

        for (int i = 0; i < grid.EdgeCells.Count; i++)
        {
            var c = grid.EdgeCells[i];
            if (c == null) continue;
            if (IsOnSide(c.q, c.r, R, cachedForcedSide))
                cachedSideCells.Add(c);
        }

        if (logEmpoweredWave)
            Debug.Log($"[EmpoweredWave] prepared wave={wave} side={cachedForcedSide} edgeCells={cachedSideCells.Count}");
    }

    private static bool IsOnSide(int q, int r, int R, HexSide side)
    {
        switch (side)
        {
            case HexSide.E:  return q == R;
            case HexSide.W:  return q == -R;
            case HexSide.SE: return r == R;
            case HexSide.NW: return r == -R;
            case HexSide.NE: return (q + r) == R;
            case HexSide.SW: return (q + r) == -R;
            default:         return false;
        }
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
