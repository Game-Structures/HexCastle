using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class EnemyWallDamage : MonoBehaviour
{
    [Header("Stats (can be set by spawner)")]
    [SerializeField] private EnemyStats stats;

    [Header("Stop while attacking")]
    [SerializeField] private bool stopMovementWhileAttacking = true;
    [SerializeField] private float releaseDelay = 0.15f;

    private NavMeshAgent agent;
    private Rigidbody rb;

    private bool holding;
    private float releaseAt;

    private WallTileLink currentLink;
    private float attackTimer;

    // НОВОЕ: внешний флаг – "я атакую стену/строение"
    public bool IsAttacking => holding && currentLink != null;

    public void SetStats(EnemyStats s) => stats = s;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (!holding) return;

        // если давно нет контакта – отпускаем
        if (Time.time >= releaseAt)
        {
            Release();
            return;
        }

        if (currentLink == null) return;

        float interval = stats != null ? stats.attackInterval : 5f;
        float damage = stats != null ? stats.attackDamage : 50f;

        if (interval <= 0f || damage <= 0f) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        attackTimer = Mathf.Max(0.02f, interval);
        currentLink.ApplyDamage(damage);
    }

    private void Hold(WallTileLink link)
    {
        if (link == null) return;

        // “контакт живой” – продлеваем удержание
        releaseAt = Time.time + Mathf.Max(0.02f, releaseDelay);

        // “удар сразу” только при первом захвате
        bool startingHold = !holding;

        holding = true;
        currentLink = link;

        if (startingHold)
            attackTimer = 0f;

        if (!stopMovementWhileAttacking) return;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void Release()
    {
        holding = false;
        currentLink = null;

        if (agent != null)
            agent.isStopped = false;
    }

    private void OnCollisionStay(Collision collision)
    {
        var link = collision.collider.GetComponentInParent<WallTileLink>();
        if (link == null) return;
        Hold(link);
    }

    private void OnTriggerStay(Collider other)
    {
        var link = other.GetComponentInParent<WallTileLink>();
        if (link == null) return;
        Hold(link);
    }
}
