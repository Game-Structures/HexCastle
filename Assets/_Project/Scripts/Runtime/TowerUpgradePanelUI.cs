using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class TowerUpgradePanelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Buttons (3 options)")]
    [SerializeField] private Button option1;
    [SerializeField] private Button option2;
    [SerializeField] private Button option3;

    [Header("Close")]
    [SerializeField] private Button closeButton;     // кнопка внутри Container
    [SerializeField] private Button backgroundButton; // Button на фоне (TowerUpgradePanel)

    [Header("Labels (TMP) - optional, can be auto-found from buttons")]
    [SerializeField] private TMP_Text label1;
    [SerializeField] private TMP_Text label2;
    [SerializeField] private TMP_Text label3;

    private TowerProgress pendingProgress;
    private TowerShooter pendingShooter;
    private TowerUpgradeId[] pendingOptions;

    private void Awake()
    {
        AutoBind();
        BindClose();
        Hide();
    }

    private void OnValidate()
    {
        AutoBind();
    }

    private void AutoBind()
    {
        if (panelRoot == null) panelRoot = gameObject;

        if (option1 != null && label1 == null) label1 = option1.GetComponentInChildren<TMP_Text>(true);
        if (option2 != null && label2 == null) label2 = option2.GetComponentInChildren<TMP_Text>(true);
        if (option3 != null && label3 == null) label3 = option3.GetComponentInChildren<TMP_Text>(true);

        // backgroundButton можно не назначать руками – попробуем взять Button с текущего объекта (фона)
        if (backgroundButton == null)
            backgroundButton = GetComponent<Button>();
    }

    private void BindClose()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        if (backgroundButton != null)
        {
            backgroundButton.onClick.RemoveAllListeners();
            backgroundButton.onClick.AddListener(Hide);
        }
    }

    public void Show(TowerProgress progress, TowerShooter shooter, TowerUpgradeId[] options, System.Func<TowerUpgradeId, string> titleFn)
    {
        AutoBind();
        BindClose();

        pendingProgress = progress;
        pendingShooter = shooter;
        pendingOptions = options;

        if (label1 != null) label1.text = titleFn(options[0]);
        if (label2 != null) label2.text = titleFn(options[1]);
        if (label3 != null) label3.text = titleFn(options[2]);

        option1.onClick.RemoveAllListeners();
        option2.onClick.RemoveAllListeners();
        option3.onClick.RemoveAllListeners();

        option1.onClick.AddListener(() => Pick(0));
        option2.onClick.AddListener(() => Pick(1));
        option3.onClick.AddListener(() => Pick(2));

        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        pendingProgress = null;
        pendingShooter = null;
        pendingOptions = null;
    }

    private void Pick(int index)
    {
        if (pendingProgress == null || pendingShooter == null || pendingOptions == null) { Hide(); return; }
        if (index < 0 || index > 2) { Hide(); return; }

        var chosen = pendingOptions[index];

        bool ok = pendingProgress.ConsumeUpgrade();
        if (ok)
            TowerUpgradeApplier.Apply(chosen, pendingShooter);

        Hide();
    }
}
