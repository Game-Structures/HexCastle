using UnityEngine;

public sealed class TowerEnsureColliderEnabled : MonoBehaviour
{
    [SerializeField] private Collider targetCollider;
    [SerializeField] private bool logOnce = false;

    private bool logged;

    private void Awake()
    {
        if (targetCollider == null)
            targetCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        EnableNow();
    }

    private void Start()
    {
        // Start гарантированно после того, как внешние скрипты могли что-то выключить в Awake/OnEnable
        EnableNow();
    }

    private void LateUpdate()
    {
        // На случай если кто-то выключает коллайдер каждый кадр
        if (targetCollider != null && !targetCollider.enabled)
            EnableNow();
    }

    private void EnableNow()
    {
        if (targetCollider == null) return;

        if (!targetCollider.enabled)
        {
            targetCollider.enabled = true;

            if (logOnce && !logged)
            {
                logged = true;
                Debug.Log($"[TowerEnsureColliderEnabled] Re-enabled collider on '{name}'.");
            }
        }
    }
}
