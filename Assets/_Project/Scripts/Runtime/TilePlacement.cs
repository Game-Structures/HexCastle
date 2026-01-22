using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public sealed class TilePlacement : MonoBehaviour
{
    [Header("Placed wall visuals")]
    [Tooltip("Fallback: used when Wall 3D Prefab is not set.")]
    [SerializeField] private GameObject wallPrefab;

    [Tooltip("Preferred: 3D prefab with Wall3DVisual component (Wall3D_Prefab).")]
    [SerializeField] private GameObject wall3DPrefab;

    [Header("Wall Sprites (used only for sprite fallback)")]
    [SerializeField] private Sprite straightSprite;
    [SerializeField] private Sprite smallCurveSprite;
    [SerializeField] private Sprite strongCurveSprite;
    [SerializeField] private Sprite splitSprite;

    [SerializeField] private Transform wallVisualsRoot;

    [Header("3D wall fit")]
    [SerializeField] private bool autoFit3DWallToCell = true;

    [Tooltip("Multiplier for 3D wall innerRadius. 1.00 = exact tile size, 1.05 = slightly longer.")]
    [SerializeField] private float wall3DInnerRadiusPadding = 1.00f;

    [Tooltip("Drag your Grid here (the one that has HexTileMesh / Radius).")]
    [SerializeField] private GameObject wall3DHexSource;

    [Tooltip("If Grid Radius is center->corner (usually yes), keep true. Then inner = outer*cos(30°).")]
    [SerializeField] private bool wall3DRadiusIsOuter = true;

    [SerializeField] private float wall3DManualInnerRadius = 0.50f;
    [SerializeField] private float wallMaxHP = 500f;

    [Header("Tile Materials (optional)")]
    [SerializeField] private Material tileBaseMaterial;
    [SerializeField] private Material tileGrassMaterial;

    [Header("Visual tweaks")]
    [SerializeField] private float wallVisualYOffset = 0.02f;
    [SerializeField] private int wallSortingOrder = 10;

    [SerializeField] private bool autoFitWallToCell = true;
    [SerializeField] private float wallFitPadding = 1.00f;
    [SerializeField] private float wallVisualScale = 1.35f;

    [Header("Visual orientation")]
    [Tooltip("Visual-only offset in 60° steps, to align sprite base orientation.")]
    [SerializeField] private int spriteRotationOffsetSteps = 0;

    // ---------------- Towers ----------------

    [Header("Towers – Prefabs (assign your 3D cylinders here)")]
    [SerializeField] private GameObject towerArcher3DPrefab;
    [SerializeField] private GameObject towerCannon3DPrefab;
    [SerializeField] private GameObject towerMagic3DPrefab;
    [SerializeField] private GameObject towerFlame3DPrefab;

    [Header("Towers – Placement")]
    [Tooltip("Tower diameter = outerRadius * this value. Per task = 0.5")]
    [SerializeField] private float towerDiameterByOuterRadius = 0.5f;

    [SerializeField] private float towerVisualYOffset = 0.02f;

    [Tooltip("Tower layer. Default = Ignore Raycast (2) to not break drag & camera.")]
    [SerializeField] private int towerLayer = 2;

    private Transform towerVisualsRoot;

    // Axial directions (pointy-top): 0..5
    private static readonly Vector2Int[] AxialDirs =
    {
        new Vector2Int( 1,  0),
        new Vector2Int( 1, -1),
        new Vector2Int( 0, -1),
        new Vector2Int(-1,  0),
        new Vector2Int(-1,  1),
        new Vector2Int( 0,  1),
    };

    private const int MaskBits = 6;
    private const int FullMask = (1 << MaskBits) - 1;
    private const float COS30 = 0.8660254f;

    private sealed class PlacedTile
    {
        public WallTileType type;
        public int rotation;
        public int mask;
        public GameObject visual;
    }

    private readonly Dictionary<Vector2Int, HexCellView> cells = new();
    private readonly Dictionary<Vector2Int, PlacedTile> placed = new();

    private readonly Dictionary<Vector2Int, GameObject> placedTowers = new();

    private void OnValidate() => FixSpriteAssignmentsIfSwapped();

    private void Awake()
    {
        FixSpriteAssignmentsIfSwapped();
        RebuildCellCache();
        EnsureTowerRoot();
    }

    private void EnsureTowerRoot()
    {
        if (towerVisualsRoot != null) return;

        var rootGo = GameObject.Find("_TowerVisualsRoot");
        if (rootGo == null) rootGo = new GameObject("_TowerVisualsRoot");
        towerVisualsRoot = rootGo.transform;
        towerVisualsRoot.localScale = Vector3.one;
    }

    private void FixSpriteAssignmentsIfSwapped()
    {
        if (smallCurveSprite == null || strongCurveSprite == null) return;

        string smallName = smallCurveSprite.name.ToLowerInvariant();
        string strongName = strongCurveSprite.name.ToLowerInvariant();

        bool smallLooksStrong = smallName.Contains("strong");
        bool strongLooksSmall = strongName.Contains("small");

        if (smallLooksStrong || strongLooksSmall)
        {
            (smallCurveSprite, strongCurveSprite) = (strongCurveSprite, smallCurveSprite);
            Debug.LogWarning("[TilePlacement] Swapped smallCurveSprite <-> strongCurveSprite based on sprite names. Check TilePlacement inspector.");
        }
    }

    public void RebuildCellCache()
    {
        cells.Clear();
        var all = FindObjectsByType<HexCellView>(FindObjectsSortMode.None);
        foreach (var c in all)
        {
            var key = new Vector2Int(c.q, c.r);
            cells[key] = c;

            if (tileBaseMaterial != null && c.rend != null)
                c.rend.sharedMaterial = tileBaseMaterial;
        }
    }

    public bool IsOccupied(int q, int r)
    {
        if (cells.Count == 0) RebuildCellCache();
        return placed.ContainsKey(new Vector2Int(q, r));
    }

    // ---------------- Enclosure helpers ----------------

    /// <summary>Returns true if there is a placed wall tile at (q,r) and outputs its 6-bit wall mask.</summary>
    public bool TryGetWallMask(int q, int r, out int mask)
    {
        var key = new Vector2Int(q, r);
        if (placed.TryGetValue(key, out var t) && t != null)
        {
            mask = t.mask;
            return true;
        }
        mask = 0;
        return false;
    }

    /// <summary>True if there is a wall segment on cell edge 'dir' (0..5) for the placed wall tile at (q,r).</summary>
    public bool HasWallSegment(int q, int r, int dir)
    {
        if (dir < 0 || dir > 5) return false;
        return TryGetWallMask(q, r, out int m) && (m & (1 << dir)) != 0;
    }

    public bool HasTowerAt(int q, int r)
    {
        var key = new Vector2Int(q, r);
        return placedTowers.TryGetValue(key, out var t) && t != null;
    }

    private bool IsCellBuildable(Vector2Int key)
    {
        if (!cells.TryGetValue(key, out var cell) || cell == null)
            return false;

        var tag = cell.GetComponent<HexCastle.Map.MapTerrainTag>();
        if (tag == null) return true;

        return tag.type == HexCastle.Map.MapTerrainType.Normal;
    }

    public bool TryPlaceWall(int q, int r, Vector3 worldPos, WallTileType type, int rotation, bool allowReplace)
    {
        rotation = Mod6(rotation);
        var key = new Vector2Int(q, r);

        if (cells.Count == 0) RebuildCellCache();
        if (!cells.ContainsKey(key)) return false;

        if (!IsCellBuildable(key)) return false;

        int baseMask = GetBaseMask(type);
        if (baseMask == 0) return false;

        int mask = RotateMask(baseMask, rotation);

        if (!allowReplace && placed.ContainsKey(key))
            return false;

        if (!HasAnyConnection(key, mask))
            return false;

        if (placed.TryGetValue(key, out var old))
        {
            if (!allowReplace) return false;

            RemoveTowerAtInternal(key);

            DestroyVisual(old);
            placed.Remove(key);
        }

        var tile = new PlacedTile
        {
            type = type,
            rotation = rotation,
            mask = mask,
            visual = null
        };

        placed[key] = tile;

        ApplyCellMaterial(key, true);
        SpawnOrUpdateVisual(key, tile);

        Debug.Log($"[TilePlacement] Placed {type} rot={rotation} at q={q}, r={r}");
        return true;
    }

    private bool HasAnyConnection(Vector2Int key, int mask)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            if ((mask & (1 << dir)) == 0) continue;

            var nKey = key + AxialDirs[dir];

            if (placed.TryGetValue(nKey, out var n))
            {
                if ((n.mask & (1 << Opp(dir))) != 0)
                    return true;
            }

            if (nKey.x == 0 && nKey.y == 0)
                return true;
        }
        return false;
    }

    private void ApplyCellMaterial(Vector2Int key, bool hasWall)
    {
        if (!cells.TryGetValue(key, out var cell)) return;
        if (cell.rend == null) return;

        if (hasWall)
        {
            if (tileGrassMaterial != null) cell.rend.sharedMaterial = tileGrassMaterial;
        }
        else
        {
            if (tileBaseMaterial != null) cell.rend.sharedMaterial = tileBaseMaterial;
        }
    }

    private void SpawnOrUpdateVisual(Vector2Int key, PlacedTile tile)
    {
        var cell = cells[key];

        if (wallVisualsRoot == null)
        {
            var rootGo = GameObject.Find("_WallVisualsRoot");
            if (rootGo == null) rootGo = new GameObject("_WallVisualsRoot");
            wallVisualsRoot = rootGo.transform;
            wallVisualsRoot.localScale = Vector3.one;
        }

        if (wall3DPrefab != null)
        {
            var vis3D = tile.visual != null ? tile.visual.GetComponent<Wall3DVisual>() : null;
            if (tile.visual == null || vis3D == null)
            {
                if (tile.visual != null) Destroy(tile.visual);

                tile.visual = Instantiate(wall3DPrefab, wallVisualsRoot);
                tile.visual.name = $"Wall3D_{tile.type}";
                vis3D = tile.visual.GetComponent<Wall3DVisual>();
            }

            if (vis3D == null)
            {
                Debug.LogError("[TilePlacement] wall3DPrefab must have Wall3DVisual component.");
                return;
            }

            tile.visual.transform.SetParent(wallVisualsRoot, true);
            tile.visual.transform.position = cell.transform.position + Vector3.up * wallVisualYOffset;
            tile.visual.transform.rotation = Quaternion.identity;
            tile.visual.transform.localScale = Vector3.one;

            float innerR = ComputeWall3DInnerRadius(cell);

            int baseMask = GetBaseMask(tile.type);
            vis3D.Build(baseMask, tile.rotation, innerR);

            // стена всегда IgnoreRaycast
            SetLayerRecursively(tile.visual, 2);

            // отключаем коллайдеры детей, кроме TowerSlot
            var allCols = tile.visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < allCols.Length; i++)
            {
                var col = allCols[i];
                if (col == null) continue;

                // корневой BoxCollider должен остаться
                if (col.transform == tile.visual.transform) continue;

                // TowerSlot collider НЕ выключаем
                if (col.GetComponent<TowerSlot>() != null) continue;

                col.enabled = false;
            }

            var capOld = tile.visual.GetComponent<CapsuleCollider>();
            if (capOld != null) capOld.enabled = false;

            var box = tile.visual.GetComponent<BoxCollider>();
            if (box == null) box = tile.visual.AddComponent<BoxCollider>();

            box.isTrigger = false;

            Bounds b = new Bounds(tile.visual.transform.position, Vector3.zero);
            var rs = tile.visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
                b.Encapsulate(rs[i].bounds);

            Vector3 localCenter = tile.visual.transform.InverseTransformPoint(b.center);
            Vector3 localSize = tile.visual.transform.InverseTransformVector(b.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

            box.center = localCenter;
            box.size = new Vector3(localSize.x + 0.05f, Mathf.Max(localSize.y + 0.2f, 1.0f), localSize.z + 0.05f);
            box.enabled = true;

            var link = tile.visual.GetComponent<WallTileLink>();
            if (link == null) link = tile.visual.AddComponent<WallTileLink>();
            link.Init(this, key.x, key.y, wallMaxHP);
            // --- NEW: setup TowerSlot (runtime link + suggested diameter like real towers) ---
var slot = tile.visual.GetComponentInChildren<TowerSlot>(true);
if (slot != null)
{
    bool enabledForWall = IsWallTypeAllowedForTower(tile.type);

    // outerR = innerR / cos(30)
    float outerR = innerR / COS30;
    float suggestedDia = outerR * Mathf.Clamp(towerDiameterByOuterRadius, 0.05f, 1.5f);

    slot.Setup(this, key.x, key.y, enabledForWall);
    slot.SetSuggestedDiameter(suggestedDia);
    slot.RefreshVisual();
}


            // NEW: настроим TowerSlot и вернем ему Default layer, если он есть
            SetupTowerSlot(tile.visual, key, tile.type);

            return;
        }

        // Sprite fallback
        if (wallPrefab == null) return;

        var spr = GetSprite(tile.type);
        if (spr == null) return;

        if (tile.visual == null)
        {
            tile.visual = Instantiate(wallPrefab, wallVisualsRoot);
            tile.visual.name = $"WallTile_{tile.type}";
        }

        tile.visual.transform.SetParent(wallVisualsRoot, true);
        tile.visual.transform.position = cell.transform.position + Vector3.up * wallVisualYOffset;

        int visSteps = Mod6(tile.rotation + spriteRotationOffsetSteps);
        tile.visual.transform.rotation = Quaternion.Euler(90f, -visSteps * 60f, 0f);

        var sr = tile.visual.GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null) sr = tile.visual.AddComponent<SpriteRenderer>();

        sr.sprite = spr;
        sr.sortingOrder = wallSortingOrder;
        sr.enabled = true;

        var img = tile.visual.GetComponentInChildren<Image>(true);
        if (img != null) img.enabled = false;

        float scale = wallVisualScale;

        if (autoFitWallToCell && cell.rend != null)
        {
            var cellSize = cell.rend.bounds.size;
            var sprSize = spr.bounds.size;
            if (sprSize.x > 0.0001f && sprSize.y > 0.0001f)
            {
                float sx = (cellSize.x * wallFitPadding) / sprSize.x;
                float sz = (cellSize.z * wallFitPadding) / sprSize.y;
                scale = Mathf.Min(sx, sz) * wallVisualScale;
            }
        }

        tile.visual.transform.localScale = Vector3.one * scale;
    }

    private void SetupTowerSlot(GameObject wallVisual, Vector2Int key, WallTileType wallType)
    {
        if (wallVisual == null) return;

        bool enabledForThisWall = IsWallTypeAllowedForTower(wallType);

        var slot = wallVisual.GetComponentInChildren<TowerSlot>(true);
        if (slot == null) return;

        slot.Setup(this, key.x, key.y, enabledForThisWall);

        // слот должен быть кликабельным – возвращаем Default layer только ему и детям
        SetLayerRecursively(slot.gameObject, 0);

        // на всякий случай включим его коллайдер
        var col = slot.GetComponent<Collider>();
        if (col != null) col.enabled = enabledForThisWall;
    }

    private float ComputeWall3DInnerRadius(HexCellView cell)
    {
        float innerR = wall3DManualInnerRadius;
        if (!autoFit3DWallToCell) return Mathf.Max(0.05f, innerR);

        if (TryComputeInnerRadiusFromCellSpacing(cell, out float fromSpacing))
        {
            innerR = fromSpacing;
        }
        else if (cell != null && cell.rend != null)
        {
            var ext = cell.rend.bounds.extents;
            innerR = Mathf.Min(ext.x, ext.z);
        }

        float k = Mathf.Clamp(wall3DInnerRadiusPadding, 0.5f, 2.0f);
        innerR *= k;

        return Mathf.Max(0.05f, innerR);
    }

    private void DestroyVisual(PlacedTile tile)
    {
        if (tile.visual != null)
            Destroy(tile.visual);
        tile.visual = null;
    }

    private static int Opp(int dir) => (dir + 3) % 6;

    private static int Mod6(int v)
    {
        v %= 6;
        if (v < 0) v += 6;
        return v;
    }

    private static int RotateMask(int baseMask, int rot)
    {
        rot = Mod6(rot);
        int left = (baseMask << rot) & FullMask;
        int right = (baseMask >> (6 - rot)) & FullMask;
        return (left | right) & FullMask;
    }

    private static int GetBaseMask(WallTileType type)
    {
        return type switch
        {
            WallTileType.Straight => (1 << 4) | (1 << 1),
            WallTileType.SmallCurve => (1 << 4) | (1 << 5),
            WallTileType.StrongCurve => (1 << 4) | (1 << 0),
            WallTileType.Split => (1 << 4) | (1 << 0) | (1 << 2),
            _ => 0
        };
    }

    private Sprite GetSprite(WallTileType t)
    {
        return t switch
        {
            WallTileType.Straight => straightSprite,
            WallTileType.SmallCurve => smallCurveSprite,
            WallTileType.StrongCurve => strongCurveSprite,
            WallTileType.Split => splitSprite,
            _ => null
        };
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }

    // ---------------- Towers API ----------------

    private bool IsWallTypeAllowedForTower(WallTileType t)
    {
        return t == WallTileType.Straight || t == WallTileType.SmallCurve || t == WallTileType.StrongCurve;
    }

    private GameObject GetTowerPrefab(TowerType type)
    {
        return type switch
        {
            TowerType.Archer => towerArcher3DPrefab,
            TowerType.Cannon => towerCannon3DPrefab,
            TowerType.Magic => towerMagic3DPrefab,
            TowerType.Flame => towerFlame3DPrefab,
            _ => null
        };
    }

    public bool TryPlaceTower(int q, int r, TowerType type, bool allowReplace)
{
    var key = new Vector2Int(q, r);

    if (cells.Count == 0) RebuildCellCache();

    if (!cells.TryGetValue(key, out var cell) || cell == null)
        return false;

    if (!placed.TryGetValue(key, out var wallTile) || wallTile == null)
        return false;

    if (!IsWallTypeAllowedForTower(wallTile.type))
        return false;

    // NEW: Cannon is locked until CannonUnlock building is built
    if (type == TowerType.Cannon)
    {
        if (BuildingEffectsManager.Instance == null || !BuildingEffectsManager.Instance.IsArtilleryUnlocked)
            return false;
    }

    var prefab = GetTowerPrefab(type);
    if (prefab == null)
        return false;

    EnsureTowerRoot();

    if (placedTowers.TryGetValue(key, out var oldTower) && oldTower != null)
    {
        if (!allowReplace) return false;
        Destroy(oldTower);
        placedTowers.Remove(key);
    }

    var tower = Instantiate(prefab, towerVisualsRoot);
    tower.name = $"Tower_{type}_{q}_{r}";

    tower.transform.position = cell.transform.position + Vector3.up * towerVisualYOffset;
    tower.transform.rotation = Quaternion.identity;

    float innerR = ComputeWall3DInnerRadius(cell);
    float outerR = innerR / COS30;

    float desiredDiameter = outerR * Mathf.Clamp(towerDiameterByOuterRadius, 0.05f, 1.5f);

    var s = tower.transform.localScale;
    tower.transform.localScale = new Vector3(desiredDiameter, s.y, desiredDiameter);

    SetLayerRecursively(tower, towerLayer);

    var cols = tower.GetComponentsInChildren<Collider>(true);
    for (int i = 0; i < cols.Length; i++)
        cols[i].enabled = false;

    placedTowers[key] = tower;

    float spacing = 1f;
    TryComputeCellCenterSpacing(cell, out spacing);

    var shooter = tower.GetComponent<TowerShooter>();
    if (shooter == null) shooter = tower.AddComponent<TowerShooter>();
    shooter.Init(type, spacing);

    // NEW: обновить SlotVisual (серую башню) на этой стене
    if (wallTile.visual != null)
    {
        var slot = wallTile.visual.GetComponentInChildren<TowerSlot>(true);
        if (slot != null) slot.RefreshVisual();
    }

    return true;
}



    private void RemoveTowerAtInternal(Vector2Int key)
    {
        if (!placedTowers.TryGetValue(key, out var tower) || tower == null)
        {
            placedTowers.Remove(key);
            return;
        }

        Destroy(tower);
        placedTowers.Remove(key);
    }

    public bool RemoveWallAt(int q, int r)
    {
        var key = new Vector2Int(q, r);

        RemoveTowerAtInternal(key);

        if (!placed.TryGetValue(key, out var tile))
            return false;

        if (tile.visual != null)
            Destroy(tile.visual);

        placed.Remove(key);
        ApplyCellMaterial(key, false);

        return true;
    }

    private bool TryComputeInnerRadiusFromCellSpacing(HexCellView cell, out float innerR)
    {
        innerR = 0f;
        if (cell == null) return false;

        var p = cell.transform.position;
        var p2 = new Vector2(p.x, p.z);

        for (int dir = 0; dir < 6; dir++)
        {
            var nk = new Vector2Int(cell.q, cell.r) + AxialDirs[dir];
            if (!cells.TryGetValue(nk, out var nCell) || nCell == null) continue;

            var np = nCell.transform.position;
            var np2 = new Vector2(np.x, np.z);

            float d = Vector2.Distance(p2, np2);
            if (d > 0.0001f)
            {
                innerR = d * 0.5f;
                return true;
            }
        }

        return false;
    }

    private bool TryComputeCellCenterSpacing(HexCellView cell, out float spacing)
    {
        spacing = 0f;
        if (cell == null) return false;

        Vector3 p = cell.transform.position;
        Vector2 p2 = new Vector2(p.x, p.z);

        for (int dir = 0; dir < 6; dir++)
        {
            var nk = new Vector2Int(cell.q, cell.r) + AxialDirs[dir];
            if (!cells.TryGetValue(nk, out var nCell) || nCell == null) continue;

            Vector3 np = nCell.transform.position;
            Vector2 np2 = new Vector2(np.x, np.z);

            float d = Vector2.Distance(p2, np2);
            if (d > 0.0001f)
            {
                spacing = d;
                return true;
            }
        }

        return false;
    }
    // NEW: API для WallPair / UI
public bool TryGetCell(int q, int r, out HexCellView cell)
{
    var key = new Vector2Int(q, r);
    if (cells.Count == 0) RebuildCellCache();

    if (cells.TryGetValue(key, out cell) && cell != null)
        return true;

    cell = null;
    return false;
}

// Проверка "можно ли вообще поставить сюда" без требования подключения.
// externalConnection = есть ли подключение к существующим стенам (или центру), НЕ считая excludeKey.
// NEW: Preview check for single + WallPair
// externalConnection = есть ли подключение к существующим стенам/центру, НЕ считая excludeKey.
public bool CanPreviewPlaceWall(int q, int r, WallTileType type, int rotation, bool allowReplace, Vector2Int excludeKey, out int mask, out bool externalConnection)
{
    rotation = Mod6(rotation);
    mask = 0;
    externalConnection = false;

    if (cells.Count == 0) RebuildCellCache();

    var key = new Vector2Int(q, r);
    if (!cells.ContainsKey(key)) return false;
    if (!IsCellBuildable(key)) return false;

    int baseMask = GetBaseMask(type);
    if (baseMask == 0) return false;

    mask = RotateMask(baseMask, rotation);

    if (!allowReplace && placed.ContainsKey(key))
        return false;

    for (int dir = 0; dir < 6; dir++)
    {
        if ((mask & (1 << dir)) == 0) continue;

        var nKey = key + AxialDirs[dir];

        if (nKey == excludeKey)
            continue;

        if (placed.TryGetValue(nKey, out var n))
        {
            if ((n.mask & (1 << Opp(dir))) != 0)
            {
                externalConnection = true;
                break;
            }
        }

        if (nKey.x == 0 && nKey.y == 0)
        {
            externalConnection = true;
            break;
        }
    }

    return true;
}


}
