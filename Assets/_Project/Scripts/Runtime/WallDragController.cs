using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WallDragController : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

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
    [SerializeField] private float ghostAlpha = 0.90f;

    [Header("Ghost Colors")]
[SerializeField] private bool tintOnlyAfterDrop = true;
[SerializeField] private Color dragNeutralColor = new Color(1f, 1f, 1f, 1f); // белый


    private WallHandSlot activeSlot;

    private HandItemKind activeKind;
    private WallTileType activeWallType;
    private TowerType activeTowerType;

    // WallPair
    private WallTileType pairA;
    private WallTileType pairB;
    private int pairBaseDir;
    private int pairRotOffsetA;
    private int pairRotOffsetB;

    private int rotation;

    private bool draggingFromHand;
    private bool placingMode;        // режим размещения активен
    private bool repositionDragging; // перетаскивание уже в режиме размещения

    private Vector2 lastScreenPos;

    private HexCellView hoverCell;
    private HexCellView placeCell;   // текущая закрепленная клетка

    // Ghost root
    private RectTransform ghostRoot;
    private Image ghostImgA;
    private Image ghostImgB;
    private CanvasGroup ghostCg;

    private bool previewValid;

    // Axial directions
    private static readonly Vector2Int[] AxialDirs =
    {
        new Vector2Int( 1,  0),
        new Vector2Int( 1, -1),
        new Vector2Int( 0, -1),
        new Vector2Int(-1,  0),
        new Vector2Int(-1,  1),
        new Vector2Int( 0,  1),
    };

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

    private void Update()
    {
        if (!placingMode) return;

        // Быстрая отмена
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            Log("Cancel by RMB/ESC");
            FinishInteraction();
            return;
        }

        // В режиме размещения: ЛКМ зажат = двигаем превью (камера заблокирована)
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            repositionDragging = true;
            Log("Reposition drag START");
        }

        if (Input.GetMouseButton(0) && repositionDragging)
        {
            lastScreenPos = Input.mousePosition;
            UpdateHoverCell(lastScreenPos);

            if (hoverCell != null)
            {
                placeCell = hoverCell;
                UpdateGhostAndPreview();
                ShowControlsAtCell(placeCell);
            }
        }

        if (Input.GetMouseButtonUp(0) && repositionDragging)
        {
            repositionDragging = false;
            Log($"Reposition drag END -> placeCell={(placeCell != null ? $"{placeCell.q},{placeCell.r}" : "null")}");
            UpdateGhostAndPreview();
            if (placeCell != null) ShowControlsAtCell(placeCell);
        }
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
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
            if (b != null && b.name.ToLower().Contains(token.ToLower()))
                return b;
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

        // блокируем камеру на весь период размещения
        PlacementInputLock.IsPlacing = true;
        TowerSlotBlocker.SetBlocked(true);

        activeSlot = slot;
        activeKind = slot.Kind;

        activeWallType = slot.TileType;
        activeTowerType = slot.TowerType;

        if (activeKind == HandItemKind.WallPair)
        {
            pairA = slot.PairTypeA;
            pairB = slot.PairTypeB;
            pairBaseDir = slot.PairBaseDir;
            pairRotOffsetA = slot.PairRotOffsetA;
            pairRotOffsetB = slot.PairRotOffsetB;
        }
        else
        {
            pairA = WallTileType.None;
            pairB = WallTileType.None;
            pairBaseDir = 0;
            pairRotOffsetA = 0;
            pairRotOffsetB = 0;
        }

        rotation = (activeKind == HandItemKind.Tower) ? 0 : slot.GetRotation();

        draggingFromHand = true;
        placingMode = false;
        repositionDragging = false;

        hoverCell = null;
        placeCell = null;

        HideControls();

        lastScreenPos = eventData != null ? eventData.position : (Vector2)Input.mousePosition;

        CreateGhostFromSlot(slot);
        UpdateHoverCell(lastScreenPos);
        UpdateGhostDuringHandDrag(lastScreenPos);

        bool canRotate = (activeKind == HandItemKind.Wall || activeKind == HandItemKind.Combo || activeKind == HandItemKind.WallPair);
        SetRotateVisible(canRotate);

        Log($"BeginDrag kind={activeKind} rot={rotation}");
    }

    public void Drag(PointerEventData eventData)
    {
        if (!draggingFromHand) return;
        if (eventData == null) return;

        lastScreenPos = eventData.position;
        UpdateHoverCell(lastScreenPos);
        UpdateGhostDuringHandDrag(lastScreenPos);
    }

    public void EndDrag(PointerEventData eventData)
    {
        if (!draggingFromHand) return;

        if (eventData != null) lastScreenPos = eventData.position;

        draggingFromHand = false;

        UpdateHoverCell(lastScreenPos);

        if (hoverCell == null)
        {
            Log("EndDrag -> hoverCell NULL -> cancel");
            FinishInteraction();
            return;
        }

        // ключевое: сразу фиксируем на клетке, без доп. клика
        placeCell = hoverCell;
        placingMode = true;
        repositionDragging = false;

        UpdateGhostAndPreview();
        ShowControlsAtCell(placeCell);

        Log($"EndDrag -> placingMode ON at ({placeCell.q},{placeCell.r})");
    }

    public void UiConfirm() => ConfirmPlace();
    public void UiCancel() => FinishInteraction();

    public void UiRotate()
    {
        if (!placingMode) return;
        if (activeKind != HandItemKind.Wall && activeKind != HandItemKind.Combo && activeKind != HandItemKind.WallPair)
            return;

        rotation = (rotation + 1) % 6;

        UpdateGhostAndPreview();
        if (placeCell != null) ShowControlsAtCell(placeCell);

        Log($"Rotate -> rot={rotation}");
    }

    private void ConfirmPlace()
    {
        if (!placingMode) return;
        if (!previewValid) { Log("Confirm blocked: preview invalid"); return; }
        if (placement == null || placeCell == null) return;

        bool ok = false;

        // replace выключен (false)
        if (activeKind == HandItemKind.Tower)
        {
            ok = placement.TryPlaceTower(placeCell.q, placeCell.r, activeTowerType, true);
        }
        else if (activeKind == HandItemKind.Wall)
        {
            ok = placement.TryPlaceWall(placeCell.q, placeCell.r, placeCell.transform.position, activeWallType, rotation, false);
        }
        else if (activeKind == HandItemKind.Combo)
        {
            bool okWall = placement.TryPlaceWall(placeCell.q, placeCell.r, placeCell.transform.position, activeWallType, rotation, false);
            if (okWall)
            {
                bool okTower = placement.TryPlaceTower(placeCell.q, placeCell.r, activeTowerType, true);
                ok = okWall && okTower;
            }
        }
        else if (activeKind == HandItemKind.WallPair)
        {
            ok = TryPlaceWallPair(placeCell);
        }

        Log($"Confirm result ok={ok}");

        if (ok && activeSlot != null)
        {
            if (handManager != null) handManager.ConsumeSlot(activeSlot.slotIndex);
            else activeSlot.ClearTile();
        }

        FinishInteraction();
    }

    private bool TryPlaceWallPair(HexCellView cellA)
{
    if (placement == null || cellA == null) return false;

    int qA = cellA.q;
    int rA = cellA.r;
    var keyA = new Vector2Int(qA, rA);

    int dirToB = Mod6(pairBaseDir + rotation);
    var d = AxialDirs[dirToB];

    int qB = qA + d.x;
    int rB = rA + d.y;
    var keyB = new Vector2Int(qB, rB);

    // BLOCK: нельзя ставить любую часть пары на клетку замка (0,0)
    if ((qA == 0 && rA == 0) || (qB == 0 && rB == 0))
        return false;

    if (!placement.TryGetCell(qB, rB, out var cellB))
        return false;

    int rotA = Mod6(rotation + pairRotOffsetA);
    int rotB = Mod6(rotation + pairRotOffsetB);

    // allowReplace = false
    if (!placement.CanPreviewPlaceWall(qA, rA, pairA, rotA, false, keyB, out int maskA, out bool extA)) return false;
    if (!placement.CanPreviewPlaceWall(qB, rB, pairB, rotB, false, keyA, out int maskB, out bool extB)) return false;

    bool mutual = ((maskA & (1 << dirToB)) != 0) && ((maskB & (1 << Opp(dirToB))) != 0);
    if (!mutual) return false;

    if (!extA && !extB) return false;

    // ставим первым тот, у кого есть внешний коннект
    if (extA)
    {
        bool ok1 = placement.TryPlaceWall(qA, rA, cellA.transform.position, pairA, rotA, false);
        if (!ok1) return false;

        bool ok2 = placement.TryPlaceWall(qB, rB, cellB.transform.position, pairB, rotB, false);
        if (!ok2)
        {
            placement.RemoveWallAt(qA, rA);
            return false;
        }

        return true;
    }
    else
    {
        bool ok1 = placement.TryPlaceWall(qB, rB, cellB.transform.position, pairB, rotB, false);
        if (!ok1) return false;

        bool ok2 = placement.TryPlaceWall(qA, rA, cellA.transform.position, pairA, rotA, false);
        if (!ok2)
        {
            placement.RemoveWallAt(qB, rB);
            return false;
        }

        return true;
    }
}


    private void FinishInteraction()
    {
        Log("FinishInteraction");

        activeSlot = null;
        hoverCell = null;
        placeCell = null;

        draggingFromHand = false;
        placingMode = false;
        repositionDragging = false;

        activeKind = HandItemKind.None;

        HideControls();
        DestroyGhost();

        SetRotateVisible(true);

        PlacementInputLock.IsPlacing = false;
        TowerSlotBlocker.SetBlocked(false);
    }

    private void UpdateHoverCell(Vector2 screenPos)
    {
        hoverCell = RaycastHexCell(screenPos);
    }

    private HexCellView RaycastHexCell(Vector2 screenPos)
    {
        var cam = Cam;
        if (cam == null) return null;

        var ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out var hit, 500f)) return null;

        var col = hit.collider;
        if (col == null) return null;

        return col.GetComponentInParent<HexCellView>();
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

        if (ghostRoot == null)
        {
            var root = new GameObject("HandGhostRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
            root.transform.SetParent(hudCanvas.transform, false);

            ghostRoot = root.GetComponent<RectTransform>();
            ghostCg = root.GetComponent<CanvasGroup>();
            ghostCg.blocksRaycasts = false;
            ghostCg.interactable = false;

            ghostRoot.anchorMin = new Vector2(0.5f, 0.5f);
            ghostRoot.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRoot.pivot = new Vector2(0.5f, 0.5f);

            var a = new GameObject("A", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            a.transform.SetParent(root.transform, false);
            var rtA = a.GetComponent<RectTransform>();
            rtA.pivot = new Vector2(0.5f, 0.5f);
            rtA.sizeDelta = ghostSize;
            ghostImgA = a.GetComponent<Image>();
            ghostImgA.preserveAspect = true;

            var b = new GameObject("B", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            b.transform.SetParent(root.transform, false);
            var rtB = b.GetComponent<RectTransform>();
            rtB.pivot = new Vector2(0.5f, 0.5f);
            rtB.sizeDelta = ghostSize;
            ghostImgB = b.GetComponent<Image>();
            ghostImgB.preserveAspect = true;
        }

        ghostCg.alpha = ghostAlpha;

        ghostImgA.sprite = slot != null ? slot.GetGhostSpriteA() : null;
        ghostImgA.enabled = (ghostImgA.sprite != null);

        bool isPair = (slot != null && slot.Kind == HandItemKind.WallPair);

        ghostImgB.sprite = isPair ? slot.GetGhostSpriteB() : null;
        ghostImgB.enabled = isPair && (ghostImgB.sprite != null);

        ghostRoot.gameObject.SetActive(true);
    }

    // Пока тащим из руки — показываем под курсором
    // Пока тащим из руки — показываем под курсором И сразу применяем rotation,
// чтобы совпадало с тем, что в руке и что будет поставлено.
// Пока тащим из руки — показываем под курсором и сразу считаем валидность по hoverCell
private void UpdateGhostDuringHandDrag(Vector2 screenPos)
{
    if (ghostRoot == null) return;

    // A под курсором
    Vector2 posA = ScreenToAnchored(screenPos);
    ghostImgA.rectTransform.anchoredPosition = posA;

    // Включаем B если WallPair
    bool isPair = (activeKind == HandItemKind.WallPair);
    if (ghostImgB != null)
        ghostImgB.enabled = isPair && (ghostImgB.sprite != null);

    // rotation визуально
    if (activeKind == HandItemKind.Wall || activeKind == HandItemKind.Combo)
    {
        ghostImgA.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotation * 60f);
    }
    else if (activeKind == HandItemKind.WallPair)
    {
        int rotA = Mod6(rotation + pairRotOffsetA);
        int rotB = Mod6(rotation + pairRotOffsetB);

        ghostImgA.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotA * 60f);

        if (ghostImgB != null && ghostImgB.enabled)
        {
            ghostImgB.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotB * 60f);

            int dirToB = Mod6(pairBaseDir + rotation);
            Vector2 posB = posA + ApproxHexUiOffset(dirToB) * (ghostSize.x * 0.85f);
            ghostImgB.rectTransform.anchoredPosition = posB;
        }
    }
    else
    {
        ghostImgA.rectTransform.localEulerAngles = Vector3.zero;
    }

    // NEW: валидность по hoverCell (куда сейчас наведена мышь)
    previewValid = false;

    if (placement != null && hoverCell != null)
    {
        if (activeKind == HandItemKind.Wall || activeKind == HandItemKind.Combo)
        {
            var keyNo = new Vector2Int(999999, 999999);
            bool ok = placement.CanPreviewPlaceWall(hoverCell.q, hoverCell.r, activeWallType, rotation, false, keyNo, out int _mask, out bool ext);
            previewValid = ok && ext;
        }
        else if (activeKind == HandItemKind.WallPair)
        {
            int dirToB = Mod6(pairBaseDir + rotation);
            var d = AxialDirs[dirToB];

            int qA = hoverCell.q;
            int rA = hoverCell.r;

            int qB = qA + d.x;
            int rB = rA + d.y;

            if (placement.TryGetCell(qB, rB, out var _))
            {
                int rotA = Mod6(rotation + pairRotOffsetA);
                int rotB = Mod6(rotation + pairRotOffsetB);

                var keyA = new Vector2Int(qA, rA);
                var keyB = new Vector2Int(qB, rB);

                bool okA = placement.CanPreviewPlaceWall(qA, rA, pairA, rotA, false, keyB, out int maskA, out bool extA);
                bool okB = placement.CanPreviewPlaceWall(qB, rB, pairB, rotB, false, keyA, out int maskB, out bool extB);

                bool mutual = okA && okB
                    && ((maskA & (1 << dirToB)) != 0)
                    && ((maskB & (1 << Opp(dirToB))) != 0);

                previewValid = okA && okB && mutual && (extA || extB);
            }
        }
        else
        {
            // Tower
            previewValid = true;
        }
    }

    if (tintOnlyAfterDrop)
{
    ApplyGhostNeutral();
    // confirm во время drag не нужен
    SetConfirmInteractable(false);
}
else
{
    ApplyGhostTint(previewValid);
    SetConfirmInteractable(previewValid);
}


    if (debugLogs)
        Debug.Log($"[WallDrag] DragPreview valid={previewValid} hover={(hoverCell != null ? $"{hoverCell.q},{hoverCell.r}" : "null")} rot={rotation} kind={activeKind}");
}



    // В режиме размещения — строго по placeCell (фиксировано), валидность по placeCell
    private void UpdateGhostAndPreview()
    {
        if (ghostRoot == null) return;

        previewValid = false;

        if (placeCell == null)
        {
            ApplyGhostTint(false);
            SetConfirmInteractable(false);
            return;
        }

        Vector2 posA = ScreenToAnchored(WorldToScreen(placeCell.transform.position));
        ghostImgA.rectTransform.anchoredPosition = posA;

        if (activeKind == HandItemKind.Wall || activeKind == HandItemKind.Combo)
        {
            ghostImgA.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotation * 60f);

            if (placement != null)
            {
                var keyNo = new Vector2Int(999999, 999999);
                bool ok = placement.CanPreviewPlaceWall(placeCell.q, placeCell.r, activeWallType, rotation, false, keyNo, out int _mask, out bool ext);
                previewValid = ok && ext;
            }

            if (ghostImgB != null) ghostImgB.enabled = false;
        }
        else if (activeKind == HandItemKind.WallPair)
        {
            int dirToB = Mod6(pairBaseDir + rotation);
            var d = AxialDirs[dirToB];

            int qB = placeCell.q + d.x;
            int rB = placeCell.r + d.y;

            // BLOCK: нельзя ставить любую часть пары на клетку замка (0,0)
if ((placeCell.q == 0 && placeCell.r == 0) || (qB == 0 && rB == 0))
{
    previewValid = false;
    ApplyGhostTint(false);
    SetConfirmInteractable(false);

    if (ghostImgB != null) ghostImgB.enabled = (ghostImgB.sprite != null);
    return;
}


            Vector2 posB = posA + ApproxHexUiOffset(dirToB) * (ghostSize.x * 0.85f);

            if (placement != null && placement.TryGetCell(qB, rB, out var cellB))
                posB = ScreenToAnchored(WorldToScreen(cellB.transform.position));

            ghostImgB.rectTransform.anchoredPosition = posB;

            int rotA = Mod6(rotation + pairRotOffsetA);
            int rotB = Mod6(rotation + pairRotOffsetB);

            ghostImgA.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotA * 60f);
            ghostImgB.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotB * 60f);

            if (placement != null && placement.TryGetCell(qB, rB, out var _))
            {
                var keyA = new Vector2Int(placeCell.q, placeCell.r);
                var keyB = new Vector2Int(qB, rB);

                bool okA = placement.CanPreviewPlaceWall(placeCell.q, placeCell.r, pairA, rotA, false, keyB, out int maskA, out bool extA);
                bool okB = placement.CanPreviewPlaceWall(qB, rB, pairB, rotB, false, keyA, out int maskB, out bool extB);

                bool mutual = okA && okB
                    && ((maskA & (1 << dirToB)) != 0)
                    && ((maskB & (1 << Opp(dirToB))) != 0);

                previewValid = okA && okB && mutual && (extA || extB);
            }

            if (ghostImgB != null) ghostImgB.enabled = (ghostImgB.sprite != null);
        }
        else
        {
            // Tower
            previewValid = true;
            if (ghostImgB != null) ghostImgB.enabled = false;
        }

        ApplyGhostTint(previewValid);
        SetConfirmInteractable(previewValid);
    }

    private void ApplyGhostTint(bool ok)
    {
        var c = ok ? Color.green : Color.red;
        c.a = ghostAlpha;

        if (ghostImgA != null) ghostImgA.color = c;
        if (ghostImgB != null && ghostImgB.enabled) ghostImgB.color = c;
    }

    private void SetConfirmInteractable(bool ok)
    {
        if (confirmButton == null) return;
        confirmButton.interactable = ok;
    }

    private static Vector2 ApproxHexUiOffset(int dir)
    {
        return dir switch
        {
            0 => new Vector2(1f, 0f),
            1 => new Vector2(0.5f, 0.8660254f),
            2 => new Vector2(-0.5f, 0.8660254f),
            3 => new Vector2(-1f, 0f),
            4 => new Vector2(-0.5f, -0.8660254f),
            _ => new Vector2(0.5f, -0.8660254f),
        };
    }

    private void DestroyGhost()
    {
        if (ghostRoot != null)
        {
            Destroy(ghostRoot.gameObject);
            ghostRoot = null;
            ghostImgA = null;
            ghostImgB = null;
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

    private static int Mod6(int v)
    {
        v %= 6;
        if (v < 0) v += 6;
        return v;
    }

    private static int Opp(int dir) => (dir + 3) % 6;

    private void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log($"[WallDrag] {msg}");
    }
    private void ApplyGhostNeutral()
{
    var c = dragNeutralColor;
    c.a = ghostAlpha;

    if (ghostImgA != null) ghostImgA.color = c;
    if (ghostImgB != null && ghostImgB.enabled) ghostImgB.color = c;
}

}
