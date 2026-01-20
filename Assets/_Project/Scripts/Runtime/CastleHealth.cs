using UnityEngine;
using UnityEngine.Events;

public sealed class CastleHealth : MonoBehaviour
{
    public UnityEvent<int, int> onHealthChanged; // current, max
    public UnityEvent onCastleDestroyed;

    [SerializeField] private int maxHp = 3000;
    [SerializeField] private int hp = 3000;

    [Header("Damage popup")]
    [SerializeField] private bool showDamagePopup = true;
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 2.2f, 0f);

    public int CurrentHp => hp;
    public int MaxHp => maxHp;

    private void Awake()
    {
        hp = Mathf.Clamp(hp, 0, maxHp);
        onHealthChanged?.Invoke(hp, maxHp);
    }

    public void SetMaxHp(int newMax, bool healToFull = true)
    {
        maxHp = Mathf.Max(1, newMax);
        if (healToFull) hp = maxHp;
        hp = Mathf.Clamp(hp, 0, maxHp);
        onHealthChanged?.Invoke(hp, maxHp);
    }

    public void Damage(int amount)
    {
        if (amount <= 0) return;
        if (hp <= 0) return;

        if (showDamagePopup)
        {
            DamagePopupWorld.Spawn(
                transform.position + popupOffset,
                amount,
                DamagePopupWorld.PopupKind.PlayerDamaged
            );
        }

        hp = Mathf.Max(0, hp - amount);
        onHealthChanged?.Invoke(hp, maxHp);

        if (hp <= 0)
        {
            onCastleDestroyed?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        if (hp <= 0) return;

        hp = Mathf.Min(maxHp, hp + amount);
        onHealthChanged?.Invoke(hp, maxHp);
    }
}
