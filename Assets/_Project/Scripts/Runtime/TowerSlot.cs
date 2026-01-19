using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public sealed class TowerSlot : MonoBehaviour
{
    [Header("Runtime link (set by TilePlacement)")]
    [SerializeField] private TilePlacement placement;
    [SerializeField] private int q;
    [SerializeField] private int r;

    [Header("Rules")]
    [SerializeField] private bool isEnabledForThisWall = true;

    [Header("Economy")]
    [SerializeField] private int buildCost = 10;

    private TowerBuildPopup cachedPopup;

    public void Setup(TilePlacement p, int cellQ, int cellR, bool enabledForWall)
    {
        placement = p;
        q = cellQ;
        r = cellR;
        isEnabledForThisWall = enabledForWall;

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = isEnabledForThisWall;
    }

    private void OnMouseUpAsButton()
    {
        // Если клик по UI (кнопки rotate/confirm/cancel и т.п.) — не открываем меню
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!isEnabledForThisWall) return;
        if (placement == null) return;
        if (placement.HasTowerAt(q, r)) return;

        if (cachedPopup == null)
        {
            cachedPopup = TowerBuildPopup.Instance;
            if (cachedPopup == null)
                cachedPopup = FindFirstObjectByType<TowerBuildPopup>(FindObjectsInactive.Include);
        }

        if (cachedPopup != null)
            cachedPopup.Show(this);
    }

    public void TryBuild(TowerType type)
    {
        if (!isEnabledForThisWall) return;
        if (placement == null) return;
        if (placement.HasTowerAt(q, r)) return;

        if (!GoldBank.TrySpend(buildCost))
            return;

        bool ok = placement.TryPlaceTower(q, r, type, allowReplace: false);
        if (!ok)
            GoldBank.Add(buildCost);
    }
}
