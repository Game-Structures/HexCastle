// Assets/_Project/Scripts/Runtime/EnclosureDebug.cs
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

public sealed class EnclosureDebug : MonoBehaviour
{
    private static readonly Vector2Int[] AxialDirs =
    {
        new Vector2Int( 1,  0),
        new Vector2Int( 1, -1),
        new Vector2Int( 0, -1),
        new Vector2Int(-1,  0),
        new Vector2Int(-1,  1),
        new Vector2Int( 0,  1),
    };

    public static EnclosureDebug Instance { get; private set; }

    [Header("Polling")]
    [SerializeField] private float pollInterval = 0.25f;

    [Header("Markers / Build")]
    [SerializeField] private bool showMarkers = true;
    [SerializeField] private int buildCost = 15;
    [SerializeField] private float markerYOffset = 0.25f;
    [SerializeField] private float markerScale = 0.18f;
    [SerializeField] private int markerSortingOrder = 200;

    [Header("Coin sprite (Resources path)")]
    [SerializeField] private string coinSpriteResourcePath = "CoinSprite"; // Assets/Resources/CoinSprite.png

    // Build materials are loaded from Resources by these names:
    private static readonly string[] BuildMatNames =
    {
        "MatTile_Blue",
        "MatTile_Yellow",
        "MatTile_Orange",
        "MatTile_Pink",
        "MatTile_Purple",
    };

    private readonly List<Material> buildMats = new();
    private Sprite coinSprite;

    private TilePlacement placement;
    private WaveController waves;

    private readonly List<Vector2Int> allCells = new();
    private readonly HashSet<Vector2Int> cellSet = new();
    private readonly List<Vector2Int> borderCells = new();
    private readonly Dictionary<Vector2Int, HexCellView> views = new();

    private readonly HashSet<Vector2Int> enclosedSet = new();

    // built cell -> material index (0..buildMats.Count-1)
    private readonly Dictionary<Vector2Int, int> builtKind = new();

    // enclosed cell -> marker GO
    private readonly Dictionary<Vector2Int, GameObject> markers = new();

    private float timer;
    private int lastWallCount = -1;
    private int lastEnclosedCount = -1;

    private Material baseMat;
    private Material grassMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<EnclosureDebug>() != null) return;
        var go = new GameObject("_EnclosureDebug");
        DontDestroyOnLoad(go);
        go.AddComponent<EnclosureDebug>();
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = pollInterval;

        if (placement == null)
        {
            placement = FindFirstObjectByType<TilePlacement>();
            if (placement == null) return;

            TryResolveMaterialsFromTilePlacement();
            LoadBuildMaterials();
            GetCoinSprite(); // preload (optional)

            ClearMarkers();
            allCells.Clear();
            views.Clear();
            enclosedSet.Clear();
            builtKind.Clear();
        }

        if (waves == null)
            waves = FindFirstObjectByType<WaveController>();

        if (allCells.Count == 0 || views.Count == 0)
            RebuildCellCache();

        if (allCells.Count == 0) return;

        int wallCount = CountWalls();
        if (wallCount == lastWallCount) return;

        int enclosedCount = ComputeEnclosedRecolorAndMarkers();

        if (wallCount != lastWallCount || enclosedCount != lastEnclosedCount)
        {
            Debug.Log($"[Enclosure] walls={wallCount}, enclosed={enclosedCount}");
            lastWallCount = wallCount;
            lastEnclosedCount = enclosedCount;
        }
    }

    public int BuildCost => buildCost;
public int EnclosedCount => enclosedSet.Count;
public int BuiltCount => builtKind.Count;

    public bool TryBuildAt(Vector2Int key)
    {
        bool isBuild = waves == null || waves.CurrentPhase == WaveController.Phase.Build;
        if (!isBuild) return false;

        if (!enclosedSet.Contains(key)) return false;
        if (builtKind.ContainsKey(key)) return false;

        if (!GoldBank.TrySpend(buildCost))
            return false;

        int idx = PickRandomBuildMatIndex();
        builtKind[key] = idx;

        // убрать маркер
        if (markers.TryGetValue(key, out var go) && go != null)
            Destroy(go);
        markers.Remove(key);

        // перекрасить сразу
        ApplyCellMaterial(key);

        Debug.Log($"[Enclosure] Built inside at {key.x},{key.y} (-{buildCost}). Gold left={GoldBank.Gold}");
        return true;
    }

    private int PickRandomBuildMatIndex()
    {
        if (buildMats.Count <= 0) return -1;
        return Random.Range(0, buildMats.Count);
    }

    private void LoadBuildMaterials()
    {
        buildMats.Clear();

        for (int i = 0; i < BuildMatNames.Length; i++)
        {
            string name = BuildMatNames[i];
            var m = Resources.Load<Material>(name);
            if (m != null) buildMats.Add(m);
            else Debug.LogWarning($"[Enclosure] Build material not found in Resources: '{name}'");
        }

        if (buildMats.Count == 0)
            Debug.LogWarning("[Enclosure] No build materials loaded. Put MatTile_* materials into Assets/Resources/.");
    }

    private Sprite GetCoinSprite()
    {
        if (coinSprite != null) return coinSprite;
        if (string.IsNullOrWhiteSpace(coinSpriteResourcePath)) return null;

        coinSprite = Resources.Load<Sprite>(coinSpriteResourcePath);
        if (coinSprite == null)
            Debug.LogWarning($"[Enclosure] Coin sprite not found in Resources at '{coinSpriteResourcePath}'.");
        return coinSprite;
    }

    private void TryResolveMaterialsFromTilePlacement()
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        var t = placement.GetType();
        var baseField = t.GetField("tileBaseMaterial", BF);
        var grassField = t.GetField("tileGrassMaterial", BF);

        baseMat = baseField != null ? baseField.GetValue(placement) as Material : null;
        grassMat = grassField != null ? grassField.GetValue(placement) as Material : null;

        if (grassMat == null)
            Debug.LogWarning("[Enclosure] tileGrassMaterial is null in TilePlacement (MatTile_Grass not assigned).");
        if (baseMat == null)
            Debug.LogWarning("[Enclosure] tileBaseMaterial is null in TilePlacement.");
    }

    private void RebuildCellCache()
    {
        allCells.Clear();
        cellSet.Clear();
        borderCells.Clear();
        views.Clear();
        enclosedSet.Clear();

        var cells = FindObjectsByType<HexCellView>(FindObjectsSortMode.None);
        foreach (var v in cells)
        {
            var k = new Vector2Int(v.q, v.r);
            allCells.Add(k);
            cellSet.Add(k);
            views[k] = v;
        }

        foreach (var k in allCells)
        {
            if (IsBorderCell(k))
                borderCells.Add(k);
        }
    }

    private bool IsBorderCell(Vector2Int key)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            var n = key + AxialDirs[dir];
            if (!cellSet.Contains(n))
                return true;
        }
        return false;
    }

    private bool IsBlocked(Vector2Int key)
    {
        if (key.x == 0 && key.y == 0) return true; // castle
        return placement.IsOccupied(key.x, key.y);  // walls
    }

    private int CountWalls()
    {
        int c = 0;
        for (int i = 0; i < allCells.Count; i++)
            if (placement.IsOccupied(allCells[i].x, allCells[i].y))
                c++;
        return c;
    }

    private int ComputeEnclosedRecolorAndMarkers()
    {
        enclosedSet.Clear();

        var visited = new HashSet<Vector2Int>();
        var q = new Queue<Vector2Int>();

        // flood fill from border over empty cells
        for (int i = 0; i < borderCells.Count; i++)
        {
            var b = borderCells[i];
            if (IsBlocked(b)) continue;
            if (visited.Add(b))
                q.Enqueue(b);
        }

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            for (int dir = 0; dir < 6; dir++)
            {
                var n = cur + AxialDirs[dir];
                if (!cellSet.Contains(n)) continue;
                if (IsBlocked(n)) continue;
                if (visited.Add(n))
                    q.Enqueue(n);
            }
        }

        for (int i = 0; i < allCells.Count; i++)
        {
            var k = allCells[i];
            if (IsBlocked(k)) continue;
            if (!visited.Contains(k))
                enclosedSet.Add(k);
        }

        // if cell is no longer enclosed -> clear "built"
        var builtToRemove = new List<Vector2Int>();
        foreach (var kv in builtKind)
            if (!enclosedSet.Contains(kv.Key))
                builtToRemove.Add(kv.Key);
        for (int i = 0; i < builtToRemove.Count; i++)
            builtKind.Remove(builtToRemove[i]);

        // recolor all empty cells (do not touch walls/castle)
        foreach (var kv in views)
        {
            var key = kv.Key;
            if (key.x == 0 && key.y == 0) continue;
            if (placement.IsOccupied(key.x, key.y)) continue;

            ApplyCellMaterial(key);
        }

        UpdateMarkers();
        return enclosedSet.Count;
    }

    private void ApplyCellMaterial(Vector2Int key)
    {
        if (!views.TryGetValue(key, out var view) || view == null || view.rend == null) return;

        // built inside
        if (builtKind.TryGetValue(key, out int idx) && idx >= 0 && idx < buildMats.Count && buildMats[idx] != null)
        {
            view.rend.sharedMaterial = buildMats[idx];
            return;
        }

        // enclosed but not built
        if (enclosedSet.Contains(key))
        {
            if (grassMat != null) view.rend.sharedMaterial = grassMat;
        }
        else
        {
            if (baseMat != null) view.rend.sharedMaterial = baseMat;
        }
    }

    private void UpdateMarkers()
    {
        bool isBuild = waves == null || waves.CurrentPhase == WaveController.Phase.Build;

        // remove markers not enclosed or already built
        var toRemove = new List<Vector2Int>();
        foreach (var kv in markers)
        {
            if (!enclosedSet.Contains(kv.Key) || builtKind.ContainsKey(kv.Key))
                toRemove.Add(kv.Key);
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            var k = toRemove[i];
            if (markers.TryGetValue(k, out var go) && go != null) Destroy(go);
            markers.Remove(k);
        }

        if (!showMarkers) return;

        foreach (var key in enclosedSet)
        {
            if (builtKind.ContainsKey(key)) continue;
            if (!views.TryGetValue(key, out var view) || view == null) continue;

            if (!markers.TryGetValue(key, out var m) || m == null)
            {
                m = CreateMarker(view.transform.position + new Vector3(0f, markerYOffset, 0f), key);
                markers[key] = m;
            }

            m.SetActive(isBuild);
        }
    }

    private GameObject CreateMarker(Vector3 worldPos, Vector2Int key)
    {
        var root = new GameObject("EnclosedBuildMarker");
        root.transform.position = worldPos;

        // lie on tile
        root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        root.transform.localScale = Vector3.one * (markerScale * 3.2f);

        // clickable collider
        var col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.2f, 1.2f, 0.2f);

        var click = root.AddComponent<EnclosedBuildMarker>();
        click.Init(key);

        // top "?"
        var qGo = new GameObject("Q");
        qGo.transform.SetParent(root.transform, false);
        qGo.transform.localPosition = new Vector3(0f, 0.28f, 0f);

        var qTmp = qGo.AddComponent<TextMeshPro>();
        qTmp.text = "?";
        qTmp.alignment = TextAlignmentOptions.Center;
        qTmp.enableWordWrapping = false;
        qTmp.fontSize = 18f;
        qTmp.color = Color.white;
        qTmp.outlineWidth = 0.35f;
        qTmp.outlineColor = Color.black;

        var qMr = qTmp.GetComponent<MeshRenderer>();
        if (qMr != null) qMr.sortingOrder = markerSortingOrder;

        // bottom row: coin + "15"
        var coin = GetCoinSprite();
        float rowY = -0.28f;

        if (coin != null)
        {
            var coinGo = new GameObject("Coin");
            coinGo.transform.SetParent(root.transform, false);
            coinGo.transform.localPosition = new Vector3(-0.28f, rowY, 0f);
            coinGo.transform.localScale = Vector3.one * 0.7f;

            var sr = coinGo.AddComponent<SpriteRenderer>();
            sr.sprite = coin;
            sr.sortingOrder = markerSortingOrder;

            var costGo = new GameObject("Cost");
            costGo.transform.SetParent(root.transform, false);
            costGo.transform.localPosition = new Vector3(0.32f, rowY, 0f);

            var costTmp = costGo.AddComponent<TextMeshPro>();
            costTmp.text = buildCost.ToString();
            costTmp.alignment = TextAlignmentOptions.Center;
            costTmp.enableWordWrapping = false;
            costTmp.fontSize = 10f;
            costTmp.color = Color.white;
            costTmp.outlineWidth = 0.25f;
            costTmp.outlineColor = Color.black;

            var costMr = costTmp.GetComponent<MeshRenderer>();
            if (costMr != null) costMr.sortingOrder = markerSortingOrder;
        }
        else
        {
            var costGo = new GameObject("Cost");
            costGo.transform.SetParent(root.transform, false);
            costGo.transform.localPosition = new Vector3(0f, rowY, 0f);

            var costTmp = costGo.AddComponent<TextMeshPro>();
            costTmp.text = buildCost.ToString();
            costTmp.alignment = TextAlignmentOptions.Center;
            costTmp.enableWordWrapping = false;
            costTmp.fontSize = 10f;
            costTmp.color = Color.white;
            costTmp.outlineWidth = 0.25f;
            costTmp.outlineColor = Color.black;

            var costMr = costTmp.GetComponent<MeshRenderer>();
            if (costMr != null) costMr.sortingOrder = markerSortingOrder;
        }

        return root;
    }

    private void ClearMarkers()
    {
        foreach (var kv in markers)
            if (kv.Value != null) Destroy(kv.Value);
        markers.Clear();
    }
}
