using UnityEngine;

public sealed class TowerClickUpgradeDebug : MonoBehaviour
{
    [SerializeField] private TowerProgress progress;

    [Header("Debug")]
    [SerializeField] private bool logOnMouseDown = true;
    [SerializeField] private bool logOnMouseOver = false;
    [SerializeField] private bool logOnMouseEnterExit = false;

    private void Awake()
    {
        if (progress == null)
            progress = GetComponent<TowerProgress>();

        Debug.Log($"[TowerClickUpgradeDebug] Awake on '{name}'. progress={(progress != null ? "OK" : "NULL")}, " +
                  $"hasCollider={GetComponent<Collider>() != null}, layer={gameObject.layer}");
    }

    private void OnMouseDown()
    {
        if (logOnMouseDown)
            Debug.Log($"[TowerClickUpgradeDebug] OnMouseDown on '{name}'. progress={(progress != null ? "OK" : "NULL")}");

        if (progress == null) return;

        Debug.Log($"[TowerClickUpgradeDebug] '{name}' state: tier={progress.Tier}, xp={progress.Xp}, xpToNext={progress.XpToNext}, canUpgrade={progress.CanUpgrade}");

        if (!progress.CanUpgrade)
        {
            Debug.Log($"[TowerClickUpgradeDebug] '{name}' cannot upgrade now.");
            return;
        }

        int oldTier = progress.Tier;
        int oldXp = progress.Xp;

        bool ok = progress.ConsumeUpgrade();
        Debug.Log($"[TowerClickUpgradeDebug] ConsumeUpgrade ok={ok}. After: tier={progress.Tier}, xp={progress.Xp}");

        if (ok)
            Debug.Log($"[TowerUpgrade] '{name}': Tier {oldTier}->{progress.Tier}, XP {oldXp}->{progress.Xp}");
    }

    private void OnMouseOver()
    {
        if (!logOnMouseOver) return;
        Debug.Log($"[TowerClickUpgradeDebug] OnMouseOver '{name}'");
    }

    private void OnMouseEnter()
    {
        if (!logOnMouseEnterExit) return;
        Debug.Log($"[TowerClickUpgradeDebug] OnMouseEnter '{name}'");
    }

    private void OnMouseExit()
    {
        if (!logOnMouseEnterExit) return;
        Debug.Log($"[TowerClickUpgradeDebug] OnMouseExit '{name}'");
    }
}
