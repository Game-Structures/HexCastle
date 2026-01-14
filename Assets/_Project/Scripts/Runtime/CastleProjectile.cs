using UnityEngine;

public sealed class CastleProjectile : MonoBehaviour
{
    private EnemyHealth target;
    private int damage;
    private float speed;
    private float hitRadius;
    private float fixedY;

    private bool debugLogs;

    public void Init(EnemyHealth t, int dmg, float spd, float hitR, float y, bool logs)
    {
        target = t;
        damage = dmg;
        speed = spd;
        hitRadius = Mathf.Max(0.01f, hitR);
        fixedY = y;
        debugLogs = logs;
    }

    private void Update()
    {
        if (GameState.IsGameOver)
        {
            Destroy(gameObject);
            return;
        }

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 p = transform.position;
        Vector3 tp = target.transform.position;
        tp.y = fixedY;

        transform.position = Vector3.MoveTowards(p, tp, speed * Time.deltaTime);

        float dSqr = (transform.position - tp).sqrMagnitude;
        if (dSqr <= hitRadius * hitRadius)
        {
            target.Damage(damage);

            if (debugLogs)
                Debug.Log($"[CastleShooter] Hit {target.name} for {damage}");

            Destroy(gameObject);
        }
    }
}
