// Assets/_Project/Scripts/Runtime/WaveController.cs
using System.Collections;
using UnityEngine;

public sealed class WaveController : MonoBehaviour
{
    public enum Phase { Build, Combat }

    [SerializeField] private EnemySpawnerSimple spawner;

    [Header("Economy")]
    [SerializeField] private int startGold = 0;
    [SerializeField] private int[] waveRewards = new int[] { 20, 40, 60, 80 };

    [Header("Wave (fallback if no WavePlan for this round)")]
    [SerializeField] private int waveNumber = 1;
    [SerializeField] private int baseEnemies = 5;
    [SerializeField] private int addPerWave = 2;
    [SerializeField] private float spawnInterval = 1.0f;

    [Header("WavePlan (manual subwaves)")]
    [SerializeField] private WavePlan wavePlan;

    public Phase CurrentPhase { get; private set; } = Phase.Build;
    public int WaveNumber => waveNumber;

    private int enemiesToSpawn;
    private float timer;

    private WallHandManager hand;

    // planned spawning state
    private bool planActive;
    private bool planSpawningComplete;
    private Coroutine planRoutine;

    private void Awake()
    {
        if (spawner == null)
            spawner = FindFirstObjectByType<EnemySpawnerSimple>();

        hand = FindFirstObjectByType<WallHandManager>();
    }

    private void Start()
    {
        GoldBank.Reset(startGold);

        if (hand == null) hand = FindFirstObjectByType<WallHandManager>();
        if (hand != null) hand.NewRoundHand();
    }

    private void Update()
    {
        if (GameState.IsGameOver) return;

        if (CurrentPhase == Phase.Build)
            return;

        // Combat
        if (planActive)
        {
            if (planSpawningComplete && FindObjectsOfType<EnemyHealth>().Length == 0)
                EndCombatToBuild();
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f && enemiesToSpawn > 0)
        {
            timer = spawnInterval;
            spawner.SpawnOnePublic();
            enemiesToSpawn--;
        }

        if (enemiesToSpawn <= 0 && FindObjectsOfType<EnemyHealth>().Length == 0)
            EndCombatToBuild();
    }

    public void StartWaveButton()
    {
        if (GameState.IsGameOver) return;
        if (CurrentPhase != Phase.Build) return;

        StartCombat();
    }

    private void StartCombat()
    {
        if (spawner == null)
            spawner = FindFirstObjectByType<EnemySpawnerSimple>();

        // stop previous planned routine if any
        if (planRoutine != null)
        {
            StopCoroutine(planRoutine);
            planRoutine = null;
        }

        CurrentPhase = Phase.Combat;

        // If there is a WavePlan entry for this wave – use it
        if (wavePlan != null && wavePlan.TryGetRound(waveNumber, out var round) && round != null && round.subWaves != null && round.subWaves.Length > 0)
        {
            planActive = true;
            planSpawningComplete = false;
            enemiesToSpawn = 0;
            timer = 0f;

            planRoutine = StartCoroutine(SpawnPlannedRound(round));
            Debug.Log($"Phase: COMBAT (wave {waveNumber}) planned subwaves={round.subWaves.Length}");
            return;
        }

        // fallback old behavior
        planActive = false;
        planSpawningComplete = false;

        enemiesToSpawn = baseEnemies + (waveNumber - 1) * addPerWave;
        timer = 0f;

        Debug.Log($"Phase: COMBAT (wave {waveNumber}) enemies={enemiesToSpawn}");
    }

    private IEnumerator SpawnPlannedRound(WavePlan.RoundDef round)
    {
        for (int i = 0; i < round.subWaves.Length; i++)
        {
            var sw = round.subWaves[i];
            Debug.Log($"[WavePlan] SubWave {i+1}/{round.subWaves.Length} delay={sw.delayFromPrevious} interval={sw.spawnInterval}");

            if (sw == null) continue;

            if (sw.delayFromPrevious > 0f)
                yield return new WaitForSeconds(sw.delayFromPrevious);

            if (sw.packs == null || sw.packs.Length == 0) continue;

            float interval = Mathf.Max(0.01f, sw.spawnInterval);

            for (int p = 0; p < sw.packs.Length; p++)
            {
                var pack = sw.packs[p];
                if (pack.type != null)
    Debug.Log($"[WavePlan] Pack type={pack.type.id} count={pack.count}");

                if (pack.type == null) continue;
                int count = Mathf.Max(1, pack.count);

                for (int c = 0; c < count; c++)
                {
                    if (GameState.IsGameOver) yield break;

                    spawner.SpawnSpecific(pack.type);
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        planSpawningComplete = true;
    }

    private void EndCombatToBuild()
    {
        // reward for current wave
        int reward = GetRewardForWave(waveNumber);
        GoldBank.Add(reward);
        Debug.Log($"Wave reward: +{reward} gold");

        // next wave
        waveNumber++;
        CurrentPhase = Phase.Build;

        // reset plan flags
        planActive = false;
        planSpawningComplete = false;
        planRoutine = null;

        // new hand at BUILD start
        if (hand == null) hand = FindFirstObjectByType<WallHandManager>();
        if (hand != null) hand.NewRoundHand();

        Debug.Log($"Phase: BUILD (wave {waveNumber})");
    }

    private int GetRewardForWave(int wave)
    {
        if (waveRewards == null || waveRewards.Length == 0) return 0;
        int idx = Mathf.Clamp(wave - 1, 0, waveRewards.Length - 1);
        return waveRewards[idx];
    }
}
