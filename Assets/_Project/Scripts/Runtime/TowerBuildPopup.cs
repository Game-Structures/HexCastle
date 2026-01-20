using UnityEngine;
using UnityEngine.UI;

public sealed class TowerBuildPopup : MonoBehaviour
{
    public static TowerBuildPopup Instance { get; private set; }

    [Header("Root to show/hide (keep THIS GameObject active!)")]
    [SerializeField] private GameObject root;

    [Header("Buttons")]
    [SerializeField] private Button archerButton;
    [SerializeField] private Button artilleryButton;
    [SerializeField] private Button magicButton;
    [SerializeField] private Button flameButton;
    [SerializeField] private Button closeButton;

    private TowerSlot current;

    private void Awake()
    {
        Instance = this;

        if (root == null) root = gameObject;

        BindButtons();
        Hide();
    }

    private void OnEnable()
    {
        Instance = this;
    }

    private void BindButtons()
    {
        if (archerButton != null)
        {
            archerButton.onClick.RemoveAllListeners();
            archerButton.onClick.AddListener(() => Choose(TowerType.Archer));
        }

        if (artilleryButton != null)
        {
            artilleryButton.onClick.RemoveAllListeners();
            artilleryButton.onClick.AddListener(() => Choose(TowerType.Cannon));
        }

        if (magicButton != null)
        {
            magicButton.onClick.RemoveAllListeners();
            magicButton.onClick.AddListener(() => Choose(TowerType.Magic));
        }

        if (flameButton != null)
        {
            flameButton.onClick.RemoveAllListeners();
            flameButton.onClick.AddListener(() => Choose(TowerType.Flame));
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    public void Show(TowerSlot slot)
    {
        current = slot;

        // NEW: disable Cannon button until unlocked by building
        if (artilleryButton != null)
        {
            bool unlocked = BuildingEffectsManager.Instance != null && BuildingEffectsManager.Instance.IsArtilleryUnlocked;
            artilleryButton.interactable = unlocked;
        }

        if (root != null) root.SetActive(true);
    }

    public void Hide()
    {
        current = null;
        if (root != null) root.SetActive(false);
    }

    private void Choose(TowerType type)
    {
        if (current == null) { Hide(); return; }

        // NEW: safety guard (logic also duplicated in TilePlacement)
        if (type == TowerType.Cannon)
        {
            bool unlocked = BuildingEffectsManager.Instance != null && BuildingEffectsManager.Instance.IsArtilleryUnlocked;
            if (!unlocked) return;
        }

        current.TryBuild(type);
        Hide();
    }
}
