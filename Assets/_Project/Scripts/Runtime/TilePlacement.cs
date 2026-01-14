using System.Collections.Generic;
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

    [Tooltip("Scale factor for auto-fit inner radius. 1.0 = exact from tile bounds, 0.98 = tiny inset.")]
    [SerializeField] private float wall3DInnerRadiusPadding = 1.00f;

    [SerializeField] private float wall3DManualInnerRadius = 0.50f;
    [SerializeField] private float wallMaxHP = 500f;

    [Header("Tile Materials (optional)")]
    [SerializeField] private Material tileBaseMaterial;
    [SerializeField] private Material tileGrassMaterial;

    [Header("Visual tweaks")]
    [SerializeField] private float wallVisualYOffset = 0.02f;
    [SerializeField] private int wallSortingOrder = 10;

    [SerializeField] private bool autoFitWallToCell = true;
    [SerializeField] private float wallFitPadding = 1.00f; // 1.0 = максимально “в размер”
    [SerializeField] private float wallVisualScale = 1.35f; // используется если autoFitWallToCell=false

    [Header("Visual orientation")]
    [Tooltip("Visual-only offset in 60° steps, to align sprite base orientation.")]
    [SerializeField] private int spriteRotationOffsetSteps = 0;

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

    private sealed class PlacedTile
    {
        public WallTileType type;
        public int rotation;      // 0..5
        public int mask;          // 6-bit (already rotated)
        public GameObject visual; // instance
    }

    private readonly Dictionary<Vector2Int, HexCellView> cells = new();
    private readonly Dictionary<Vector2Int, PlacedTile> placed = new();

    private void OnValidate()
    {
        FixSpriteAssignmentsIfSwapped();
    }

    private void Awake()
    {
        FixSpriteAssignmentsIfSwapped();
        RebuildCellCache();
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

    public bool TryPlaceWall(int q, int r, Vector3 worldPos, WallTileType type, int rotation, bool allowReplace)
    {
        rotation = Mod6(rotation);
        var key = new Vector2Int(q, r);

        if (cells.Count == 0)
            RebuildCellCache();

        if (!cells.ContainsKey(key))
            return false;

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

            // соединение со стеной
            if (placed.TryGetValue(nKey, out var n))
            {
                if ((n.mask & (1 << Opp(dir))) != 0)
                    return true;
            }

            // соединение с замком: центр (0,0)
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

        // Root без неравномерного scale, чтобы поворот/масштаб не "тянуло"
        if (wallVisualsRoot == null)
        {
            var rootGo = GameObject.Find("_WallVisualsRoot");
            if (rootGo == null) rootGo = new GameObject("_WallVisualsRoot");
            wallVisualsRoot = rootGo.transform;
            wallVisualsRoot.localScale = Vector3.one;
        }

        // --- 3D ветка (если задан префаб) ---
        if (wall3DPrefab != null)
        {
            // если раньше стоял спрайтовый visual – заменяем на 3D
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

            float innerR = wall3DManualInnerRadius;

if (autoFit3DWallToCell)
{
    // 1) Самый точный способ – по расстоянию до соседа (гарантирует “до края тайла”)
    if (!TryComputeInnerRadiusFromNeighbors(key, out innerR))
    {
        // 2) Фолбэк – по bounds, если вдруг нет соседей (например, одиночная клетка)
        if (cell.rend != null)
            innerR = ComputeInnerRadiusFromBounds(cell.rend.bounds);
    }

    float k = Mathf.Clamp(wall3DInnerRadiusPadding, 0.5f, 1.5f);
    innerR *= k;
}


            innerR = Mathf.Max(0.05f, innerR);

            // tile.mask уже повернут в мировую ориентацию (RotateMask(baseMask, rotation))
            // поэтому rotationSteps=0
            vis3D.Build(tile.mask, 0, innerR);

            // 3D-стены: не мешаем кликам по клеткам
            SetLayerRecursively(tile.visual, 2); // Ignore Raycast

            // 1) Отключаем коллайдеры на визуальных кубах
            var allCols = tile.visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < allCols.Length; i++)
            {
                if (allCols[i].transform != tile.visual.transform)
                    allCols[i].enabled = false;
            }

            // 2) Ставим BoxCollider по реальным габаритам (render bounds)
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

            // небольшой запас
            box.size = new Vector3(localSize.x + 0.05f, Mathf.Max(localSize.y + 0.2f, 1.0f), localSize.z + 0.05f);
            box.enabled = true;

            // 3) Линк стены к клетке + HP
            var link = tile.visual.GetComponent<WallTileLink>();
            if (link == null) link = tile.visual.AddComponent<WallTileLink>();
            link.Init(this, key.x, key.y, wallMaxHP);

            return;
        }

        // --- Спрайтовый fallback (как было) ---
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
        float zDeg = -visSteps * 60f;

        Quaternion inPlane = Quaternion.Euler(0f, 0f, zDeg);
        Quaternion tiltToGround = Quaternion.Euler(90f, 0f, 0f);
        tile.visual.transform.rotation = tiltToGround * inPlane;

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
            var cellSize = cell.rend.bounds.size; // world X/Z
            var sprSize = spr.bounds.size;        // sprite X/Y
            if (sprSize.x > 0.0001f && sprSize.y > 0.0001f)
            {
                float sx = (cellSize.x * wallFitPadding) / sprSize.x;
                float sz = (cellSize.z * wallFitPadding) / sprSize.y;
                scale = Mathf.Min(sx, sz) * wallVisualScale;
            }
        }

        tile.visual.transform.localScale = Vector3.one * scale;
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

    private bool MatchesNeighbors(Vector2Int key, int mask)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            var nKey = key + AxialDirs[dir];
            if (!placed.TryGetValue(nKey, out var n)) continue;

            bool a = (mask & (1 << dir)) != 0;
            bool b = (n.mask & (1 << Opp(dir))) != 0;

            if (a != b) return false;
        }
        return true;
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
        // Base sprite orientations:
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

    public bool RemoveWallAt(int q, int r)
    {
        var key = new Vector2Int(q, r);

        if (!placed.TryGetValue(key, out var tile))
            return false;

        if (tile.visual != null)
            Destroy(tile.visual);

        placed.Remove(key);
        ApplyCellMaterial(key, false);
        return true;
    }

    // ======= FIT HELPERS =======

    private static float ComputeInnerRadiusFromBounds(Bounds b)
    {
        float sizeX = b.size.x;
        float sizeZ = b.size.z;

        const float SQRT3 = 1.7320508f;
        const float COS30 = 0.8660254f;

        // pointy-top: Z = 2R, X = sqrt3*R
        float R_pointy_fromZ = sizeZ * 0.5f;
        float R_pointy_fromX = sizeX / SQRT3;
        float errPointy = Mathf.Abs(R_pointy_fromZ - R_pointy_fromX);
        float R_pointy = (R_pointy_fromZ + R_pointy_fromX) * 0.5f;

        // flat-top: X = 2R, Z = sqrt3*R
        float R_flat_fromX = sizeX * 0.5f;
        float R_flat_fromZ = sizeZ / SQRT3;
        float errFlat = Mathf.Abs(R_flat_fromX - R_flat_fromZ);
        float R_flat = (R_flat_fromX + R_flat_fromZ) * 0.5f;

        float R = (errPointy <= errFlat) ? R_pointy : R_flat;

        float innerR = R * COS30; // апофема
        return (innerR <= 0.001f) ? 0.50f : innerR;
    }
    private bool TryComputeInnerRadiusFromNeighbors(Vector2Int key, out float innerR)
{
    innerR = 0f;

    if (!cells.TryGetValue(key, out var cell) || cell == null)
        return false;

    Vector3 p = cell.transform.position;

    for (int dir = 0; dir < 6; dir++)
    {
        var nk = key + AxialDirs[dir];
        if (!cells.TryGetValue(nk, out var nCell) || nCell == null)
            continue;

        float d = Vector3.Distance(p, nCell.transform.position);
        if (d > 0.0001f)
        {
            innerR = d * 0.5f;
            return true;
        }
    }

    return false;
}

}
