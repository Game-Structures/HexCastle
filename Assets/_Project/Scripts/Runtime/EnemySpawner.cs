using UnityEngine;

public sealed class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform castle;
    [SerializeField] private Transform[] spawnPoints;

    private int idx = 0;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SpawnOne();
        }
    }

    public void Init(Transform castleTransform, Transform[] spawnTransforms)
    {
        castle = castleTransform;
        spawnPoints = spawnTransforms;
    }

    private void SpawnOne()
    {
        if (enemyPrefab == null || castle == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("EnemySpawner: assign enemyPrefab, castle, spawnPoints.");
            return;
        }

        Transform sp = spawnPoints[idx % spawnPoints.Length];
        idx++;

        GameObject go = Instantiate(enemyPrefab, sp.position + new Vector3(0f, 0.3f, 0f), Quaternion.identity);

        var hp = go.GetComponent<EnemyHealth>();
        if (hp == null) hp = go.AddComponent<EnemyHealth>();
        hp.SetTargetKind(EnemyTargetKind.Ground);

        var mover = go.GetComponent<EnemyMover>();
        if (mover == null) mover = go.AddComponent<EnemyMover>();
        mover.SetTarget(castle);
        mover.SetTargetKind(EnemyTargetKind.Ground);
    }
}
