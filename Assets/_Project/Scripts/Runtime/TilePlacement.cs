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

    [Tooltip("Fallback: if no Grid source, compute from cell renderer bounds as outer radius.")]
    [SerializeField] private bool wall3DBoundsAreOuterRadius = true;

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

    // ---------------- Towers (Step 2) ----------------

    [Header("Towers – Prefabs (assign your 3D cylinders here)")]
    [SerializeField] private GameObject towerArcher3DPrefab;
    [SerializeField] private GameObject towerArtillery3DPrefab;
    [SerializeField] private GameObject towerMagic3DPrefab;
    [SerializeField] private GameObject towerFlame3DPrefab;

    [Header("Towers – Placement")]
    [Tooltip("Tower diameter = outerRadius * this value. Per task = 0.5")]
    [SerializeField] private float towerDiameterByOuterRadius = 0.5f;

    [SerializeField] private float towerVisualYOffset = 0.02f;

    [Tooltip("Tower layer. Default = Ignore Raycast (2) to not break drag & camera.")]
    [SerializeField] private int towerLayer = 2;

    [Header("Towers – Debug spawn on Start (temporary for Step 2 check)")]
    [SerializeField] private bool debugSpawnTowerOnStart = false;
    [SerializeField] private TowerType debugTowerType = TowerType.Archer;
    [SerializeField] private int debugTowerQ = 0;
    [SerializeField] private int debugTowerR = 0;

    private Transform towerVisualsRoot;

    // Axial directions (pointy-top): 0..5
    // 0: E, 1: NE, 2: NW, 3: W, 4: SW, 5: SE
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
        public int rotation;      // 0..5
        public int mask;          // 6-bit (already rotated) – used for logic/neighbor checks
        public GameObject visual; // instance
    }

    private readonly Dictionary<Vector2Int, HexCellView> cells = new();
private readonly Dictionary<Vector2Int, PlacedTile> placed = new();

// Towers placed per cell
private readonly Dictionary<Vector2Int, GameObject> placedTowers = new();

// Step 2.3 – remembers last placed wall cell (for quick tower debug)
private Vector2Int lastPlacedWallKey = new Vector2Int(int.MinValue, int.MinValue);


    private void OnValidate()
    {
        FixSpriteAssignmentsIfSwapped();
    }

    private void Awake()
    {
        FixSpriteAssignmentsIfSwapped();
        RebuildCellCache();
        EnsureTowerRoot();
    }

    private void Start()
    {
        if (debugSpawnTowerOnStart)
        {
            // For Step 2 verification: place a wall first, then enable this flag and press Play.
            TryPlaceTower(debugTowerQ, debugTowerR, debugTowerType, true);
        }
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
        if (cells.Count == 0)
            RebuildCellCache();
        return placed.ContainsKey(new Vector2Int(q, r));
    }
private bool IsCellBuildable(Vector2Int key)
{
    if (!cells.TryGetValue(key, out var cell) || cell == null)
        return false;

    // Если тега нет – считаем обычной клеткой (Normal)
    var tag = cell.GetComponent<HexCastle.Map.MapTerrainTag>();
    if (tag == null) return true;

    return tag.type == HexCastle.Map.MapTerrainType.Normal;
}

    public bool TryPlaceWall(int q, int r, Vector3 worldPos, WallTileType type, int rotation, bool allowReplace)
{
    rotation = Mod6(rotation);
    var key = new Vector2Int(q, r);

    if (cells.Count == 0)
        RebuildCellCache();

    if (!cells.ContainsKey(key))
        return false;

    // NEW: запрет строительства на воде/лесу/горах
    if (!IsCellBuildable(key))
    {
        var cell = cells[key];
        var tag = cell != null ? cell.GetComponent<HexCastle.Map.MapTerrainTag>() : null;
        if (tag != null)
            Debug.Log($"[TilePlacement] Denied: terrain={tag.type} at q={q}, r={r}");
        return false;
    }

    int baseMask = GetBaseMask(type);
    if (baseMask == 0)
        return false;

    int mask = RotateMask(baseMask, rotation);

    if (!allowReplace && placed.ContainsKey(key))
        return false;

    if (!HasAnyConnection(key, mask))
        return false;

    if (placed.TryGetValue(key, out var old))
    {
        if (!allowReplace) return false;

        // If wall is being replaced – remove tower too (safe & consistent)
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

    lastPlacedWallKey = key;

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

        // --- 3D ---
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

            // строим как в старом тесте: baseMask + rotationSteps
            int baseMask = GetBaseMask(tile.type);
            vis3D.Build(baseMask, tile.rotation, innerR);

            SetLayerRecursively(tile.visual, 2); // Ignore Raycast

            var allCols = tile.visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < allCols.Length; i++)
            {
                if (allCols[i].transform != tile.visual.transform)
                    allCols[i].enabled = false;
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

            return;
        }

        // --- Sprite fallback ---
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

    private float ComputeWall3DInnerRadius(HexCellView cell)
    {
        float innerR = wall3DManualInnerRadius;

        if (!autoFit3DWallToCell)
            return Mathf.Max(0.05f, innerR);

        // 1) Самый точный способ – из расстояния между центрами клеток
        if (TryComputeInnerRadiusFromCellSpacing(cell, out float fromSpacing))
        {
            innerR = fromSpacing;
        }
        else
        {
            // 2) Фолбэк – как было (Grid Radius через reflection)
            if (TryGetHexOuterRadiusFromSource(wall3DHexSource, out float R))
            {
                innerR = wall3DRadiusIsOuter ? R * COS30 : R;
            }
            else if (cell != null && cell.rend != null)
            {
                // 3) Последний фолбэк – bounds клетки
                var ext = cell.rend.bounds.extents;
                innerR = Mathf.Min(ext.x, ext.z);
            }
        }

        float k = Mathf.Clamp(wall3DInnerRadiusPadding, 0.5f, 2.0f);
        innerR *= k;

        return Mathf.Max(0.05f, innerR);
    }

    private bool TryGetHexOuterRadiusFromSource(GameObject go, out float outerR)
    {
        outerR = -1f;
        if (go == null) return false;

        float radius = -1f;

        var monos = go.GetComponents<MonoBehaviour>();
        for (int i = 0; i < monos.Length; i++)
        {
            var mb = monos[i];
            if (mb == null) continue;

            var t = mb.GetType();
            if (!t.Name.Contains("HexTileMesh")) continue;

            var f = t.GetField("Radius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                 ?? t.GetField("radius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (f != null && f.FieldType == typeof(float))
            {
                radius = (float)f.GetValue(mb);
                break;
            }

            var p = t.GetProperty("Radius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                 ?? t.GetProperty("radius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (p != null && p.PropertyType == typeof(float))
            {
                radius = (float)p.GetValue(mb);
                break;
            }
        }

        if (radius <= 0f) return false;

        // Radius обычно хранится в ЛОКАЛЬНЫХ юнитах Grid – переводим в WORLD
        var s = go.transform.lossyScale;
        float scaleXZ = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));

        outerR = radius * scaleXZ;
        return outerR > 0f;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
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
        // A=NE(1), B=E(0), C=SE(5), D=SW(4), E=W(3), F=NW(2)
        // SmallCurve: D–C (4–5)
        // Split: D–B–F (4–0–2)
        // Straight: D–A (4–1)
        // StrongCurve: D–B (4–0)
        return type switch
        {
            WallTileType.Straight    => (1 << 4) | (1 << 1),
            WallTileType.SmallCurve  => (1 << 4) | (1 << 5),
            WallTileType.StrongCurve => (1 << 4) | (1 << 0),
            WallTileType.Split       => (1 << 4) | (1 << 0) | (1 << 2),
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

    // ---------------- Towers API (Step 2) ----------------

    private bool IsWallTypeAllowedForTower(WallTileType t)
    {
        return t == WallTileType.Straight || t == WallTileType.SmallCurve || t == WallTileType.StrongCurve;
    }

    private GameObject GetTowerPrefab(TowerType type)
    {
        return type switch
        {
            TowerType.Archer => towerArcher3DPrefab,
            TowerType.Artillery => towerArtillery3DPrefab,
            TowerType.Magic => towerMagic3DPrefab,
            TowerType.Flame => towerFlame3DPrefab,
            _ => null
        };
    }

    public bool TryPlaceTower(int q, int r, TowerType type, bool allowReplace)
    {
        var key = new Vector2Int(q, r);

        if (cells.Count == 0)
            RebuildCellCache();

        if (!cells.TryGetValue(key, out var cell) || cell == null)
            return false;

        if (!placed.TryGetValue(key, out var wallTile) || wallTile == null)
        {
            Debug.Log($"[Tower] Denied: no wall at q={q} r={r}");
            return false;
        }

        if (!IsWallTypeAllowedForTower(wallTile.type))
        {
            Debug.Log($"[Tower] Denied: wall type {wallTile.type} is not allowed for tower at q={q} r={r}");
            return false;
        }

        var prefab = GetTowerPrefab(type);
        if (prefab == null)
        {
            Debug.LogWarning($"[Tower] Prefab not assigned for {type}. Assign it in TilePlacement inspector.");
            return false;
        }

        EnsureTowerRoot();

        if (placedTowers.TryGetValue(key, out var oldTower) && oldTower != null)
        {
            if (!allowReplace) return false;
            Destroy(oldTower);
            placedTowers.Remove(key);
        }

        var tower = Instantiate(prefab, towerVisualsRoot);
        tower.name = $"Tower_{type}_{q}_{r}";

        // position
        tower.transform.position = cell.transform.position + Vector3.up * towerVisualYOffset;
        tower.transform.rotation = Quaternion.identity;

        // scale by cell radius:
        // innerR = apothem; outerR = innerR / cos(30)
        float innerR = ComputeWall3DInnerRadius(cell);
        float outerR = innerR / COS30;

        float desiredDiameter = outerR * Mathf.Clamp(towerDiameterByOuterRadius, 0.05f, 1.5f);

        // Unity Cylinder primitive diameter at scale 1 = 1.0 (radius 0.5), so XZ scale = desiredDiameter
        var s = tower.transform.localScale;
        tower.transform.localScale = new Vector3(desiredDiameter, s.y, desiredDiameter);

        // do not break raycasts
        SetLayerRecursively(tower, towerLayer);

        // disable colliders (safe for now)
        var cols = tower.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;

        placedTowers[key] = tower;

// вычислим расстояние между центрами соседних клеток (world units) – это будет 1 "тайл"
float spacing = 1f;
if (TryComputeCellCenterSpacing(cell, out float spacingTmp))
    spacing = spacingTmp;

var shooter = tower.GetComponent<TowerShooter>();
if (shooter == null) shooter = tower.AddComponent<TowerShooter>();
shooter.Init(type, spacing);

Debug.Log($"[Tower] Placed {type} at q={q} r={r} diameter={desiredDiameter:0.###} (outerR={outerR:0.###}) spacing={spacing:0.###}");
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

    // ---------------------------------------------------

    public bool RemoveWallAt(int q, int r)
    {
        var key = new Vector2Int(q, r);

        // IMPORTANT: if wall is destroyed – remove tower too
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

        // innerRadius = (distance between centers of соседних клеток) / 2
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
    private void Update()
    {
    if (Input.GetKeyDown(KeyCode.T))
    {
        if (lastPlacedWallKey.x == int.MinValue)
        {
            Debug.Log("[Tower] No last placed wall yet – place a wall first.");
            return;
        }

        TryPlaceTower(lastPlacedWallKey.x, lastPlacedWallKey.y, debugTowerType, true);
    }
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


}
