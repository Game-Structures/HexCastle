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
    public Image icon;

    private WallDragController drag;

    [Header("Data")]
    [SerializeField] private HandItemKind kind = HandItemKind.None;
    [SerializeField] private WallTileType tileType = WallTileType.None;   // for Wall / Combo
    [SerializeField] private TowerType towerType = TowerType.Archer;      // for Tower / Combo

    // rotation for Wall / Combo (0..5)
    [SerializeField] private int rotationSteps;

    // Compatibility
    public bool HasTile => kind != HandItemKind.None;
    public WallTileType TileType => tileType;

    public bool HasItem => HasTile;
    public WallTileType WallType => tileType;
    public void ClearItem() => ClearTile();

    // New
    public HandItemKind Kind => kind;
    public TowerType TowerType => towerType;

    public int GetRotation() => rotationSteps;
    public Sprite GetSprite() => icon != null ? icon.sprite : null;

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

        if (name.StartsWith("TileSlot"))
        {
            if (int.TryParse(name.Replace("TileSlot", ""), out int idx))
                slotIndex = idx;
        }

        ApplyIconRotation();
    }

    // --- Walls ---
    public void SetTile(WallTileType t)
    {
        kind = (t == WallTileType.None) ? HandItemKind.None : HandItemKind.Wall;
        tileType = t;
        if (label != null) label.text = "";
        ApplyIconRotation();
    }

    public void SetTile(WallTileType t, Sprite s)
    {
        SetTile(t);
        SetSprite(s);
    }

    public void SetWall(WallTileType t, int rotSteps, Sprite s)
    {
        SetTile(t, s);
        SetRotation(rotSteps);
    }

    public void SetRotation(int rotSteps)
    {
        rotationSteps = Mod6(rotSteps);
        ApplyIconRotation();
    }

    // --- Towers ---
    public void SetTower(TowerType t, Sprite s)
    {
        kind = HandItemKind.Tower;
        towerType = t;
        tileType = WallTileType.None;
        rotationSteps = 0;

        if (label != null) label.text = "";
        SetSprite(s);
        ApplyIconRotation();
    }

    // --- Combo (Wall + Tower) ---
    public void SetCombo(WallTileType wall, TowerType tower, int rotSteps, Sprite s)
    {
        kind = HandItemKind.Combo;
        tileType = wall;
        towerType = tower;
        rotationSteps = Mod6(rotSteps);

        if (label != null) label.text = "";
        SetSprite(s);
        ApplyIconRotation();
    }

    public void ClearTile()
    {
        kind = HandItemKind.None;
        tileType = WallTileType.None;
        towerType = TowerType.Archer;
        rotationSteps = 0;

        if (label != null) label.text = "";
        SetSprite(null);
        ApplyIconRotation();
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
