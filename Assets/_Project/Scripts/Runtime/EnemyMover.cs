using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class EnemyMover : MonoBehaviour
{
    [SerializeField] private EnemyStats stats;

    private Transform target;
    private Rigidbody rb;

    private WallHealth wallTarget;
    private CastleHealth castleTarget;

    private float attackTimer;

    public void SetTarget(Transform t) => target = t;

    public void SetStats(EnemyStats s)
    {
        stats = s;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (GameState.IsGameOver)
{
    rb.velocity = Vector3.zero;
    return;
}
        if (target == null) return;

        float speed = stats != null ? stats.speed : 0.5f;
        float attackInterval = stats != null ? stats.attackInterval : 5f;
        int attackDamage = stats != null ? stats.attackDamage : 50;

        bool isAttacking = wallTarget != null || castleTarget != null;

        if (isAttacking)
        {
            rb.velocity = Vector3.zero;

            attackTimer -= Time.fixedDeltaTime;
            if (attackTimer <= 0f)
            {
                attackTimer = attackInterval;

                if (wallTarget != null) wallTarget.Damage(attackDamage);
                else if (castleTarget != null) castleTarget.Damage(attackDamage);
            }

            return;
        }

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.01f)
        {
            rb.velocity = Vector3.zero;
            return;
        }

        rb.velocity = dir.normalized * speed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name.Contains("WallPrefab"))
        {
            var w = collision.gameObject.GetComponent<WallHealth>();
            if (w == null) w = collision.gameObject.AddComponent<WallHealth>();

            wallTarget = w;
            castleTarget = null;
            attackTimer = 0f;
            return;
        }

        if (collision.gameObject.name == "Castle")
        {
            var c = collision.gameObject.GetComponent<CastleHealth>();
            if (c == null) c = collision.gameObject.AddComponent<CastleHealth>();

            castleTarget = c;
            wallTarget = null;
            attackTimer = 0f;
            return;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.name.Contains("WallPrefab"))
        {
            if (wallTarget != null && collision.gameObject == wallTarget.gameObject)
                wallTarget = null;
        }

        if (collision.gameObject.name == "Castle")
        {
            if (castleTarget != null && collision.gameObject == castleTarget.gameObject)
                castleTarget = null;
        }
    }
}
