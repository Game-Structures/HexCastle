// Assets/_Project/Scripts/Runtime/HUDController.cs
using TMPro;
using UnityEngine;

public sealed class HUDController : MonoBehaviour
{
    [Header("HUD Text (debug panel)")]
    [SerializeField] private TMP_Text hudText;
    [SerializeField] private GameObject startWaveButton;

    // BUILD UI (магазин)
    [SerializeField] private GameObject wallHandPanel;
    [SerializeField] private GameObject buttonControls;

    [Header("Banner (DungeonBannerText in HUDCanvas)")]
    [SerializeField] private TextMeshProUGUI bannerText;
    [SerializeField] private float bannerSeconds = 2.5f;

    private float bannerTimer;
    private bool bannerWasInactiveBeforeShow;

    private CastleHealth castle;
    private WaveController waves;
    private WallHandManager hand;
    private EnclosureDebug enclosure;

    private bool initialized;
    private bool triedFindUi;

    private void Awake()
    {
        if (hudText == null)
            hudText = GetComponentInChildren<TMP_Text>(true);

        FindBannerText();

        if (bannerText != null)
        {
            // не трогаем активность объекта здесь – только компонент
            bannerText.text = "";
            bannerText.enabled = false;
        }

        Debug.Log($"[HUDController] Loaded (DungeonBanner manual v2) banner={(bannerText != null ? "ok" : "missing")}");
    }

    private void FindBannerText()
    {
        if (bannerText != null) return;

        // 1) Надежно: HUDCanvas/DungeonBannerText (находит даже если DungeonBannerText неактивен)
        var hudCanvasGo = GameObject.Find("HUDCanvas");
        if (hudCanvasGo != null)
        {
            var t = hudCanvasGo.transform.Find("DungeonBannerText");
            if (t != null)
            {
                bannerText = t.GetComponent<TextMeshProUGUI>();
                if (bannerText != null) return;
            }
        }

        // 2) Фолбэк: активный объект по имени (если баннер активен)
        var go = GameObject.Find("DungeonBannerText");
        if (go != null)
            bannerText = go.GetComponent<TextMeshProUGUI>();
    }

    public void ShowBanner(string message, float seconds = -1f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        FindBannerText();

        float dur = seconds > 0f ? seconds : bannerSeconds;
        bannerTimer = Mathf.Max(0.1f, dur);

        string msg = message.Trim();

        if (bannerText == null)
        {
            Debug.LogWarning($"[HUDController] ShowBanner FAILED (DungeonBannerText not found): {msg}");
            return;
        }

        // если объект был выключен – временно включим
        bannerWasInactiveBeforeShow = !bannerText.gameObject.activeSelf;
        if (bannerWasInactiveBeforeShow)
            bannerText.gameObject.SetActive(true);

        bannerText.text = msg;
        bannerText.enabled = true;

        Debug.Log($"[HUDController] ShowBanner visible: {msg}");
    }

    private void TryFindUiOnce()
    {
        if (triedFindUi) return;
        triedFindUi = true;

        if (wallHandPanel == null)
            wallHandPanel = GameObject.Find("WallHandPanel");

        if (buttonControls == null)
            buttonControls = GameObject.Find("ButtonControls");
    }

    private void Update()
    {
        if (!initialized) initialized = true;

        // banner timer
        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.deltaTime;
            if (bannerTimer <= 0f)
            {
                bannerTimer = 0f;

                if (bannerText != null)
                {
                    bannerText.text = "";
                    bannerText.enabled = false;

                    // возвращаем как было
                    if (bannerWasInactiveBeforeShow && bannerText.gameObject.activeSelf)
                        bannerText.gameObject.SetActive(false);

                    bannerWasInactiveBeforeShow = false;
                }
            }
        }

        if (GameState.IsGameOver)
        {
            if (startWaveButton != null) startWaveButton.SetActive(false);
            if (wallHandPanel != null) wallHandPanel.SetActive(false);
            if (buttonControls != null) buttonControls.SetActive(false);

            if (hudText != null) hudText.text = "GAME OVER";
            return;
        }

        if (hudText == null) return;

        if (castle == null) castle = FindFirstObjectByType<CastleHealth>();
        if (waves == null) waves = FindFirstObjectByType<WaveController>();
        if (hand == null) hand = FindFirstObjectByType<WallHandManager>();
        if (enclosure == null) enclosure = FindFirstObjectByType<EnclosureDebug>();

        if (castle == null || waves == null) return;

        TryFindUiOnce();

        bool isBuild = waves.CurrentPhase == WaveController.Phase.Build;

        if (startWaveButton != null)
            startWaveButton.SetActive(isBuild);

        if (wallHandPanel != null)
            wallHandPanel.SetActive(isBuild);

        if (buttonControls != null)
            buttonControls.SetActive(isBuild);

        string phase = isBuild ? "BUILD" : "COMBAT";

        string enclosureLine = "";
        if (enclosure != null)
            enclosureLine = $"Enclosed: {enclosure.EnclosedCount}, Built: {enclosure.BuiltCount}\n";

        string handLine = "";
        if (hand != null)
        {
            handLine =
                $"Hand: [1]{hand.Hand[0]}  [2]{hand.Hand[1]}  [3]{hand.Hand[2]}\n" +
                $"Selected: {hand.SelectedIndex + 1}, Rot: {hand.Rotation} (Q/E)\n";
        }

        hudText.text =
            $"Gold: {GoldBank.Gold}\n" +
            $"Castle HP: {castle.CurrentHp}/{castle.MaxHp}\n" +
            $"Wave: {waves.WaveNumber}\n" +
            $"Phase: {phase}\n" +
            enclosureLine +
            handLine;
    }
}
