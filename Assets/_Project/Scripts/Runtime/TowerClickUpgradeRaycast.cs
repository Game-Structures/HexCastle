using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TowerClickUpgradeRaycast : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera cam;
    [SerializeField] private TowerUpgradePanelUI panel;

    [Header("Settings")]
    [SerializeField] private bool ignoreClicksOverUI = true;
    [SerializeField] private float maxDistance = 500f;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (panel == null) panel = FindFirstObjectByType<TowerUpgradePanelUI>();
    }

    private void Update()
    {
        // Если панель открыта – не обрабатываем клики по миру
        if (panel != null && panel.gameObject.activeInHierarchy)
        {
            // panelRoot может быть дочерним и включаться/выключаться, поэтому проверяем корень:
            // если корень выключен, gameObject.activeInHierarchy будет false и мы сюда не попадем.
        }

        if (!Input.GetMouseButtonDown(0)) return;

        if (ignoreClicksOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        var hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        TowerProgress progress = null;
        TowerShooter shooter = null;

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i].collider;
            var tp = col.GetComponentInParent<TowerProgress>();
            if (tp != null)
            {
                progress = tp;
                shooter = col.GetComponentInParent<TowerShooter>();
                break;
            }
        }

        if (progress == null || shooter == null) return;
        if (!progress.CanUpgrade) return;

        if (panel == null)
            panel = FindFirstObjectByType<TowerUpgradePanelUI>();

        var options = GenerateThreeOptions();

        // Открываем UI
        if (panel != null)
            panel.Show(progress, shooter, options, GetTitle);
        else
            Debug.LogWarning("[TowerUpgrade] TowerUpgradePanelUI not found in scene.");
    }

    private static TowerUpgradeId[] GenerateThreeOptions()
    {
        TowerUpgradeId[] pool = new[]
        {
            TowerUpgradeId.DamagePlus10,
            TowerUpgradeId.FireRatePlus10,
            TowerUpgradeId.RangePlus1,
        };

        for (int i = pool.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return new[] { pool[0], pool[1], pool[2] };
    }

    private static string GetTitle(TowerUpgradeId id)
    {
        switch (id)
        {
            case TowerUpgradeId.DamagePlus10: return "+10% Damage";
            case TowerUpgradeId.FireRatePlus10: return "+10% Attack Speed";
            case TowerUpgradeId.RangePlus1: return "+1 Range";
            default: return id.ToString();
        }
    }
}
