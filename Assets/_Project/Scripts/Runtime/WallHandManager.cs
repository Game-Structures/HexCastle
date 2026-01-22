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

    [Header("WallPair – Preview Sprites (optional)")]
    [Tooltip("Можно оставить пустым – тогда в слоте будет спрайт A, а ghost всё равно будет из 2-х стен.")]
    [SerializeField] private Sprite pairStraightStrongCurvePreview;
    [SerializeField] private Sprite pairStraightSplitPreview;
    [SerializeField] private Sprite pairStrongCurveSplitLeftPreview;
    [SerializeField] private Sprite pairStrongCurveSplitRightPreview;
    [SerializeField] private Sprite pairStrongCurveStrongCurveZigzagPreview;
    [SerializeField] private Sprite pairStrongCurveStrongCurveCrescentPreview;

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

    [Range(0f, 1f)]
    [SerializeField] private float wallPairChance = 0.10f; // NEW

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

    // WallPair data
    private readonly List<WallTileType> handPairB = new();
    private readonly List<int> handPairBaseDir = new();
    private readonly List<int> handPairRotA = new();
    private readonly List<int> handPairRotB = new();

    private bool refreshButtonBound;

    private struct WallPairDef
    {
        public WallTileType a;
        public WallTileType b;
        public int baseDir;     // 0..5 (A->B)
        public int rotA;        // offset
        public int rotB;        // offset
        public Sprite preview;  // optional
    }

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

        while (handPairB.Count < handSize) handPairB.Add(WallTileType.None);
        while (handPairB.Count > handSize) handPairB.RemoveAt(handPairB.Count - 1);

        while (handPairBaseDir.Count < handSize) handPairBaseDir.Add(0);
        while (handPairBaseDir.Count > handSize) handPairBaseDir.RemoveAt(handPairBaseDir.Count - 1);

        while (handPairRotA.Count < handSize) handPairRotA.Add(0);
        while (handPairRotA.Count > handSize) handPairRotA.RemoveAt(handPairRotA.Count - 1);

        while (handPairRotB.Count < handSize) handPairRotB.Add(0);
        while (handPairRotB.Count > handSize) handPairRotB.RemoveAt(handPairRotB.Count - 1);
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
        float p = Mathf.Clamp01(wallPairChance);

        // чтобы сумма не улетела в 1
        if (c + t + p > 0.95f)
        {
            float s = c + t + p;
            float k = 0.95f / Mathf.Max(0.0001f, s);
            c *= k;
            t *= k;
            p *= k;
        }

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

                handPairB[i] = WallTileType.None;
                handPairBaseDir[i] = 0;
                handPairRotA[i] = 0;
                handPairRotB[i] = 0;
            }
            else if (roll < c + t)
            {
                // Tower
                handKind[i] = HandItemKind.Tower;
                hand[i] = WallTileType.None;
                handTower[i] = RandomTowerType();
                handRot[i] = 0;

                handPairB[i] = WallTileType.None;
                handPairBaseDir[i] = 0;
                handPairRotA[i] = 0;
                handPairRotB[i] = 0;
            }
            else if (roll < c + t + p)
            {
                // NEW: WallPair
                var def = RandomWallPair();

                handKind[i] = HandItemKind.WallPair;
                hand[i] = def.a;
                handTower[i] = TowerType.Archer;

                handPairB[i] = def.b;
                handPairBaseDir[i] = Mod6(def.baseDir);
                handPairRotA[i] = Mod6(def.rotA);
                handPairRotB[i] = Mod6(def.rotB);

                handRot[i] = Random.Range(0, 6);
            }
            else
            {
                // Wall
                handKind[i] = HandItemKind.Wall;
                hand[i] = RandomWallType();
                handTower[i] = TowerType.Archer;
                handRot[i] = Random.Range(0, 6);

                handPairB[i] = WallTileType.None;
                handPairBaseDir[i] = 0;
                handPairRotA[i] = 0;
                handPairRotB[i] = 0;
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

        handPairB[index] = WallTileType.None;
        handPairBaseDir[index] = 0;
        handPairRotA[index] = 0;
        handPairRotB[index] = 0;

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
            else if (k == HandItemKind.Combo)
            {
                var wt = hand[idx];
                var tt = handTower[idx];
                int rot = Mod6(handRot[idx]);

                slot.SetCombo(wt, tt, rot, GetComboSprite(wt, tt));
            }
            else // WallPair
{
    var a = hand[idx];
    var b = handPairB[idx];

    int baseDir = Mod6(handPairBaseDir[idx]);
    int rotSteps = Mod6(handRot[idx]);

    int rotA = Mod6(handPairRotA[idx]);
    int rotB = Mod6(handPairRotB[idx]);

    // preview-иконка для слота (может быть null)
    Sprite preview = GetWallPairPreview(a, b, baseDir, rotA, rotB);
    if (preview == null) preview = GetWallSprite(a);

    // В слоте рисуем 2 иконки: A и B
    // ghost = реальные спрайты стен (A и B)
    slot.SetWallPair(
        a, b,
        baseDir,
        rotSteps,
        rotA, rotB,
        GetWallSprite(a),   // iconA
        GetWallSprite(b),   // iconB
        GetWallSprite(a),   // ghostA
        GetWallSprite(b)    // ghostB_
    );

    // Если хочешь показывать именно preview-иконку (единый спрайт пары) как iconA:
    // замени GetWallSprite(a) выше на preview.
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
            TowerType.Cannon => spriteTowerArtillery,
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
                TowerType.Cannon => comboStraightArtillery,
                TowerType.Flame => comboStraightFlame,
                _ => comboStraightArcher
            },
            WallTileType.SmallCurve => t switch
            {
                TowerType.Archer => comboSmallCurveArcher,
                TowerType.Magic => comboSmallCurveMagic,
                TowerType.Cannon => comboSmallCurveArtillery,
                TowerType.Flame => comboSmallCurveFlame,
                _ => comboSmallCurveArcher
            },
            WallTileType.StrongCurve => t switch
            {
                TowerType.Archer => comboStrongCurveArcher,
                TowerType.Magic => comboStrongCurveMagic,
                TowerType.Cannon => comboStrongCurveArtillery,
                TowerType.Flame => comboStrongCurveFlame,
                _ => comboStrongCurveArcher
            },
            _ => comboStraightArcher
        };
    }

    // --- WallPair variants, как ты перечислил ---
    private WallPairDef RandomWallPair()
    {
        // Список вариантов:
        // • Straight + StrongCurve (2)
        // • Straight + Split (1)
        // • StrongCurve + Split (2: baseDir=1 и baseDir=5)
        // • StrongCurve + StrongCurve (2: zigzag и crescent)

        int pick = Random.Range(0, 7);

        // Общая идея: B всегда сосед A по baseDir, а у обоих есть сегмент на общей грани
        // (чтобы второй тайл мог соединиться с первым).
        return pick switch
        {
            // Straight + StrongCurve (turn one way)
            0 => new WallPairDef
            {
                a = WallTileType.Straight,
                b = WallTileType.StrongCurve,
                baseDir = 0,
                rotA = 2,   // Straight имеет сегмент на dir0 и dir3
                rotB = 5,   // StrongCurve имеет сегмент на dir3 и ещё один на dir5
                preview = pairStraightStrongCurvePreview
            },

            // Straight + StrongCurve (turn other way)
            1 => new WallPairDef
            {
                a = WallTileType.Straight,
                b = WallTileType.StrongCurve,
                baseDir = 0,
                rotA = 2,
                rotB = 3,   // StrongCurve имеет сегмент на dir3 и ещё один на dir1
                preview = pairStraightStrongCurvePreview
            },

            // Straight + Split (single)
            2 => new WallPairDef
            {
                a = WallTileType.Straight,
                b = WallTileType.Split,
                baseDir = 0,
                rotA = 2,   // соединение по общей грани
                rotB = 3,   // Split имеет сегмент на dir3 + разветвления
                preview = pairStraightSplitPreview
            },

            // StrongCurve + Split (shift left: baseDir=1)
            3 => new WallPairDef
            {
                a = WallTileType.StrongCurve,
                b = WallTileType.Split,
                baseDir = 1,
                rotA = 1,   // StrongCurve включает dir1
                rotB = 0,   // Split включает dir4 (opposite of dir1)
                preview = pairStrongCurveSplitLeftPreview
            },

            // StrongCurve + Split (shift right: baseDir=5)
            4 => new WallPairDef
            {
                a = WallTileType.StrongCurve,
                b = WallTileType.Split,
                baseDir = 5,
                rotA = 5,   // StrongCurve включает dir5
                rotB = 0,   // Split включает dir2 (opposite of dir5)
                preview = pairStrongCurveSplitRightPreview
            },

            // StrongCurve + StrongCurve (zigzag)
            5 => new WallPairDef
            {
                a = WallTileType.StrongCurve,
                b = WallTileType.StrongCurve,
                baseDir = 0,
                rotA = 0,   // A имеет сегмент на dir0
                rotB = 5,   // B имеет сегмент на dir3 и "уходит" вниз (зигзаг)
                preview = pairStrongCurveStrongCurveZigzagPreview
            },

            // StrongCurve + StrongCurve (crescent)
            _ => new WallPairDef
            {
                a = WallTileType.StrongCurve,
                b = WallTileType.StrongCurve,
                baseDir = 0,
                rotA = 0,   // A имеет сегмент на dir0
                rotB = 3,   // B имеет сегмент на dir3 и "уходит" вверх (полумесяц)
                preview = pairStrongCurveStrongCurveCrescentPreview
            }
        };
    }

    private Sprite GetWallPairPreview(WallTileType a, WallTileType b, int baseDir, int rotA, int rotB)
    {
        // Тут можно сделать более умный матчинг под разные preview-спрайты.
        // Пока используем заранее заданные ссылки (могут быть null).
        if (a == WallTileType.Straight && b == WallTileType.StrongCurve) return pairStraightStrongCurvePreview;
        if (a == WallTileType.Straight && b == WallTileType.Split) return pairStraightSplitPreview;

        if (a == WallTileType.StrongCurve && b == WallTileType.Split)
        {
            if (baseDir == 1) return pairStrongCurveSplitLeftPreview;
            if (baseDir == 5) return pairStrongCurveSplitRightPreview;
        }

        if (a == WallTileType.StrongCurve && b == WallTileType.StrongCurve)
        {
            if (rotB == 5) return pairStrongCurveStrongCurveZigzagPreview;
            if (rotB == 3) return pairStrongCurveStrongCurveCrescentPreview;
        }

        return null;
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
