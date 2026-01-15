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

    // ЕДИНЫЙ источник rotation для этого слота (0..5)
    [SerializeField] private int rotationSteps;

    public bool HasTile => hasTile;
    public WallTileType TileType => tileType;
    public TMP_Text Label => label;

    public int GetRotation() => rotationSteps;

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

        if (name.StartsWith("TileSlot"))
        {
            if (int.TryParse(name.Replace("TileSlot", ""), out int idx))
                slotIndex = idx;
        }
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
    }

    public void ClearTile()
    {
        hasTile = false;
        tileType = WallTileType.None;
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

    public Sprite GetSprite()
    {
        return icon != null ? icon.sprite : null;
    }

    public void SetRotation(int rotSteps)
    {
        rotationSteps = Mod6(rotSteps);
        ApplyIconRotation();
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
