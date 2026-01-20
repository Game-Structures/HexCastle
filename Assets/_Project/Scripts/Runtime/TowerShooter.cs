using UnityEngine;

public sealed class TowerShooter : MonoBehaviour
{
    [Header("Type")]
    [SerializeField] private TowerType type = TowerType.Archer;

    [Header("Targets")]
    [SerializeField] private AttackTargetKind attackTargets = AttackTargetKind.All;

    [Header("Attack")]
    [SerializeField] private int damage = 20;
    [SerializeField] private float fireInterval = 1.0f;
    [SerializeField] private int rangeTiles = 2;

    [Header("Range padding")]
    [SerializeField] private float rangeExtraTiles = 0.05f;
    [SerializeField] private float aoeRadiusTiles = 1.0f;

    [Header("World scale")]
    [Tooltip("Distance between centers of neighbor cells (world units).")]
    [SerializeField] private float cellSpacing = 1f;

    [Header("Projectile (CastleProjectile)")]
    [SerializeField] private CastleProjectile projectilePrefab;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileHitRadius = 0.08f;
    [SerializeField] private float projectileYOffset = 0.35f;
    [SerializeField] private float projectileScale = 0.15f;

    [Header("Magic beam")]
    [SerializeField] private float beamDuration = 0.06f;
    [SerializeField] private float beamWidth = 0.03f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    [SerializeField] private Color magicBeamColor = new Color(1f, 0.2f, 0.7f, 1f);
    [SerializeField] private Color flameBeamColor = new Color(1f, 0.5f, 0.0f, 1f);

    private float timer;
    private WaveController waves;
    private Transform muzzle;

    private LineRenderer beam;

    // Cannon aoe pending
    private Vector3 aoeCenter;
    private float aoeRadiusWorld;
    private int aoeDamage;

    private static float _globalRangeMul = 1f;

    public static void SetGlobalRangeMultiplier(float mul)
    {
        _globalRangeMul = Mathf.Max(0.1f, mul);
    }

    private float GetRangeWorld()
    {
        return (rangeTiles + rangeExtraTiles) * cellSpacing * _globalRangeMul;
    }

    public void Init(TowerType t, float spacing)
    {
        type = t;
        cellSpacing = Mathf.Max(0.01f, spacing);

        switch (type)
        {
            case TowerType.Archer:
                rangeTiles = 3;
                rangeExtraTiles = 0.1f;
                damage = 17;
                fireInterval = 1.5f;
                projectileSpeed = 10f;
                projectileScale = 0.12f;
                break;

            case TowerType.Cannon:
                rangeTiles = 4;
                rangeExtraTiles = 0.1f;
                aoeRadiusTiles = 1.25f;
                damage = 22;
                fireInterval = 5f;
                projectileSpeed = 8f;
                projectileScale = 0.16f;
                break;

            case TowerType.Magic:
                rangeTiles = 2;
                rangeExtraTiles = 0.1f;
                damage = 2;
                fireInterval = 0.1f;
                break;

            case TowerType.Flame:
                rangeTiles = 1;
                rangeExtraTiles = 0.1f;
                damage = 9;
                fireInterval = 0.3f;
                projectileSpeed = 14f;
                projectileScale = 0.09f;
                break;
        }
    }

    private void Awake()
    {
        waves = FindFirstObjectByType<WaveController>();
        timer = Random.Range(0f, 0.2f);

        var t = transform.Find("Muzzle");
        muzzle = (t != null) ? t : transform;

        beam = GetComponent<LineRenderer>();
        if (beam == null)
            beam = gameObject.AddComponent<LineRenderer>();

        if (beam != null)
        {
            beam.enabled = false;
            beam.positionCount = 2;
            beam.useWorldSpace = true;
            beam.numCapVertices = 6;
            beam.numCornerVertices = 6;

            beam.material = new Material(Shader.Find("Sprites/Default"));

            beam.startWidth = beamWidth;
            beam.endWidth = beamWidth;
            beam.startColor = magicBeamColor;
            beam.endColor = magicBeamColor;
        }
    }

    private void Update()
    {
        if (GameState.IsGameOver) return;
        if (waves != null && waves.CurrentPhase != WaveController.Phase.Combat) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        var target = FindNearestEnemyInRange();
        if (target == null) return;

        timer = fireInterval;

        if (type == TowerType.Magic)
        {
            DrawBeamTo(target.transform.position, isFlame: false);
            target.Damage(damage);

            if (debugLogs) Debug.Log($"[TowerShooter] MAGIC beam -> {target.name} dmg={damage}");
            return;
        }

        if (type == TowerType.Flame)
        {
            DrawBeamTo(target.transform.position, isFlame: true);
            ApplyAOE(transform.position, GetRangeWorld(), damage);

            if (debugLogs) Debug.Log($"[TowerShooter] FLAME AOE dmg={damage}");
            return;
        }

        SpawnProjectile(target);

        if (debugLogs) Debug.Log($"[TowerShooter] Fire {type} -> {target.name} dmg={damage}");
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

    private EnemyHealth FindNearestEnemyInRange()
    {
        float rangeWorld = GetRangeWorld();
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

    private void SpawnProjectile(EnemyHealth target)
    {
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        float y = origin.y + projectileYOffset;

        CastleProjectile proj = null;

        if (projectilePrefab != null)
        {
            proj = Instantiate(projectilePrefab);
            proj.transform.position = new Vector3(origin.x, y, origin.z);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"TowerProjectile_{type}";

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.transform.position = new Vector3(origin.x, y, origin.z);
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

        if (type == TowerType.Cannon)
        {
            float aoeW = Mathf.Max(0f, aoeRadiusTiles) * cellSpacing;
            if (aoeW > 0.01f)
            {
                aoeCenter = target.transform.position;
                aoeRadiusWorld = aoeW;
                aoeDamage = damage;

                float dist = Vector3.Distance(proj.transform.position, target.transform.position);
                float tHit = dist / Mathf.Max(0.1f, projectileSpeed);

                CancelInvoke(nameof(DoArtilleryAOE));
                Invoke(nameof(DoArtilleryAOE), tHit);
            }
        }
    }

    private void DoArtilleryAOE()
    {
        if (aoeRadiusWorld <= 0.01f) return;

        float r2 = aoeRadiusWorld * aoeRadiusWorld;
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

            float d2 = (e.transform.position - aoeCenter).sqrMagnitude;
            if (d2 <= r2)
                e.Damage(aoeDamage);
        }

        aoeRadiusWorld = 0f;
    }

    private void DrawBeamTo(Vector3 targetPos, bool isFlame)
    {
        if (beam == null) return;

        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        origin.y += projectileYOffset;

        targetPos.y += projectileYOffset;

        float w = isFlame ? (beamWidth * 4.0f) : beamWidth;
        beam.startWidth = w;
        beam.endWidth = w;

        var c = isFlame ? flameBeamColor : magicBeamColor;

        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(c, 0f),
                new GradientColorKey(c, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            }
        );
        beam.colorGradient = g;

        beam.enabled = true;
        beam.SetPosition(0, origin);
        beam.SetPosition(1, targetPos);

        float dur = isFlame ? Mathf.Max(0.08f, beamDuration * 2.0f) : beamDuration;
        CancelInvoke(nameof(HideBeam));
        Invoke(nameof(HideBeam), dur);
    }

    private void HideBeam()
    {
        if (beam != null) beam.enabled = false;
    }

    private void ApplyAOE(Vector3 center, float radiusWorld, int dmg)
    {
        float r2 = radiusWorld * radiusWorld;
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

            float d2 = (e.transform.position - center).sqrMagnitude;
            if (d2 <= r2)
                e.Damage(dmg);
        }
    }
}
