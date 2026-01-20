using System;
using UnityEngine;

public sealed class WallHealth : MonoBehaviour
{
    [SerializeField] private int baseMaxHp = 500;

    // чтобы было видно в инспекторе во время Play
    [SerializeField] private int hp;

    public int Hp => hp;
    public int MaxHp => baseMaxHp + _globalBonus;

    public event Action OnDestroyed;

    private static int _globalBonus = 0;

    private void Awake()
    {
        if (hp <= 0)
            hp = MaxHp;
        else
            hp = Mathf.Clamp(hp, 0, MaxHp);
    }

    /// <summary>Глобальный бонус к MaxHP для всех стен (например от Каменоломни).</summary>
    public static void SetGlobalMaxHpBonus(int bonus)
    {
        bonus = Mathf.Max(0, bonus);
        int delta = bonus - _globalBonus;
        if (delta == 0) return;

        _globalBonus = bonus;

        // обновить все существующие стены
        var walls = FindObjectsOfType<WallHealth>();
        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] != null)
                walls[i].ApplyBonusDelta(delta);
        }

        Debug.Log($"[WallHealth] Global MaxHP bonus = {_globalBonus}");
    }

    private void ApplyBonusDelta(int delta)
    {
        // при увеличении — добавляем HP на столько же (чтобы было именно “+100 HP”)
        if (delta > 0) hp += delta;

        hp = Mathf.Clamp(hp, 0, MaxHp);
    }

    public void TakeDamage(int dmg)
    {
        if (dmg <= 0) return;

        hp -= dmg;
        Debug.Log($"Wall HP: {hp}/{MaxHp}");

        if (hp <= 0)
        {
            OnDestroyed?.Invoke();
            Destroy(gameObject);
        }
    }

    // совместимость со старым кодом
    public void Damage(int dmg) => TakeDamage(dmg);

    // пригодится для “Кузницы” позже
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        hp = Mathf.Min(MaxHp, hp + amount);
        Debug.Log($"Wall HP: {hp}/{MaxHp}");
    }
}
