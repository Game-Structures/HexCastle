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

    public void SetStats(EnemyStats s) => stats = s;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>(); // может отсутствовать – это нормально
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (!holding) return;

        // если давно нет контакта — отпускаем
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
        holding = true;
        releaseAt = Time.time + Mathf.Max(0.02f, releaseDelay);

        // если цель сменилась – бьем сразу
        if (currentLink != link)
        {
            currentLink = link;
            attackTimer = 0f;
        }

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
