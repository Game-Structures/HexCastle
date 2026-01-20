using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyHealth : MonoBehaviour
{
    public static readonly List<EnemyHealth> Alive = new List<EnemyHealth>(256);

    [SerializeField] private int maxHp = 100;
    private int hp;

    [Header("VFX")]
    [SerializeField] private bool showDamagePopup = true;
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.8f, 0f);

    public int CurrentHp => hp;
    public int MaxHp => maxHp;

    private void OnEnable()
    {
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
    }

    public void SetStats(EnemyStats stats)
    {
        if (stats == null) return;
        maxHp = Mathf.Max(1, stats.maxHp);
        hp = maxHp;
    }

    public void Damage(int amount)
    {
        if (amount <= 0) return;

        // NEW: в лесу враг неуязвим
        var stealth = GetComponent<EnemyForestStealth>();
        if (stealth != null && stealth.IsHidden)
            return;

        if (showDamagePopup)
            DamagePopupWorld.Spawn(transform.position + popupOffset, amount);

        hp -= amount;
        Debug.Log($"Enemy HP: {hp}");

        if (hp <= 0)
            Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}
