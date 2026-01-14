using UnityEngine;

public sealed class WallTileLink : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private float maxHp = 500f;
    [SerializeField] private float hp = 500f;

    [Header("Debug")]
    [SerializeField] private bool logHp = true;
    [SerializeField] private float logEverySeconds = 0.25f;

    private TilePlacement placement;
    private int q;
    private int r;
    private bool initialized;

    private float nextLogTime;

    public void Init(TilePlacement placement, int q, int r, float wallMaxHp)
    {
        this.placement = placement;
        this.q = q;
        this.r = r;

        maxHp = Mathf.Max(1f, wallMaxHp);
        hp = maxHp;

        initialized = true;
        nextLogTime = 0f;

        if (logHp)
            Debug.Log($"[WallHP] init q={q} r={r} hp={hp:0}/{maxHp:0}");
    }

    public void ApplyDamage(float damage)
    {
        if (!initialized) return;
        if (damage <= 0f) return;

        hp -= damage;

        if (logHp && Application.isPlaying && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + Mathf.Max(0.05f, logEverySeconds);
            Debug.Log($"[WallHP] q={q} r={r} hp={Mathf.Max(0f, hp):0.0}/{maxHp:0}");
        }

        if (hp > 0f) return;

        initialized = false;

        if (logHp)
            Debug.Log($"[WallHP] destroyed q={q} r={r}");

        if (placement != null)
            placement.RemoveWallAt(q, r);
        else
            Destroy(gameObject);
    }
}
