// Assets/_Project/Scripts/Runtime/EnclosureDebug.cs
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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
[SerializeField] private EnclosureBuildCatalog buildCatalog;

    [Header("Markers / Build")]
    [SerializeField] private bool showMarkers = true;
    [SerializeField] private int buildCost = 15;
    [SerializeField] private float markerYOffset = 0.25f;
    [SerializeField] private float markerScale = 0.18f;
    [SerializeField] private int markerSortingOrder = 200;

    [Header("Coin sprite (Resources path)")]
    [SerializeField] private string coinSpriteResourcePath = "CoinSprite"; // Assets/Resources/CoinSprite.png

[Header("Build Spawn")]
[SerializeField] private float builtPrefabYOffset = 0.25f;

private readonly Dictionary<Vector2Int, GameObject> builtSpawned = new();
private Transform builtRoot;

    // Build materials are loaded from Resources by these names:
    private static readonly string[] BuildMatNames =
    {
        "MatTile_Blue",
        "MatTile_Yellow",
        "MatTile_Orange",
        "MatTile_Pink",
        "MatTile_Purple",
        "MatTile_Red",
    };

    [Header("Popup options (manual)")]
   
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
    private int lastWallHash = int.MinValue;
    private int lastEnclosedCount = -1;

    private Material baseMat;
    private Material grassMat;

    private static readonly Vector2Int CastleKey = new Vector2Int(0, 0);

    // Popup link
    private EnclosureBuildPopup popup;
    private Vector2Int popupKey;
    private bool popupHasKey;

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
            GetCoinSprite();

            EnsurePopupInstance();
            EnsurePopupOptionsPrepared();

            ClearMarkers();
            allCells.Clear();
            views.Clear();
            enclosedSet.Clear();
            builtKind.Clear();

            lastWallHash = int.MinValue;
            lastEnclosedCount = -1;
        }

        if (waves == null)
            waves = FindFirstObjectByType<WaveController>();

        if (allCells.Count == 0 || views.Count == 0)
            RebuildCellCache();

        if (allCells.Count == 0) return;

        int wallHash = ComputeWallStateHash();
        if (wallHash != lastWallHash)
        {
            int enclosedCount = ComputeEnclosedRecolorAndMarkers(out int wallCount, out int openGaps);

            Debug.Log($"[Enclosure] walls={wallCount}, gaps={openGaps}, enclosed={enclosedCount}");
            lastWallHash = wallHash;
            lastEnclosedCount = enclosedCount;
        }

        ValidatePopup();
    }

    private void ValidatePopup()
    {
        if (popup == null || !popup.IsOpen) return;

        bool isBuild = waves == null || waves.CurrentPhase == WaveController.Phase.Build;
        if (!isBuild) { HidePopup(); return; }

        if (!popupHasKey) { HidePopup(); return; }

        if (!enclosedSet.Contains(popupKey) || builtKind.ContainsKey(popupKey))
        {
            HidePopup();
            return;
        }
    }

    public int BuildCost => buildCost;
    public int EnclosedCount => enclosedSet.Count;
    public int BuiltCount => builtKind.Count;

    // marker click
    public bool TryBuildAt(Vector2Int key)
    {
        bool isBuild = waves == null || waves.CurrentPhase == WaveController.Phase.Build;
        if (!isBuild) return false;

        if (!enclosedSet.Contains(key)) return false;
        if (builtKind.ContainsKey(key)) return false;

        ShowPopup(key);
        return true;
    }

    private bool TryBuildAtKind(Vector2Int key, int optionIndex)
{
    Debug.Log($"[EnclosureBuild] TryBuildAt optionIndex={optionIndex} at {key.x},{key.y}");

    bool isBuild = waves == null || waves.CurrentPhase == WaveController.Phase.Build;
    if (!isBuild) { Debug.LogWarning("[EnclosureBuild] FAIL: not in Build phase"); return false; }

    if (!enclosedSet.Contains(key)) { Debug.LogWarning("[EnclosureBuild] FAIL: cell not enclosed anymore"); return false; }
    if (builtKind.ContainsKey(key)) { Debug.LogWarning("[EnclosureBuild] FAIL: already built"); return false; }

    // Берём опцию из каталога (runtimePopupOptions формируется из buildCatalog)
    if (runtimePopupOptions == null || optionIndex < 0 || optionIndex >= runtimePopupOptions.Count)
    {
        Debug.LogWarning($"[EnclosureBuild] FAIL: optionIndex out of range. optionIndex={optionIndex}, options={(runtimePopupOptions==null?0:runtimePopupOptions.Count)}");
        return false;
    }

    var opt = runtimePopupOptions[optionIndex];
    int cost = opt != null ? opt.cost : buildCost;

    if (!GoldBank.TrySpend(cost))
    {
        Debug.LogWarning($"[EnclosureBuild] FAIL: not enough gold. gold={GoldBank.Gold}, cost={cost}");
        return false;
    }

    builtKind[key] = optionIndex;

    // убрать маркер
    if (markers.TryGetValue(key, out var markerGo) && markerGo != null)
        Destroy(markerGo);
    markers.Remove(key);

    // корень для построек
    if (builtRoot == null)
    {
        var rootGo = GameObject.Find("_EnclosureBuiltRoot");
        if (rootGo == null) rootGo = new GameObject("_EnclosureBuiltRoot");
        builtRoot = rootGo.transform;
    }

    // если вдруг уже был объект – удалить
    if (builtSpawned.TryGetValue(key, out var old) && old != null)
        Destroy(old);
    builtSpawned.Remove(key);

    // заспавнить prefab (если назначен)
    if (opt != null && opt.prefab != null && views.TryGetValue(key, out var view) && view != null)
    {
        Vector3 pos = view.transform.position + new Vector3(0f, builtPrefabYOffset, 0f);
        var go = Instantiate(opt.prefab, pos, Quaternion.identity, builtRoot);
        go.name = $"EnclBuilt_{key.x}_{key.y}_{opt.prefab.name}";
        builtSpawned[key] = go;
    }
    else
    {
        Debug.LogWarning("[EnclosureBuild] No prefab assigned for this option (opt.prefab is null) or no cell view found.");
    }

    Debug.Log($"[EnclosureBuild] OK: built at {key.x},{key.y} optionIndex={optionIndex} (-{cost}). Gold left={GoldBank.Gold}");
    return true;
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

    private void EnsurePopupInstance()
    {
        if (popup != null) return;

        popup = FindFirstObjectByType<EnclosureBuildPopup>();
        if (popup != null) return;

        var go = new GameObject("_EnclosureBuildPopup");
        DontDestroyOnLoad(go);
        popup = go.AddComponent<EnclosureBuildPopup>();
    }

    private readonly List<EnclosureBuildOption> runtimePopupOptions = new();

private void EnsurePopupOptionsPrepared()
{
    runtimePopupOptions.Clear();

    // 1) Если назначен Catalog и в нём есть options — используем их
    if (buildCatalog != null && buildCatalog.options != null && buildCatalog.options.Count > 0)
    {
        for (int i = 0; i < buildCatalog.options.Count; i++)
        {
            var opt = buildCatalog.options[i];
            if (opt != null) runtimePopupOptions.Add(opt);
        }
        return;
    }

    // 2) Фолбэк: нагенерим варианты из материалов (как раньше)
    var icon = GetCoinSprite();
    for (int i = 0; i < buildMats.Count; i++)
    {
        runtimePopupOptions.Add(new EnclosureBuildOption
        {
            icon = icon,
            title = $"Option {i + 1}",
            description = "Placeholder description",
            cost = buildCost,
            tileMaterial = null,
            prefab = null
        });

        // ВАЖНО: kindIndex хранится в самом opt? Его у тебя нет в классе.
        // Поэтому используем правило: optionIndex == kindIndex.
        // Т.е. порядок в каталоге должен соответствовать buildMats, либо мы сделаем поле kindIndex (след. шаг).
    }
}


    private void ShowPopup(Vector2Int key)
{
    // Можно открывать меню даже если материалов нет – каталог может работать только с префабами.
    EnsureEventSystem();
    EnsurePopupInstance();
    EnsurePopupOptionsPrepared();

    popupKey = key;
    popupHasKey = true;

    // Если вообще нет опций – нечего показывать
    if (runtimePopupOptions == null || runtimePopupOptions.Count == 0)
    {
        Debug.LogWarning("[EnclosurePopup] No options to show (buildCatalog is empty and fallback options are empty).");
        popupHasKey = false;
        return;
    }

    popup.Open(
        runtimePopupOptions,
        onSelectIndex: (optionIndex) =>
        {
            Debug.Log($"[EnclosurePopup] Selected optionIndex={optionIndex} for cell={popupKey.x},{popupKey.y}, popupHasKey={popupHasKey}");

            if (!popupHasKey)
            {
                Debug.LogWarning("[EnclosurePopup] popupHasKey=false, ignoring selection");
                return;
            }

            // Пока упрощение: optionIndex == kindIndex
            bool ok = TryBuildAtKind(popupKey, optionIndex);

            Debug.Log($"[EnclosurePopup] TryBuildAtKind returned ok={ok}");

            if (ok) HidePopup();
        },
        onClose: () =>
        {
            Debug.Log("[EnclosurePopup] Closed");
            popupHasKey = false;
        }
    );
}


    private void HidePopup()
    {
        popupHasKey = false;
        if (popup != null) popup.Close();
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var esGo = new GameObject("_EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(esGo);
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

    private static int Opp(int dir) => (dir + 3) % 6;

    private bool HasWallOnSharedEdge(Vector2Int a, Vector2Int b, int dirAtoB)
    {
        // периметр замка считаем стеной
        if (a == CastleKey || b == CastleKey)
            return true;

        // стык: сегмент должен быть у обоих на общем ребре
        return placement.HasWallSegment(a.x, a.y, dirAtoB)
            && placement.HasWallSegment(b.x, b.y, Opp(dirAtoB));
    }

    private int ComputeWallStateHash()
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < allCells.Count; i++)
            {
                var k = allCells[i];
                if (!placement.TryGetWallMask(k.x, k.y, out int m)) continue;

                int keyHash = (k.x * 73856093) ^ (k.y * 19349663) ^ (m * 83492791);
                h = h * 31 + keyHash;
            }
            return h;
        }
    }

    private int ComputeEnclosedRecolorAndMarkers(out int wallCount, out int openGaps)
    {
        enclosedSet.Clear();

        var blocked = new HashSet<Vector2Int>(256);
        wallCount = 0;

        for (int i = 0; i < allCells.Count; i++)
        {
            var k = allCells[i];
            if (k == CastleKey) { blocked.Add(k); continue; }

            if (placement.IsOccupied(k.x, k.y))
            {
                blocked.Add(k);
                wallCount++;
            }
        }

        var diag = new Dictionary<Vector2Int, List<Vector2Int>>(256);
        var extraOutsideSeeds = new HashSet<Vector2Int>();
        openGaps = 0;

        foreach (var a in blocked)
        {
            for (int dir = 0; dir < 6; dir++)
            {
                var b = a + AxialDirs[dir];
                if (!cellSet.Contains(b)) continue;
                if (!blocked.Contains(b)) continue;

                if (a.x > b.x || (a.x == b.x && a.y > b.y))
                    continue;

                if (HasWallOnSharedEdge(a, b, dir))
                    continue;

                openGaps++;

                var c = a + AxialDirs[(dir + 5) % 6];
                var d = a + AxialDirs[(dir + 1) % 6];

                bool cIn = cellSet.Contains(c) && !blocked.Contains(c);
                bool dIn = cellSet.Contains(d) && !blocked.Contains(d);

                bool cOut = !cellSet.Contains(c);
                bool dOut = !cellSet.Contains(d);

                if (cIn && dIn)
                {
                    AddDiag(diag, c, d);
                    AddDiag(diag, d, c);
                }
                else
                {
                    if (cIn && dOut) extraOutsideSeeds.Add(c);
                    if (dIn && cOut) extraOutsideSeeds.Add(d);
                }
            }
        }

        var visited = new HashSet<Vector2Int>(512);
        var q = new Queue<Vector2Int>(512);

        for (int i = 0; i < borderCells.Count; i++)
        {
            var b = borderCells[i];
            if (blocked.Contains(b)) continue;
            if (visited.Add(b))
                q.Enqueue(b);
        }

        foreach (var s in extraOutsideSeeds)
        {
            if (blocked.Contains(s)) continue;
            if (visited.Add(s))
                q.Enqueue(s);
        }

        while (q.Count > 0)
        {
            var cur = q.Dequeue();

            for (int dir = 0; dir < 6; dir++)
            {
                var n = cur + AxialDirs[dir];
                if (!cellSet.Contains(n)) continue;
                if (blocked.Contains(n)) continue;
                if (visited.Add(n))
                    q.Enqueue(n);
            }

            if (diag.TryGetValue(cur, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var n2 = list[i];
                    if (!cellSet.Contains(n2)) continue;
                    if (blocked.Contains(n2)) continue;
                    if (visited.Add(n2))
                        q.Enqueue(n2);
                }
            }
        }

        for (int i = 0; i < allCells.Count; i++)
        {
            var k = allCells[i];
            if (blocked.Contains(k)) continue;
            if (!visited.Contains(k))
                enclosedSet.Add(k);
        }

        // если клетка перестала быть enclosed – убираем built
        var builtToRemove = new List<Vector2Int>();
        foreach (var kv in builtKind)
            if (!enclosedSet.Contains(kv.Key))
                builtToRemove.Add(kv.Key);
        for (int i = 0; i < builtToRemove.Count; i++)
            builtKind.Remove(builtToRemove[i]);

        foreach (var kv in views)
        {
            var key = kv.Key;
            if (blocked.Contains(key)) continue;
            ApplyCellMaterial(key);
        }

        UpdateMarkers();
        return enclosedSet.Count;
    }

    private static void AddDiag(Dictionary<Vector2Int, List<Vector2Int>> diag, Vector2Int from, Vector2Int to)
    {
        if (!diag.TryGetValue(from, out var list))
        {
            list = new List<Vector2Int>(4);
            diag[from] = list;
        }
        for (int i = 0; i < list.Count; i++)
            if (list[i] == to) return;

        list.Add(to);
    }

    private void ApplyCellMaterial(Vector2Int key)
    {
        if (!views.TryGetValue(key, out var view) || view == null || view.rend == null) return;

        if (builtKind.TryGetValue(key, out int idx) && idx >= 0 && idx < buildMats.Count && buildMats[idx] != null)
        {
            view.rend.sharedMaterial = buildMats[idx];
            return;
        }

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

        if (!showMarkers)
        {
            foreach (var kv in markers)
                if (kv.Value != null) kv.Value.SetActive(false);
            return;
        }

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

        root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        root.transform.localScale = Vector3.one * (markerScale * 3.2f);

        var col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.2f, 1.2f, 0.2f);

        var click = root.AddComponent<EnclosedBuildMarker>();
        click.Init(key);

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
