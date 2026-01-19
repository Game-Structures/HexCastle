using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WallDragController : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] private TilePlacement placement;
    [SerializeField] private WallHandManager handManager;
    [SerializeField] private Canvas hudCanvas;

    [Header("Floating Controls (triangle)")]
    [SerializeField] private RectTransform floatingControlsRoot;
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Vector2 controlsOffset = Vector2.zero;

    [Header("Ghost UI")]
    [SerializeField] private Vector2 ghostSize = new Vector2(170, 170);
    [Range(0f, 1f)]
    [SerializeField] private float ghostAlpha = 0.9f;

    [Header("Ghost behavior")]
    [SerializeField] private bool snapGhostToCellWhenHolding = true;
    [SerializeField] private bool autoFitGhostToCell = true;
    [SerializeField] private float ghostFitPadding = 1.00f;

    private WallHandSlot activeSlot;

    private HandItemKind activeKind;
    private WallTileType activeWallType;
    private TowerType activeTowerType;

    private int rotation; // for Wall / Combo

    private bool dragging;
    private bool holding;
    private Vector2 lastScreenPos;

    private HexCellView hoverCell;

    private RectTransform ghostRect;
    private Image ghostImg;
    private CanvasGroup ghostCg;

    private Camera Cam
    {
        get
        {
            if (hudCanvas == null) return Camera.main;
            if (hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return Camera.main;
            return hudCanvas.worldCamera != null ? hudCanvas.worldCamera : Camera.main;
        }
    }

    private void Awake()
    {
        AutoAssignRefs();
        HookButtons();

        HideControls();
        DestroyGhost();
    }

    private void OnEnable()
    {
        AutoAssignRefs();
        HookButtons();
        HideControls();
    }

    private void AutoAssignRefs()
    {
        if (placement == null) placement = FindFirstObjectByType<TilePlacement>();
        if (handManager == null) handManager = FindFirstObjectByType<WallHandManager>();
        if (hudCanvas == null) hudCanvas = FindFirstObjectByType<Canvas>();

        if (floatingControlsRoot != null)
        {
            if (rotateButton == null) rotateButton = FindButtonByNameContains(floatingControlsRoot, "Rotate");
            if (confirmButton == null) confirmButton = FindButtonByNameContains(floatingControlsRoot, "Confirm");
            if (cancelButton == null) cancelButton = FindButtonByNameContains(floatingControlsRoot, "Cancel");
        }
    }

    private static Button FindButtonByNameContains(RectTransform root, string token)
    {
        var buttons = root.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            if (b != null && b.name.ToLower().Contains(token.ToLower()))
                return b;
        }
        return null;
    }

    private void HookButtons()
    {
        if (rotateButton != null)
        {
            rotateButton.onClick.RemoveListener(UiRotate);
            rotateButton.onClick.AddListener(UiRotate);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(UiConfirm);
            confirmButton.onClick.AddListener(UiConfirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(UiCancel);
            cancelButton.onClick.AddListener(UiCancel);
        }
    }

    public void BeginDrag(WallHandSlot slot, PointerEventData eventData)
    {
        if (slot == null) return;
        if (!slot.HasTile) return;

        activeSlot = slot;
        activeKind = slot.Kind;

        activeWallType = slot.TileType;
        activeTowerType = slot.TowerType;

        rotation = (activeKind == HandItemKind.Tower) ? 0 : slot.GetRotation();

        hoverCell = null;
        holding = false;
        dragging = true;

        HideControls();

        lastScreenPos = eventData != null ? eventData.position : (Vector2)Input.mousePosition;

        CreateGhostFromSlot(slot);
        UpdateHoverCell(lastScreenPos);
        UpdateGhost(lastScreenPos);

        SetRotateVisible(activeKind == HandItemKind.Wall || activeKind == HandItemKind.Combo);
    }

    public void Drag(PointerEventData eventData)
    {
        if (!dragging) return;
        if (eventData == null) return;

        lastScreenPos = eventData.position;
        UpdateHoverCell(lastScreenPos);
        UpdateGhost(lastScreenPos);
    }

    public void EndDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        if (eventData != null) lastScreenPos = eventData.position;

        dragging = false;

        UpdateHoverCell(lastScreenPos);

        if (hoverCell == null)
        {
            CancelPlace();
            return;
        }

        holding = true;

        UpdateGhost(lastScreenPos);
        ShowControlsAtCell(hoverCell);
    }

    public void UiConfirm() => ConfirmPlace();
    public void UiCancel() => CancelPlace();

    public void UiRotate()
    {
        if (activeKind != HandItemKind.Wall && activeKind != HandItemKind.Combo) return;

        rotation = (rotation + 1) % 6;
        UpdateGhost(lastScreenPos);

        if (hoverCell != null && floatingControlsRoot != null && floatingControlsRoot.gameObject.activeSelf)
            ShowControlsAtCell(hoverCell);
    }

    private void ConfirmPlace()
    {
        if (placement == null)
        {
            CancelPlace();
            return;
        }

        if (hoverCell == null)
            UpdateHoverCell(lastScreenPos);

        if (hoverCell == null)
        {
            CancelPlace();
            return;
        }

        bool ok = false;

        if (activeKind == HandItemKind.Tower)
        {
            ok = placement.TryPlaceTower(hoverCell.q, hoverCell.r, activeTowerType, true);
        }
        else if (activeKind == HandItemKind.Wall)
        {
            ok = placement.TryPlaceWall(
                hoverCell.q, hoverCell.r, hoverCell.transform.position,
                activeWallType, rotation, true
            );
        }
        else if (activeKind == HandItemKind.Combo)
        {
            bool okWall = placement.TryPlaceWall(
                hoverCell.q, hoverCell.r, hoverCell.transform.position,
                activeWallType, rotation, true
            );

            if (okWall)
            {
                bool okTower = placement.TryPlaceTower(hoverCell.q, hoverCell.r, activeTowerType, true);
                ok = okWall && okTower;

                // Если башня вдруг не поставилась – оставляем стену (без отката), чтобы не потерять возможную замену стены
                // ok может быть false – тогда слот НЕ расходуем
                if (!okTower) ok = false;
            }
        }

        if (ok && activeSlot != null)
        {
            if (handManager != null) handManager.ConsumeSlot(activeSlot.slotIndex);
            else activeSlot.ClearTile();
        }

        FinishInteraction();
    }

    private void CancelPlace() => FinishInteraction();

    private void FinishInteraction()
    {
        activeSlot = null;
        hoverCell = null;
        holding = false;
        dragging = false;

        activeKind = HandItemKind.None;

        HideControls();
        DestroyGhost();

        SetRotateVisible(true);
    }

    private void UpdateHoverCell(Vector2 screenPos)
    {
        hoverCell = RaycastHexCell(screenPos);
    }

    // ВАЖНО: RaycastAll – чтобы коллайдер TowerSlot не мешал выбирать клетку
    private HexCellView RaycastHexCell(Vector2 screenPos)
    {
        var cam = Cam;
        if (cam == null) return null;

        var ray = cam.ScreenPointToRay(screenPos);
        var hits = Physics.RaycastAll(ray, 500f);
        if (hits == null || hits.Length == 0) return null;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i].collider;
            if (col == null) continue;

            var cell = col.GetComponentInParent<HexCellView>();
            if (cell != null) return cell;
        }

        return null;
    }

    private void HideControls()
    {
        if (floatingControlsRoot != null)
            floatingControlsRoot.gameObject.SetActive(false);
    }

    private void ShowControlsAtCell(HexCellView cell)
    {
        if (floatingControlsRoot == null || hudCanvas == null || cell == null) return;

        floatingControlsRoot.gameObject.SetActive(true);

        Vector2 screen = WorldToScreen(cell.transform.position);
        Vector2 anchored = ScreenToAnchored(screen);

        floatingControlsRoot.anchoredPosition = anchored + controlsOffset;
    }

    private void SetRotateVisible(bool visible)
    {
        if (rotateButton != null)
            rotateButton.gameObject.SetActive(visible);
    }

    private void CreateGhostFromSlot(WallHandSlot slot)
    {
        if (hudCanvas == null) return;

        if (ghostRect == null)
        {
            var go = new GameObject("WallGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(hudCanvas.transform, false);

            ghostRect = go.GetComponent<RectTransform>();
            ghostImg = go.GetComponent<Image>();
            ghostCg = go.GetComponent<CanvasGroup>();

            ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
            ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRect.pivot = new Vector2(0.5f, 0.5f);

            ghostCg.blocksRaycasts = false;
            ghostCg.interactable = false;
        }

        ghostRect.sizeDelta = ghostSize;
        ghostCg.alpha = ghostAlpha;

        Sprite spr = slot != null ? slot.GetSprite() : null;

        ghostImg.sprite = spr;
        ghostImg.enabled = (spr != null);
        ghostImg.preserveAspect = true;

        ghostRect.localEulerAngles = Vector3.zero;
        ghostRect.gameObject.SetActive(true);
    }

    private void UpdateGhost(Vector2 screenPos)
    {
        if (ghostRect == null || hudCanvas == null) return;

        Vector2 anchoredPos;
        if (snapGhostToCellWhenHolding && holding && hoverCell != null)
            anchoredPos = ScreenToAnchored(WorldToScreen(hoverCell.transform.position));
        else
            anchoredPos = ScreenToAnchored(screenPos);

        ghostRect.anchoredPosition = anchoredPos;

        if (autoFitGhostToCell && hoverCell != null && hoverCell.rend != null && TryGetGhostSizeFromCell(hoverCell, out float px))
            ghostRect.sizeDelta = new Vector2(px, px);
        else
            ghostRect.sizeDelta = ghostSize;

        ghostRect.localEulerAngles = new Vector3(0f, 0f, -rotation * 60f);

        if (activeSlot != null && ghostImg != null)
        {
            var spr = activeSlot.GetSprite();
            ghostImg.sprite = spr;
            ghostImg.enabled = (spr != null);
            ghostImg.preserveAspect = true;
        }
    }

    private bool TryGetGhostSizeFromCell(HexCellView cell, out float sizePx)
    {
        sizePx = 0f;

        var cam = Cam;
        if (cam == null || cell == null || cell.rend == null) return false;

        var b = cell.rend.bounds;
        var c = b.center;
        var e = b.extents;

        var l = cam.WorldToScreenPoint(c - new Vector3(e.x, 0f, 0f));
        var r = cam.WorldToScreenPoint(c + new Vector3(e.x, 0f, 0f));
        float w = Mathf.Abs(r.x - l.x);

        var d = cam.WorldToScreenPoint(c - new Vector3(0f, 0f, e.z));
        var u = cam.WorldToScreenPoint(c + new Vector3(0f, 0f, e.z));
        float h = Mathf.Abs(u.y - d.y);

        float s = Mathf.Min(w, h) * ghostFitPadding;
        if (float.IsNaN(s) || float.IsInfinity(s) || s < 8f) return false;

        sizePx = s;
        return true;
    }

    private void DestroyGhost()
    {
        if (ghostRect != null)
        {
            Destroy(ghostRect.gameObject);
            ghostRect = null;
            ghostImg = null;
            ghostCg = null;
        }
    }

    private Vector2 WorldToScreen(Vector3 worldPos)
    {
        var cam = Cam;
        if (cam == null) return Vector2.zero;
        var sp = cam.WorldToScreenPoint(worldPos);
        return new Vector2(sp.x, sp.y);
    }

    private Vector2 ScreenToAnchored(Vector2 screenPos)
    {
        var canvasRect = hudCanvas.transform as RectTransform;
        if (canvasRect == null) return Vector2.zero;

        Camera cam = (hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : hudCanvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out var local);
        return local;
    }
}
