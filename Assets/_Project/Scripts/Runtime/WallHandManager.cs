// WallHandManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class WallHandManager : MonoBehaviour
{
    [Header("Hand")]
    [SerializeField] private int handSize = 3;

    [Header("Economy")]
    [SerializeField] private int refreshCost = 20;

    [Header("Slots (optional)")]
    [Tooltip("Можно не назначать – будет найдено в сцене по WallHandSlot.")]
    [SerializeField] private WallHandSlot[] slots;

    [Header("Sprites")]
    [SerializeField] private Sprite spriteStraight;
    [SerializeField] private Sprite spriteSmallCurve;
    [SerializeField] private Sprite spriteStrongCurve;
    [SerializeField] private Sprite spriteSplit;
[SerializeField] private WaveController wave;
    private readonly List<WallTileType> hand = new();

    public IReadOnlyList<WallTileType> Hand => hand;
    public int SelectedIndex { get; private set; } = 0;
    public int Rotation { get; private set; } = 0;

    private bool refreshButtonBound;

    private void Awake()
    {
        AutoBindSlotsSceneWide();
        EnsureHandSize();
        UpdateUI();
        AutoBindRefreshButton();
    }

    private void AutoBindSlotsSceneWide()
    {
        if (slots != null && slots.Length > 0) return;

        slots = FindObjectsByType<WallHandSlot>(FindObjectsSortMode.None);

        // Ensure slotIndex is sane even if Awake order differs
        if (slots != null)
        {
            foreach (var s in slots)
            {
                if (s == null) continue;
                if (s.name.StartsWith("TileSlot"))
                {
                    if (int.TryParse(s.name.Replace("TileSlot", ""), out int idx))
                        s.slotIndex = idx;
                }
            }

            System.Array.Sort(slots, (a, b) => a.slotIndex.CompareTo(b.slotIndex));
        }

        Debug.Log($"[WallHandManager] Slots bound: {(slots == null ? 0 : slots.Length)}");
    }

private void AutoBindRefreshButton()
{
    if (wave == null) wave = FindFirstObjectByType<WaveController>();

    
    if (refreshButtonBound) return;

    var go = GameObject.Find("RefreshButton");
    if (go == null) return;

    var btn = go.GetComponent<Button>();
    if (btn == null) return;

    // Если кнопка уже настроена в инспекторе – НЕ добавляем ещё один listener
    if (btn.onClick.GetPersistentEventCount() > 0)
    {
        refreshButtonBound = true;
        Debug.Log("[WallHandManager] RefreshButton already has persistent OnClick binding – skipping auto-bind.");
        return;
    }

    btn.onClick.AddListener(RefreshHand);
    refreshButtonBound = true;
    Debug.Log("[WallHandManager] RefreshButton auto-bound to RefreshHand().");
}


    private void EnsureHandSize()
    {
        if (handSize < 1) handSize = 1;

        while (hand.Count < handSize) hand.Add(WallTileType.None);
        while (hand.Count > handSize) hand.RemoveAt(hand.Count - 1);
    }

    public void NewRoundHand()
    {
        AutoBindSlotsSceneWide();
        EnsureHandSize();

        for (int i = 0; i < handSize; i++)
            hand[i] = RandomType();

        SelectedIndex = 0;
        Rotation = 0;

        UpdateUI();
        Debug.Log("[WallHandManager] NewRoundHand dealt.");
    }

    public void RefreshHand()
    {
       if (wave != null && wave.CurrentPhase != WaveController.Phase.Build)
{
    Debug.Log("[WallHandManager] Refresh blocked: COMBAT phase");
    return;
}

        
        Debug.Log("[WallHandManager] RefreshHand clicked.");

        if (!GoldBank.TrySpend(refreshCost))
        {
            Debug.Log($"[WallHandManager] Refresh denied. Need={refreshCost}, have={GoldBank.Gold}");
            return;
        }

        NewRoundHand();
        Debug.Log($"[WallHandManager] Refresh OK (-{refreshCost}). Gold left={GoldBank.Gold}");
    }

    public void SetSelected(int index)
    {
        if (index < 0 || index >= handSize) return;
        SelectedIndex = index;
        Rotation = 0;
    }

    public void RotateSelected()
    {
        Rotation = (Rotation + 1) % 6;
    }

    public bool TryGetSlotType(int index, out WallTileType type)
    {
        type = WallTileType.None;
        if (index < 0 || index >= hand.Count) return false;
        type = hand[index];
        return type != WallTileType.None;
    }

    public void ConsumeSlot(int index)
    {
        if (index < 0 || index >= hand.Count) return;
        hand[index] = WallTileType.None;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (slots == null || slots.Length == 0)
        {
            Debug.LogWarning("[WallHandManager] UpdateUI skipped: no slots bound.");
            return;
        }

        EnsureHandSize();

        foreach (var slot in slots)
        {
            if (slot == null) continue;

            int idx = slot.slotIndex;
            WallTileType t = (idx >= 0 && idx < hand.Count) ? hand[idx] : WallTileType.None;

            if (t == WallTileType.None)
            {
                slot.ClearTile();
                continue;
            }

            slot.SetTile(t);
            slot.SetSprite(GetSprite(t));
        }
    }

    private WallTileType RandomType()
    {
        int v = Random.Range(0, 4);
        return v switch
        {
            0 => WallTileType.Straight,
            1 => WallTileType.SmallCurve,
            2 => WallTileType.StrongCurve,
            _ => WallTileType.Split
        };
    }

    public Sprite GetSprite(WallTileType type)
    {
        return type switch
        {
            WallTileType.Straight => spriteStraight,
            WallTileType.SmallCurve => spriteSmallCurve,
            WallTileType.StrongCurve => spriteStrongCurve,
            WallTileType.Split => spriteSplit,
            _ => null
        };
    }
}
