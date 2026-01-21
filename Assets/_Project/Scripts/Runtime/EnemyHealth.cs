using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyHealth : MonoBehaviour
{
    public static readonly List<EnemyHealth> Alive = new List<EnemyHealth>(256);

    [Header("Kind")]
    [SerializeField] private EnemyTargetKind targetKind = EnemyTargetKind.Ground;

    [Header("HP")]
    [SerializeField] private int maxHp = 100;
    private int hp;

    [Header("XP reward (to tower on kill)")]
    [SerializeField] private int xpReward = 10;

    [Header("VFX")]
    [SerializeField] private bool showDamagePopup = true;
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.8f, 0f);

    private TowerProgress lastHitTower;
    private bool isDead;

    public int CurrentHp => hp;
    public int MaxHp => maxHp;
    public EnemyTargetKind TargetKind => targetKind;

    private void OnEnable()
    {
        isDead = false;

        if (!Alive.Contains(this))
            Alive.Add(this);
    }

    private void OnDisable()
    {
        Alive.Remove(this);
    }

    private void Awake()
    {
        hp = maxHp;
        isDead = false;
    }

    public void SetStats(EnemyStats stats)
    {
        if (stats == null) return;

        maxHp = Mathf.Max(1, stats.maxHp);
        hp = maxHp;

        if (stats.xpReward >= 0)
            xpReward = stats.xpReward;
    }

    public void SetTargetKind(EnemyTargetKind kind)
    {
        targetKind = kind;
    }

    // Старые вызовы остаются рабочими
    public void Damage(int amount)
    {
        Damage(amount, null);
    }

    // Новый вызов с источником урона (вышкой)
    public void Damage(int amount, TowerProgress sourceTower)
    {
        if (isDead) return;
        if (amount <= 0) return;

        var stealth = GetComponent<EnemyForestStealth>();
        if (stealth != null && stealth.IsHidden)
            return;

        // Строгий LastHit: если добивает НЕ вышка (sourceTower == null), XP не получит никто
lastHitTower = sourceTower; // null тоже допустим – значит последний удар не от вышки


        if (showDamagePopup)
            DamagePopupWorld.Spawn(transform.position + popupOffset, amount);

        hp -= amount;

        if (hp <= 0)
            Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (lastHitTower != null && xpReward > 0)
            lastHitTower.AddXP(xpReward);

        Destroy(gameObject);
    }
}
