using UnityEngine;

public sealed class CastleShooter : MonoBehaviour
{
    [Header("Range in cells")]
    [SerializeField] private int rangeCells = 5;
    [SerializeField] private float hexSize = 1f;

    [Header("Targets")]
    [SerializeField] private AttackTargetKind attackTargets = AttackTargetKind.All;

    [Header("Attack")]
    [SerializeField] private int damage = 50;
    [SerializeField] private float attackInterval = 2f;

    [Header("Projectile")]
    [SerializeField] private CastleProjectile projectilePrefab;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileHitRadius = 0.08f;
    [SerializeField] private float projectileYOffset = 0.35f;
    [SerializeField] private float projectileScale = 0.15f;
    [SerializeField] private bool debugLogs = true;

    private float timer;

    public void SetHexSize(float size) => hexSize = Mathf.Max(0.01f, size);

    private void Update()
    {
        if (GameState.IsGameOver) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        var target = FindNearestEnemyInRange();
        if (target == null) return;

        timer = attackInterval;

        SpawnProjectile(target);

        if (debugLogs)
            Debug.Log($"[CastleShooter] Fire at {target.name} dmg={damage}");
    }

    private bool MatchesAttackTargets(EnemyHealth e)
    {
        if (e == null) return false;

        switch (attackTargets)
        {
            case AttackTargetKind.All:
                return true;
            case AttackTargetKind.Ground:
                return e.TargetKind == EnemyTargetKind.Ground;
            case AttackTargetKind.Air:
                return e.TargetKind == EnemyTargetKind.Air;
            default:
                return true;
        }
    }

    private void SpawnProjectile(EnemyHealth target)
    {
        float y = transform.position.y + projectileYOffset;

        CastleProjectile proj = null;

        if (projectilePrefab != null)
        {
            proj = Instantiate(projectilePrefab);
            proj.transform.position = new Vector3(transform.position.x, y, transform.position.z);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CastleProjectile";

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.transform.position = new Vector3(transform.position.x, y, transform.position.z);
            go.transform.localScale = Vector3.one * Mathf.Max(0.01f, projectileScale);

            proj = go.AddComponent<CastleProjectile>();
        }

        proj.Init(
            target,
            damage,
            Mathf.Max(0.1f, projectileSpeed),
            Mathf.Max(0.01f, projectileHitRadius),
            y,
            debugLogs
        );
    }

    private EnemyHealth FindNearestEnemyInRange()
    {
        float rangeWorld = rangeCells * Mathf.Sqrt(3f) * hexSize;
        float bestDistSq = rangeWorld * rangeWorld;

        EnemyHealth best = null;
        Vector3 p = transform.position;

        var enemies = EnemyHealth.Alive;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var e = enemies[i];
            if (e == null)
            {
                enemies.RemoveAt(i);
                continue;
            }

            if (!MatchesAttackTargets(e))
                continue;

            var stealth = e.GetComponent<EnemyForestStealth>();
            if (stealth != null && stealth.IsHidden)
                continue;

            float d = (e.transform.position - p).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                best = e;
            }
        }

        return best;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float rangeWorld = rangeCells * Mathf.Sqrt(3f) * hexSize;
        Gizmos.DrawWireSphere(transform.position, rangeWorld);
    }
#endif
}
