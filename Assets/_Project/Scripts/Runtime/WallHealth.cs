using System;
using UnityEngine;

public sealed class WallHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 500;

    public int Hp { get; private set; }

    public event Action OnDestroyed;

    private void Awake()
    {
        Hp = maxHp;
    }

    public void TakeDamage(int dmg)
    {
        if (dmg <= 0) return;

        Hp -= dmg;
        Debug.Log($"Wall HP: {Hp}");

        if (Hp <= 0)
        {
            OnDestroyed?.Invoke();
            Destroy(gameObject);
        }
    }

    // Совместимость со старым кодом (EnemyMover вызывает Damage)
    public void Damage(int dmg)
    {
        TakeDamage(dmg);
    }
}
