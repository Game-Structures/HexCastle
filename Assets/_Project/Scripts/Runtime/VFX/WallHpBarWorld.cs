using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WallTileLink))]
public sealed class WallHpBarWorld : MonoBehaviour
{
    [Header("Standard size")]
    [SerializeField] private float barLengthK = 0.25f;
    [SerializeField] private float barHeightK = 0.12f;

    [Header("Placement")]
    [SerializeField] private float yPaddingAboveTop = 0.20f;

    [Header("BUILD phase")]
    [SerializeField] private bool showAlwaysInBuildPhase = true;

    [Tooltip("If object with this name is active – считаем что BUILD (обычно это StartWaveButton в HUD).")]
    [SerializeField] private string buildIndicatorObjectName = "StartWaveButton";

    [Header("Visibility rules (COMBAT)")]
    [SerializeField] private float showForSecondsAfterDamage = 2.5f;
    [SerializeField] private bool hideWhenFullHp = true;

    [Header("Render priority")]
    [SerializeField] private int sortingOrder = 5000;
    [SerializeField] private float pushTowardCamera = 0.60f;

    [Header("Colors")]
    [SerializeField] private Color borderColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color lostRedColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color hpGreenColor = new Color(0.2f, 1f, 0.2f, 1f);

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private float debugLogEverySeconds = 1.0f;

    private WallTileLink link;
    private float prevHp;
    private bool everDamaged;
    private float lastDamageTime = -999f;

    private Transform root;
    private SpriteRenderer border;
    private SpriteRenderer redFull;
    private SpriteRenderer greenHp;

    private float barWidth;
    private float barHeight;

    private static Sprite whiteSprite;

    private static GameObject cachedBuildIndicator;
    private float nextDebugTime;

    private void Awake()
    {
        link = GetComponent<WallTileLink>();

        EnsureSprite();
        RecalculateSize();
        Build();

        prevHp = link != null ? link.Hp : 0f;
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (link == null) return;

        float hp = link.Hp;
        float max = Mathf.Max(1f, link.MaxHpPublic);

        bool isBuild = showAlwaysInBuildPhase && IsBuildPhaseByIndicator();

        if (hp < prevHp - 0.0001f)
        {
            everDamaged = true;
            lastDamageTime = Time.time;
        }
        prevHp = hp;

        bool shouldShow;
        if (isBuild)
        {
            shouldShow = true;
        }
        else
        {
            shouldShow = everDamaged;

            if (shouldShow && hideWhenFullHp && hp >= max - 0.0001f)
                shouldShow = false;

            if (shouldShow && (Time.time - lastDamageTime) > showForSecondsAfterDamage)
                shouldShow = false;
        }

        SetVisible(shouldShow);
        if (!shouldShow) return;

        // Бар по центру тайла (XZ = transform.position)
        float topY = EstimateTopYExcludingBar();
        Vector3 pos = new Vector3(transform.position.x, topY + yPaddingAboveTop, transform.position.z);

        var cam = Camera.main;
        if (cam != null)
        {
            pos += cam.transform.forward * pushTowardCamera;
            root.position = pos;
            root.rotation = cam.transform.rotation;
        }
        else
        {
            root.position = pos;
        }

        // КРАСНОЕ: всегда полное под низом
        // ЗЕЛЁНОЕ: уменьшается только справа (левый край фиксирован)
        float k = Mathf.Clamp01(hp / max);
        float greenW = Mathf.Clamp(barWidth * k, 0f, barWidth);

        greenHp.transform.localScale = new Vector3(greenW, barHeight, 1f);

        float leftX = -barWidth * 0.5f;
        greenHp.transform.localPosition = new Vector3(leftX + greenW * 0.5f, 0f, 0f);

        if (debugLogs && Time.time >= nextDebugTime)
        {
            nextDebugTime = Time.time + Mathf.Max(0.2f, debugLogEverySeconds);
            Debug.Log($"[WallHpBar] build={isBuild} show={shouldShow} hp={hp:0.0}/{max:0.0} k={k:0.00} indicator={(cachedBuildIndicator ? cachedBuildIndicator.activeInHierarchy : false)}");
        }
    }

    private bool IsBuildPhaseByIndicator()
    {
        if (cachedBuildIndicator == null)
            cachedBuildIndicator = GameObject.Find(buildIndicatorObjectName);

        return cachedBuildIndicator != null && cachedBuildIndicator.activeInHierarchy;
    }

    private void RecalculateSize()
    {
        float tileRadius = EstimateTileRadiusFromWallSegments();
        if (tileRadius <= 0.0001f) tileRadius = 1.0f;

        barWidth = Mathf.Clamp(tileRadius * barLengthK, 0.10f, 10f);
        barHeight = Mathf.Clamp(barWidth * barHeightK, 0.02f, 2f);
    }

    private float EstimateTileRadiusFromWallSegments()
    {
        var wallRoot = transform.Find("_Wall3D");
        if (wallRoot == null) return 0f;

        var renderers = wallRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return 0f;

        float best = 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var s = r.bounds.size;
            float xz = Mathf.Max(s.x, s.z);
            if (xz > best) best = xz;
        }
        return best;
    }

    private float EstimateTopYExcludingBar()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return transform.position.y + 1.0f;

        bool hasAny = false;
        float top = transform.position.y;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (root != null && r.transform.IsChildOf(root)) continue;

            float y = r.bounds.max.y;
            if (!hasAny) { top = y; hasAny = true; }
            else top = Mathf.Max(top, y);
        }

        if (!hasAny)
            top = transform.position.y + 1.0f;

        return top;
    }

    private void Build()
    {
        if (root != null)
        {
            if (Application.isPlaying) Destroy(root.gameObject);
            else DestroyImmediate(root.gameObject);
        }

        root = new GameObject("WallHpBar").transform;
        root.SetParent(transform, false);

        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(root, false);
        border = borderGO.AddComponent<SpriteRenderer>();
        border.sprite = whiteSprite;
        border.sortingOrder = sortingOrder;
        border.color = borderColor;
        border.transform.localScale = new Vector3(barWidth * 1.02f, barHeight * 1.25f, 1f);

        var redGO = new GameObject("LostRedFull");
        redGO.transform.SetParent(root, false);
        redFull = redGO.AddComponent<SpriteRenderer>();
        redFull.sprite = whiteSprite;
        redFull.sortingOrder = sortingOrder + 1;
        redFull.color = lostRedColor;
        redFull.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        redFull.transform.localPosition = Vector3.zero;

        var greenGO = new GameObject("GreenHP");
        greenGO.transform.SetParent(root, false);
        greenHp = greenGO.AddComponent<SpriteRenderer>();
        greenHp.sprite = whiteSprite;
        greenHp.sortingOrder = sortingOrder + 2;
        greenHp.color = hpGreenColor;

        // Инициализация: full hp
        greenHp.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        greenHp.transform.localPosition = Vector3.zero;
    }

    private void SetVisible(bool v)
    {
        if (root != null && root.gameObject.activeSelf != v)
            root.gameObject.SetActive(v);
    }

    private static void EnsureSprite()
    {
        if (whiteSprite != null) return;

        var tex = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
    }
}
