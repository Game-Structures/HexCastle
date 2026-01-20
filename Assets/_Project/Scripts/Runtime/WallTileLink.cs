using System.Collections.Generic;
using UnityEngine;

public sealed class WallTileLink : MonoBehaviour
{
    [Header("HP (base)")]
    [SerializeField] private float baseMaxHp = 500f;
    [SerializeField] private float hp = 500f;

    [Header("Debug")]
    [SerializeField] private bool logHp = true;
    [SerializeField] private float logEverySeconds = 0.25f;

    private TilePlacement placement;
    private int q;
    private int r;
    private bool initialized;

    private float nextLogTime;

    private static float _globalBonus = 0f;

    private float MaxHp => Mathf.Max(1f, baseMaxHp + _globalBonus);

    public bool IsDamaged => initialized && hp < MaxHp;
    public float Hp => hp;
    public float MaxHpPublic => MaxHp;

    public static void SetGlobalMaxHpBonus(float bonus)
    {
        bonus = Mathf.Max(0f, bonus);
        float delta = bonus - _globalBonus;
        if (Mathf.Abs(delta) < 0.0001f) return;

        _globalBonus = bonus;

        var links = FindObjectsOfType<WallTileLink>();
        for (int i = 0; i < links.Length; i++)
        {
            var l = links[i];
            if (l != null) l.ApplyBonusDelta(delta);
        }

        Debug.Log($"[WallHP] Global bonus = {_globalBonus:0}");
    }

    private void ApplyBonusDelta(float delta)
    {
        if (!initialized) return;

        // при увеличении бонуса — добавляем HP, чтобы было «+100 HP»
        if (delta > 0f) hp += delta;

        hp = Mathf.Clamp(hp, 0f, MaxHp);

        if (logHp)
            Debug.Log($"[WallHP] bonus applied q={q} r={r} hp={hp:0.0}/{MaxHp:0}");
    }

    public void Init(TilePlacement placement, int q, int r, float wallMaxHp)
    {
        this.placement = placement;
        this.q = q;
        this.r = r;

        baseMaxHp = Mathf.Max(1f, wallMaxHp);
        hp = MaxHp;

        initialized = true;
        nextLogTime = 0f;

        if (logHp)
            Debug.Log($"[WallHP] init q={q} r={r} hp={hp:0}/{MaxHp:0}");
    }

    public void ApplyDamage(float damage)
    {
        if (!initialized) return;
        if (damage <= 0f) return;

        hp -= damage;

        if (logHp && Application.isPlaying && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + Mathf.Max(0.05f, logEverySeconds);
            Debug.Log($"[WallHP] q={q} r={r} hp={Mathf.Max(0f, hp):0.0}/{MaxHp:0}");
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

    public void Heal(float amount)
    {
        if (!initialized) return;
        if (amount <= 0f) return;

        float before = hp;
        hp = Mathf.Min(MaxHp, hp + amount);

        if (logHp)
            Debug.Log($"[WallHP] healed q={q} r={r} +{amount:0} ({before:0}->{hp:0}) / {MaxHp:0}");
    }

    public static bool HealRandomDamagedWall(float amount)
{
    if (amount <= 0f) return false;

    var all = FindObjectsOfType<WallTileLink>();
    if (all == null || all.Length == 0) return false;

    List<WallTileLink> damaged = null;
    for (int i = 0; i < all.Length; i++)
    {
        var w = all[i];
        if (w == null) continue;
        if (w.IsDamaged)
        {
            damaged ??= new List<WallTileLink>();
            damaged.Add(w);
        }
    }

    if (damaged == null || damaged.Count == 0) return false;

    var pick = damaged[Random.Range(0, damaged.Count)];

    float before = pick.Hp;
    float max = pick.MaxHpPublic;

    pick.Heal(amount);

    Debug.Log($"[ForgeHeal] healed wall q={pick.q} r={pick.r} +{amount:0} ({before:0}->{pick.Hp:0}) / {max:0}");
    return true;
}

}
