using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Collider))]
public sealed class TowerSlot : MonoBehaviour
{
    [Header("Runtime link (set by TilePlacement)")]
    [SerializeField] private TilePlacement placement;
    [SerializeField] private int q;
    [SerializeField] private int r;

    [Header("Slot Visual (root that will be scaled in XZ)")]
    [SerializeField] private GameObject slotVisual;

    [Header("Rules")]
    [SerializeField] private bool isEnabledForThisWall = true;

    [Header("Economy")]
    [SerializeField] private int buildCost = 10;

    private TowerBuildPopup cachedPopup;

    private float suggestedDiameter = -1f;

    private Vector3 slotVisualBaseScale = Vector3.one;
    private bool baseScaleCached;

    private void Awake()
    {
        CacheBaseScale();
        RefreshVisual();
    }

    private void CacheBaseScale()
    {
        if (baseScaleCached) return;
        if (slotVisual == null) return;
        slotVisualBaseScale = slotVisual.transform.localScale;
        if (slotVisualBaseScale == Vector3.zero) slotVisualBaseScale = Vector3.one;
        baseScaleCached = true;
    }

    public void Setup(TilePlacement p, int cellQ, int cellR, bool enabledForWall)
    {
        placement = p;
        q = cellQ;
        r = cellR;
        isEnabledForThisWall = enabledForWall;

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = isEnabledForThisWall;

        RefreshVisual();
    }

    public void SetSuggestedDiameter(float diameter)
    {
        suggestedDiameter = diameter;
        ApplySlotVisualScale();
    }

    private void ApplySlotVisualScale()
    {
        if (slotVisual == null) return;
        if (suggestedDiameter <= 0f) return;

        CacheBaseScale();

        // Наша “нормализованная башня” собрана под диаметр=1.
        // Поэтому X/Z множитель = suggestedDiameter.
        float k = suggestedDiameter;

        slotVisual.transform.localScale = new Vector3(
            slotVisualBaseScale.x * k,
            slotVisualBaseScale.y,      // высоту не трогаем
            slotVisualBaseScale.z * k
        );
    }

    public void RefreshVisual()
    {
        if (slotVisual != null)
        {
            bool hasTower = placement != null && placement.HasTowerAt(q, r);
            slotVisual.SetActive(isEnabledForThisWall && !hasTower);
        }

        ApplySlotVisualScale();
    }

    private void OnMouseUpAsButton()
    {
        if (TowerSlotBlocker.IsBlocked) return;

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
        {
            GoldBank.Add(buildCost);
            return;
        }

        RefreshVisual();
    }
}
