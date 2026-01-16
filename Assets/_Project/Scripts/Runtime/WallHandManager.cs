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
    [Tooltip("Можно не назначать – будет найдено в сцене по TileSlot0/1/2.")]
    [SerializeField] private WallHandSlot[] slots;

    [Header("Walls – Sprites")]
    [SerializeField] private Sprite spriteStraight;
    [SerializeField] private Sprite spriteSmallCurve;
    [SerializeField] private Sprite spriteStrongCurve;
    [SerializeField] private Sprite spriteSplit;

    [Header("Towers – Sprites")]
    [SerializeField] private Sprite spriteTowerArcher;
    [SerializeField] private Sprite spriteTowerMagic;
    [SerializeField] private Sprite spriteTowerArtillery;
    [SerializeField] private Sprite spriteTowerFlame;

    [Header("Combo – Sprites (12 pcs)")]
    [SerializeField] private Sprite comboStraightArcher;
    [SerializeField] private Sprite comboStraightMagic;
    [SerializeField] private Sprite comboStraightArtillery;
    [SerializeField] private Sprite comboStraightFlame;

    [SerializeField] private Sprite comboSmallCurveArcher;
    [SerializeField] private Sprite comboSmallCurveMagic;
    [SerializeField] private Sprite comboSmallCurveArtillery;
    [SerializeField] private Sprite comboSmallCurveFlame;

    [SerializeField] private Sprite comboStrongCurveArcher;
    [SerializeField] private Sprite comboStrongCurveMagic;
    [SerializeField] private Sprite comboStrongCurveArtillery;
    [SerializeField] private Sprite comboStrongCurveFlame;

    [Header("Mixing")]
    [Range(0f, 1f)]
    [SerializeField] private float towerChance = 0.01f;

    [Range(0f, 1f)]
    [SerializeField] private float comboChance = 0.01f;

    [SerializeField] private WaveController wave;

    // For HUDController compatibility
    private readonly List<WallTileType> hand = new();
    private readonly List<int> handRot = new();

    public IReadOnlyList<WallTileType> Hand => hand;
    public int SelectedIndex { get; private set; } = 0;
    public int Rotation { get; private set; } = 0;

    // New
    private readonly List<HandItemKind> handKind = new();
    private readonly List<TowerType> handTower = new();

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

        var found = FindObjectsByType<WallHandSlot>(FindObjectsSortMode.None);
        var list = new List<WallHandSlot>();

        foreach (var s in found)
        {
            if (s == null) continue;
            if (!s.name.StartsWith("TileSlot")) continue;

            if (int.TryParse(s.name.Replace("TileSlot", ""), out int idx))
                s.slotIndex = idx;

            list.Add(s);
        }

        list.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
        slots = list.ToArray();
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
            return;
        }

        btn.onClick.AddListener(RefreshHand);
        refreshButtonBound = true;
    }

    private void EnsureHandSize()
    {
        if (handSize < 1) handSize = 1;

        while (hand.Count < handSize) hand.Add(WallTileType.None);
        while (hand.Count > handSize) hand.RemoveAt(hand.Count - 1);

        while (handRot.Count < handSize) handRot.Add(0);
        while (handRot.Count > handSize) handRot.RemoveAt(handRot.Count - 1);

        while (handKind.Count < handSize) handKind.Add(HandItemKind.None);
        while (handKind.Count > handSize) handKind.RemoveAt(handKind.Count - 1);

        while (handTower.Count < handSize) handTower.Add(TowerType.Archer);
        while (handTower.Count > handSize) handTower.RemoveAt(handTower.Count - 1);
    }

    public int GetHandRotation(int index)
    {
        if (index < 0 || index >= handRot.Count) return 0;
        return Mod6(handRot[index]);
    }

    public HandItemKind GetKind(int index)
    {
        if (index < 0 || index >= handKind.Count) return HandItemKind.None;
        return handKind[index];
    }

    public TowerType GetTowerType(int index)
    {
        if (index < 0 || index >= handTower.Count) return TowerType.Archer;
        return handTower[index];
    }

    public void NewRoundHand()
    {
        AutoBindSlotsSceneWide();
        EnsureHandSize();

        float c = Mathf.Clamp01(comboChance);
        float t = Mathf.Clamp01(towerChance);
        if (c + t > 0.95f) t = 0.95f - c;

        for (int i = 0; i < handSize; i++)
        {
            float roll = Random.value;

            if (roll < c)
            {
                // Combo: only Straight/SmallCurve/StrongCurve
                handKind[i] = HandItemKind.Combo;
                hand[i] = RandomWallTypeForCombo();
                handTower[i] = RandomTowerType();
                handRot[i] = Random.Range(0, 6);
            }
            else if (roll < c + t)
            {
                // Tower
                handKind[i] = HandItemKind.Tower;
                hand[i] = WallTileType.None;
                handTower[i] = RandomTowerType();
                handRot[i] = 0;
            }
            else
            {
                // Wall
                handKind[i] = HandItemKind.Wall;
                hand[i] = RandomWallType();
                handTower[i] = TowerType.Archer;
                handRot[i] = Random.Range(0, 6);
            }
        }

        SelectedIndex = 0;
        Rotation = 0;

        UpdateUI();
    }

    public void RefreshHand()
    {
        if (wave != null && wave.CurrentPhase != WaveController.Phase.Build)
            return;

        if (!GoldBank.TrySpend(refreshCost))
            return;

        NewRoundHand();
    }

    public void ConsumeSlot(int index)
    {
        if (index < 0 || index >= handSize) return;

        handKind[index] = HandItemKind.None;
        hand[index] = WallTileType.None;
        handRot[index] = 0;
        handTower[index] = TowerType.Archer;

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (slots == null || slots.Length == 0) return;

        EnsureHandSize();

        foreach (var slot in slots)
        {
            if (slot == null) continue;

            int idx = slot.slotIndex;
            if (idx < 0 || idx >= handSize)
            {
                slot.ClearTile();
                continue;
            }

            var k = handKind[idx];
            if (k == HandItemKind.None)
            {
                slot.ClearTile();
                continue;
            }

            if (k == HandItemKind.Wall)
            {
                var wt = hand[idx];
                int rot = Mod6(handRot[idx]);

                slot.SetTile(wt);
                slot.SetSprite(GetWallSprite(wt));
                slot.SetRotation(rot);
            }
            else if (k == HandItemKind.Tower)
            {
                var tt = handTower[idx];
                slot.SetTower(tt, GetTowerSprite(tt));
            }
            else // Combo
            {
                var wt = hand[idx];
                var tt = handTower[idx];
                int rot = Mod6(handRot[idx]);

                slot.SetCombo(wt, tt, rot, GetComboSprite(wt, tt));
            }
        }
    }

    private Sprite GetWallSprite(WallTileType type)
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

    private Sprite GetTowerSprite(TowerType t)
    {
        return t switch
        {
            TowerType.Archer => spriteTowerArcher,
            TowerType.Magic => spriteTowerMagic,
            TowerType.Artillery => spriteTowerArtillery,
            TowerType.Flame => spriteTowerFlame,
            _ => spriteTowerArcher
        };
    }

    private Sprite GetComboSprite(WallTileType w, TowerType t)
    {
        return w switch
        {
            WallTileType.Straight => t switch
            {
                TowerType.Archer => comboStraightArcher,
                TowerType.Magic => comboStraightMagic,
                TowerType.Artillery => comboStraightArtillery,
                TowerType.Flame => comboStraightFlame,
                _ => comboStraightArcher
            },
            WallTileType.SmallCurve => t switch
            {
                TowerType.Archer => comboSmallCurveArcher,
                TowerType.Magic => comboSmallCurveMagic,
                TowerType.Artillery => comboSmallCurveArtillery,
                TowerType.Flame => comboSmallCurveFlame,
                _ => comboSmallCurveArcher
            },
            WallTileType.StrongCurve => t switch
            {
                TowerType.Archer => comboStrongCurveArcher,
                TowerType.Magic => comboStrongCurveMagic,
                TowerType.Artillery => comboStrongCurveArtillery,
                TowerType.Flame => comboStrongCurveFlame,
                _ => comboStrongCurveArcher
            },
            _ => comboStraightArcher
        };
    }

    private static WallTileType RandomWallType()
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

    private static WallTileType RandomWallTypeForCombo()
    {
        int v = Random.Range(0, 3);
        return v switch
        {
            0 => WallTileType.Straight,
            1 => WallTileType.SmallCurve,
            _ => WallTileType.StrongCurve
        };
    }

    private static TowerType RandomTowerType()
    {
        int v = Random.Range(0, 4);
        return (TowerType)v;
    }

    private static int Mod6(int v)
    {
        v %= 6;
        if (v < 0) v += 6;
        return v;
    }
}
