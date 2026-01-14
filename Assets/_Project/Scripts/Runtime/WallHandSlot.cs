// WallHandSlot.cs
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
    private bool hasTile;
    private WallTileType tileType = WallTileType.None;

    public bool HasTile => hasTile;
    public WallTileType TileType => tileType;
    public TMP_Text Label => label;

    private void Awake()
    {
        if (drag == null) drag = FindFirstObjectByType<WallDragController>();

        // Auto-bind label/icon if not assigned
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
            if (icon == null)
            {
                var imgs = GetComponentsInChildren<Image>(true);
                if (imgs != null && imgs.Length > 0)
                {
                    foreach (var im in imgs)
                        if (im != null && im.gameObject.name == "Icon") { icon = im; break; }

                    if (icon == null)
                    {
                        foreach (var im in imgs)
                            if (im != null && im.transform != transform) { icon = im; break; }
                    }
                }
            }
        }

        // Auto slotIndex from object name: TileSlot0/1/2
        if (name.StartsWith("TileSlot"))
        {
            if (int.TryParse(name.Replace("TileSlot", ""), out int idx))
                slotIndex = idx;
        }

        if (icon == null)
            Debug.LogWarning($"[WallHandSlot] Icon Image is not bound on {name} (slotIndex={slotIndex}).");
        if (label == null)
            Debug.LogWarning($"[WallHandSlot] Label TMP_Text is not bound on {name} (slotIndex={slotIndex}).");
    }

    public void SetTile(WallTileType t, Sprite s)
    {
        tileType = t;
        hasTile = (t != WallTileType.None);

        if (label != null) label.text = "";
        SetSprite(s);
    }

    public void SetTile(WallTileType t)
    {
        tileType = t;
        hasTile = (t != WallTileType.None);

        if (label != null) label.text = "";
        // sprite intentionally not touched here
    }

    public void ClearTile()
    {
        hasTile = false;
        tileType = WallTileType.None;

        if (label != null) label.text = "";
        SetSprite(null);
    }

    public void SetSprite(Sprite s)
    {
        if (icon == null) return;
        icon.sprite = s;
        icon.enabled = (s != null);
    }

    public Sprite GetSprite()
    {
        return icon != null ? icon.sprite : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!hasTile) return;
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
