// WallDragController.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WallDragController : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] private TilePlacement placement;
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
    [SerializeField] private float ghostFitPadding = 1.00f; // 1.0 = как клетка, 0.95 = чуть меньше

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private WallHandSlot activeSlot;
    private WallTileType activeType;
    private int rotation; // 0..5

    private bool dragging;
    private bool holding;
    private Vector2 lastScreenPos;

    private HexCellView hoverCell;

    // Ghost
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
        if (hudCanvas == null) hudCanvas = FindFirstObjectByType<Canvas>();

        // Автопоиск кнопок внутри floatingControlsRoot (даже если объект выключен)
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

    // --- Called by WallHandSlot ------------------------------------------------

    public void BeginDrag(WallHandSlot slot) => BeginDrag(slot, null);

    public void BeginDrag(WallHandSlot slot, PointerEventData eventData)
    {
        if (slot == null) return;
        if (!slot.HasTile) return;

        activeSlot = slot;
        activeType = slot.TileType;
        rotation = 0;

        hoverCell = null;
        holding = false;
        dragging = true;

        HideControls();

        lastScreenPos = eventData != null ? eventData.position : (Vector2)Input.mousePosition;

        CreateGhostFromSlot(slot);
        UpdateHoverCell(lastScreenPos);
        UpdateGhost(lastScreenPos);

        if (debugLogs) Debug.Log($"[WallDrag] BeginDrag slot={slot.slotIndex} type={activeType}");
    }

    public void Drag(PointerEventData eventData)
    {
        if (!dragging) return;
        if (eventData == null) return;

        lastScreenPos = eventData.position;
        UpdateHoverCell(lastScreenPos);
        UpdateGhost(lastScreenPos);
    }

    public void Drag(Vector2 screenPos)
    {
        if (!dragging) return;

        lastScreenPos = screenPos;
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
            if (debugLogs) Debug.Log("[WallDrag] EndDrag -> no hover cell, cancel");
            CancelPlace();
            return;
        }

        holding = true;

        // ВАЖНО: сразу снапаем ghost на центр клетки перед показом контролов
        UpdateGhost(lastScreenPos);

        ShowControlsAtCell(hoverCell);

        if (debugLogs) Debug.Log($"[WallDrag] EndDrag over cell q={hoverCell.q} r={hoverCell.r}");
    }

    // --- UI -------------------------------------------------------------------

    public void UiConfirm()
    {
        Debug.Log("[WallDrag] OK clicked");
        ConfirmPlace();
    }

    public void UiCancel()
    {
        Debug.Log("[WallDrag] X clicked");
        CancelPlace();
    }

    public void UiRotate()
    {
        Debug.Log("[WallDrag] ROT clicked");

        rotation = (rotation + 1) % 6;
        UpdateGhost(lastScreenPos);

        if (hoverCell != null && floatingControlsRoot != null && floatingControlsRoot.gameObject.activeSelf)
            ShowControlsAtCell(hoverCell);
    }

    // --- Placement ------------------------------------------------------------

    private void ConfirmPlace()
    {
        if (placement == null)
        {
            Debug.LogWarning("[WallDrag] TilePlacement is NULL");
            CancelPlace();
            return;
        }

        if (hoverCell == null)
            UpdateHoverCell(lastScreenPos);

        if (hoverCell == null)
        {
            Debug.Log("[WallDrag] Confirm -> hoverCell still null, cancel");
            CancelPlace();
            return;
        }

        bool ok = placement.TryPlaceWall(
            hoverCell.q,
            hoverCell.r,
            hoverCell.transform.position,
            activeType,
            rotation,
            true
        );

        Debug.Log($"[WallDrag] TryPlaceWall result={ok} q={hoverCell.q} r={hoverCell.r} type={activeType} rot={rotation}");

        if (ok && activeSlot != null)
            activeSlot.ClearTile();

        FinishInteraction();
    }

    private void CancelPlace()
    {
        FinishInteraction();
    }

    private void FinishInteraction()
    {
        activeSlot = null;
        hoverCell = null;
        holding = false;
        dragging = false;

        HideControls();
        DestroyGhost();
    }

    // --- Hover cell -----------------------------------------------------------

    private void UpdateHoverCell(Vector2 screenPos)
    {
        hoverCell = RaycastHexCell(screenPos);
    }

    private HexCellView RaycastHexCell(Vector2 screenPos)
    {
        var cam = Cam;
        if (cam == null) return null;

        var ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, 500f))
            return hit.collider.GetComponentInParent<HexCellView>();

        return null;
    }

    // --- Controls positioning -------------------------------------------------

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

    // --- Ghost ----------------------------------------------------------------

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

            // якоря/пивот по центру, чтобы вращение было корректным
            ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
            ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRect.pivot = new Vector2(0.5f, 0.5f);

            ghostCg.blocksRaycasts = false;
            ghostCg.interactable = false;
        }

        ghostRect.sizeDelta = ghostSize;
        ghostCg.alpha = ghostAlpha;

        Sprite spr = null;
        if (slot != null && slot.icon != null) spr = slot.icon.sprite;

        ghostImg.sprite = spr;
        ghostImg.enabled = (spr != null);
        ghostImg.preserveAspect = true;

        ghostRect.localEulerAngles = Vector3.zero;
        ghostRect.gameObject.SetActive(true);
    }

    private void UpdateGhost(Vector2 screenPos)
    {
        if (ghostRect == null || hudCanvas == null) return;

        // 1) позиция: в BUILD-holding снапаем в центр клетки
        Vector2 anchoredPos;
        if (snapGhostToCellWhenHolding && holding && hoverCell != null)
        {
            anchoredPos = ScreenToAnchored(WorldToScreen(hoverCell.transform.position));
        }
        else
        {
            anchoredPos = ScreenToAnchored(screenPos);
        }

        ghostRect.anchoredPosition = anchoredPos;

        // 2) размер: подгоняем в размер клетки на экране (опционально)
        if (autoFitGhostToCell && hoverCell != null && hoverCell.rend != null && TryGetGhostSizeFromCell(hoverCell, out float px))
        {
            ghostRect.sizeDelta = new Vector2(px, px);
        }
        else
        {
            ghostRect.sizeDelta = ghostSize;
        }

        // 3) вращение
        ghostRect.localEulerAngles = new Vector3(0f, 0f, -rotation * 60f);

        // 4) держим спрайт актуальным
        if (activeSlot != null && activeSlot.icon != null && ghostImg != null)
        {
            var spr = activeSlot.icon.sprite;
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

        // ширина по X
        var l = cam.WorldToScreenPoint(c - new Vector3(e.x, 0f, 0f));
        var r = cam.WorldToScreenPoint(c + new Vector3(e.x, 0f, 0f));
        float w = Mathf.Abs(r.x - l.x);

        // высота по Z (в screen Y при top-down обычно это даёт адекватную оценку)
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

    // --- Helpers --------------------------------------------------------------

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
