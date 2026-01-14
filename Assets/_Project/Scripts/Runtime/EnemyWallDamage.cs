using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class EnemyWallDamage : MonoBehaviour
{
    [SerializeField] private float damagePerSecond = 80f;

    [Header("Stop while attacking")]
    [SerializeField] private bool stopMovementWhileAttacking = true;
    [SerializeField] private float releaseDelay = 0.15f;

    private NavMeshAgent agent;
    private Rigidbody rb;

    private bool holding;
    private float releaseAt;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!holding) return;

        // если давно нет контакта — отпускаем
        if (Time.time >= releaseAt)
            Release();
    }

    void Hold()
    {
        if (!stopMovementWhileAttacking) return;

        holding = true;
        releaseAt = Time.time + Mathf.Max(0.02f, releaseDelay);

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

    void Release()
    {
        holding = false;

        if (agent != null)
            agent.isStopped = false;
    }

    void Damage(WallTileLink link)
    {
        if (damagePerSecond <= 0f) return;
        link.ApplyDamage(damagePerSecond * Time.deltaTime);
    }

    private void OnCollisionStay(Collision collision)
    {
        var link = collision.collider.GetComponentInParent<WallTileLink>();
        if (link == null) return;

        Hold();
        Damage(link);
    }

    private void OnTriggerStay(Collider other)
    {
        var link = other.GetComponentInParent<WallTileLink>();
        if (link == null) return;

        Hold();
        Damage(link);
    }
}
