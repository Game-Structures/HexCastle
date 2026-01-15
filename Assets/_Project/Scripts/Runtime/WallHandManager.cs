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

    // ВАЖНО: храним ДВА списка: типы и повороты (0..5) для каждого слота
    private readonly List<WallTileType> hand = new();
    private readonly List<int> handRot = new(); // 0..5

    public IReadOnlyList<WallTileType> Hand => hand;

    // оставляем как было (для HUD)
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

        while (handRot.Count < handSize) handRot.Add(0);
        while (handRot.Count > handSize) handRot.RemoveAt(handRot.Count - 1);
    }

    public int GetHandRotation(int index)
    {
        if (index < 0 || index >= handRot.Count) return 0;
        return Mod6(handRot[index]);
    }

    public void NewRoundHand()
    {
        AutoBindSlotsSceneWide();
        EnsureHandSize();

        for (int i = 0; i < handSize; i++)
        {
            hand[i] = RandomType();
            handRot[i] = Random.Range(0, 6); // 0..5
        }

        SelectedIndex = 0;
        Rotation = 0;

        UpdateUI();
        LogHandToConsole();

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
        handRot[index] = 0;
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
            int rot = (idx >= 0 && idx < handRot.Count) ? Mod6(handRot[idx]) : 0;

            if (t == WallTileType.None)
            {
                slot.ClearTile();
                continue;
            }

            slot.SetTile(t);
            slot.SetSprite(GetSprite(t));

            // ВАЖНО: всегда выставляем rotation из handRot (единый источник)
            slot.SetRotation(rot);
        }
    }

    private void LogHandToConsole()
    {
        for (int i = 0; i < handSize; i++)
        {
            var t = hand[i];
            int rot = Mod6(handRot[i]);

            int baseMask = BaseMask(t);
            int mask = RotateMask(baseMask, rot);
            string dirs = MaskToLetters(mask);

            Debug.Log($"Tile {i + 1}: {t} Direction: {dirs} Rotation: {rot}");
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

    // --- helpers (как модель TilePlacement) ---

    private static int BaseMask(WallTileType type)
    {
        // как в TilePlacement.GetBaseMask() :contentReference[oaicite:2]{index=2}
        return type switch
        {
            WallTileType.Straight    => (1 << 4) | (1 << 1),             // D–A
            WallTileType.SmallCurve  => (1 << 4) | (1 << 5),             // D–C
            WallTileType.StrongCurve => (1 << 4) | (1 << 0),             // D–B
            WallTileType.Split       => (1 << 4) | (1 << 0) | (1 << 2),  // D–B–F
            _ => 0
        };
    }

    private const int FullMask = (1 << 6) - 1;

    private static int RotateMask(int baseMask, int rot)
    {
        rot = Mod6(rot);
        int left = (baseMask << rot) & FullMask;
        int right = (baseMask >> (6 - rot)) & FullMask;
        return (left | right) & FullMask;
    }

    private static int Mod6(int v)
    {
        v %= 6;
        if (v < 0) v += 6;
        return v;
    }

    // A=NE(1), B=E(0), C=SE(5), D=SW(4), E=W(3), F=NW(2)
    private static string MaskToLetters(int mask)
    {
        var letters = new List<string>(3);

        // порядок вывода A,B,C,D,E,F
        int[] dirs = { 1, 0, 5, 4, 3, 2 };
        foreach (int d in dirs)
        {
            if ((mask & (1 << d)) == 0) continue;
            letters.Add(d switch
            {
                1 => "A",
                0 => "B",
                5 => "C",
                4 => "D",
                3 => "E",
                2 => "F",
                _ => "?"
            });
        }

        if (letters.Count == 0) return "Unknown";
        return string.Join(" - ", letters);
    }
}
