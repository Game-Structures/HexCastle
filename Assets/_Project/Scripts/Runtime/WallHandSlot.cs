using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WallHandSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Slot")]
    public int slotIndex;

    [Header("UI")]
    public TMP_Text label;
    public Image icon;      // основной значок
    public Image iconB;     // второй значок для пары (создается автоматически)

    private WallDragController drag;

    [Header("Data")]
    [SerializeField] private HandItemKind kind = HandItemKind.None;

    // Single / Combo
    [SerializeField] private WallTileType tileType = WallTileType.None;
    [SerializeField] private TowerType towerType = TowerType.Archer;

    // rotation for Wall / Combo / WallPair (0..5)
    [SerializeField] private int rotationSteps;

    // WallPair data
    [Header("WallPair")]
    [SerializeField] private WallTileType pairTypeB = WallTileType.None;
    [SerializeField] private int pairBaseDir;     // 0..5
    [SerializeField] private int pairRotOffsetA;  // 0..5
    [SerializeField] private int pairRotOffsetB;  // 0..5

    [SerializeField] private Sprite ghostSpriteA;
    [SerializeField] private Sprite ghostSpriteB;

    // Compatibility
    public bool HasTile => kind != HandItemKind.None;
    public WallTileType TileType => tileType;

    public bool HasItem => HasTile;
    public WallTileType WallType => tileType;
    public void ClearItem() => ClearTile();

    public HandItemKind Kind => kind;
    public TowerType TowerType => towerType;

    public int GetRotation() => rotationSteps;

    // WallPair getters
    public bool IsWallPair => kind == HandItemKind.WallPair;
    public WallTileType PairTypeA => tileType;
    public WallTileType PairTypeB => pairTypeB;
    public int PairBaseDir => Mod6(pairBaseDir);
    public int PairRotOffsetA => Mod6(pairRotOffsetA);
    public int PairRotOffsetB => Mod6(pairRotOffsetB);

    public Sprite GetGhostSpriteA() => ghostSpriteA != null ? ghostSpriteA : (icon != null ? icon.sprite : null);
    public Sprite GetGhostSpriteB() => ghostSpriteB != null ? ghostSpriteB : null;

    private void Awake()
    {
        if (drag == null) drag = FindFirstObjectByType<WallDragController>();

        if (label == null)
        {
            var tmp = transform.Find("Text (TMP)");
            if (tmp != null) label = tmp.GetComponent<TMP_Text>();
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        }

        if (icon == null)
        {
            var ico = transform.Find("Icon");
            if (ico != null) icon = ico.GetComponent<Image>();
            if (icon == null) icon = GetComponentInChildren<Image>(true);
        }

        EnsureIconB();

        if (name.StartsWith("TileSlot"))
        {
            if (int.TryParse(name.Replace("TileSlot", ""), out int idx))
                slotIndex = idx;
        }

        ApplyIconRotation();
        UpdatePairVisual(false);
    }

    private void EnsureIconB()
    {
        if (icon == null) return;
        if (iconB != null) return;

        var t = transform.Find("IconB");
        if (t != null)
        {
            iconB = t.GetComponent<Image>();
            return;
        }

        var go = new GameObject("IconB", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(icon.transform.parent, false);

        iconB = go.GetComponent<Image>();
        iconB.preserveAspect = true;

        var rt = iconB.rectTransform;
        rt.anchorMin = icon.rectTransform.anchorMin;
        rt.anchorMax = icon.rectTransform.anchorMax;
        rt.pivot = icon.rectTransform.pivot;

        // позицию/размер выставим в UpdatePairVisual
        iconB.enabled = false;
    }

    private void UpdatePairVisual(bool isPair)
    {
        if (iconB == null || icon == null) return;

        if (!isPair)
        {
            iconB.enabled = false;
            iconB.sprite = null;
            return;
        }

        // Показать второй гекс поверх, сместив вправо
        iconB.enabled = (iconB.sprite != null);

        var main = icon.rectTransform;
        var rt = iconB.rectTransform;

        rt.sizeDelta = main.sizeDelta * 0.80f;
        rt.localScale = Vector3.one;

        // смещение в локальных координатах
        float dx = (main.sizeDelta.x * 0.32f);
        float dy = (main.sizeDelta.y * -0.05f);
        rt.anchoredPosition = main.anchoredPosition + new Vector2(dx, dy);

        // чтобы второй был “сверху”
        iconB.transform.SetAsLastSibling();
    }

    // --- Walls ---
    public void SetTile(WallTileType t)
    {
        kind = (t == WallTileType.None) ? HandItemKind.None : HandItemKind.Wall;
        tileType = t;

        pairTypeB = WallTileType.None;
        pairBaseDir = 0;
        pairRotOffsetA = 0;
        pairRotOffsetB = 0;

        ghostSpriteA = null;
        ghostSpriteB = null;

        if (label != null) label.text = "";
        ApplyIconRotation();

        UpdatePairVisual(false);
    }

    public void SetTile(WallTileType t, Sprite s)
    {
        SetTile(t);
        SetSprite(s);
        ghostSpriteA = s;
    }

    public void SetRotation(int rotSteps)
    {
        rotationSteps = Mod6(rotSteps);
        ApplyIconRotation();
    }

    // --- WallPair ---
    public void SetWallPair(
        WallTileType a, WallTileType b,
        int baseDir,
        int rotSteps,
        int rotOffsetA,
        int rotOffsetB,
        Sprite iconA,
        Sprite iconB_,
        Sprite ghostA,
        Sprite ghostB_)
    {
        kind = HandItemKind.WallPair;

        tileType = a;
        pairTypeB = b;

        pairBaseDir = Mod6(baseDir);
        pairRotOffsetA = Mod6(rotOffsetA);
        pairRotOffsetB = Mod6(rotOffsetB);

        rotationSteps = Mod6(rotSteps);

        if (label != null) label.text = "";

        EnsureIconB();

        // В слоте показываем 2 иконки (A и B)
        SetSprite(iconA);

        if (iconB != null)
        {
            iconB.sprite = iconB_;
            iconB.enabled = (iconB_ != null);
        }

        // Ghost – реальные спрайты стен
        ghostSpriteA = ghostA != null ? ghostA : iconA;
        ghostSpriteB = ghostB_ != null ? ghostB_ : iconB_;

        ApplyIconRotation();
        UpdatePairVisual(true);
    }

    // --- Towers ---
    public void SetTower(TowerType t, Sprite s)
    {
        kind = HandItemKind.Tower;
        towerType = t;

        tileType = WallTileType.None;

        pairTypeB = WallTileType.None;
        pairBaseDir = 0;
        pairRotOffsetA = 0;
        pairRotOffsetB = 0;

        rotationSteps = 0;

        ghostSpriteA = s;
        ghostSpriteB = null;

        if (label != null) label.text = "";
        SetSprite(s);
        ApplyIconRotation();

        UpdatePairVisual(false);
    }

    // --- Combo (Wall + Tower) ---
    public void SetCombo(WallTileType wall, TowerType tower, int rotSteps, Sprite s)
    {
        kind = HandItemKind.Combo;
        tileType = wall;
        towerType = tower;
        rotationSteps = Mod6(rotSteps);

        pairTypeB = WallTileType.None;
        pairBaseDir = 0;
        pairRotOffsetA = 0;
        pairRotOffsetB = 0;

        ghostSpriteA = s;
        ghostSpriteB = null;

        if (label != null) label.text = "";
        SetSprite(s);
        ApplyIconRotation();

        UpdatePairVisual(false);
    }

    public void ClearTile()
    {
        kind = HandItemKind.None;

        tileType = WallTileType.None;
        towerType = TowerType.Archer;

        pairTypeB = WallTileType.None;
        pairBaseDir = 0;
        pairRotOffsetA = 0;
        pairRotOffsetB = 0;

        rotationSteps = 0;

        ghostSpriteA = null;
        ghostSpriteB = null;

        if (label != null) label.text = "";
        SetSprite(null);

        if (iconB != null)
        {
            iconB.sprite = null;
            iconB.enabled = false;
        }

        ApplyIconRotation();
        UpdatePairVisual(false);
    }

    public void SetSprite(Sprite s)
    {
        if (icon == null) return;
        icon.sprite = s;
        icon.enabled = (s != null);
    }

    private void ApplyIconRotation()
    {
        if (icon == null) return;

        icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotationSteps * 60f);

        // второй значок тоже крутим так же (достаточно для понятности “это поворачивается”)
        if (iconB != null)
            iconB.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotationSteps * 60f);
    }

    private static int Mod6(int v)
    {
        v %= 6;
        if (v < 0) v += 6;
        return v;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!HasTile) return;
        if (drag == null) drag = FindFirstObjectByType<WallDragController>();
        if (drag == null) return;

        drag.BeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (drag == null) return;
        drag.Drag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (drag == null) return;
        drag.EndDrag(eventData);
    }
}
