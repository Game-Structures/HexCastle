// Assets/_Project/Scripts/Runtime/HUDController.cs
using TMPro;
using UnityEngine;

public sealed class HUDController : MonoBehaviour
{
    [SerializeField] private TMP_Text hudText;
    [SerializeField] private GameObject startWaveButton;

    // BUILD UI (магазин)
    [SerializeField] private GameObject wallHandPanel;   // "WallHandPanel"
    [SerializeField] private GameObject buttonControls;  // "ButtonControls" (RefreshButton внутри)

    private CastleHealth castle;
    private WaveController waves;
    private WallHandManager hand;
    private EnclosureDebug enclosure;

    private bool initialized;
    private bool triedFindUi;

    private void Awake()
    {
        if (hudText == null)
            hudText = GetComponent<TMP_Text>();

        if (hudText != null)
        {
            hudText.richText = false;
            hudText.text = "";
        }
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
        if (hudText == null) return;

        if (!initialized)
        {
            hudText.text = "";
            initialized = true;
        }

        if (GameState.IsGameOver)
        {
            if (startWaveButton != null) startWaveButton.SetActive(false);
            if (wallHandPanel != null) wallHandPanel.SetActive(false);
            if (buttonControls != null) buttonControls.SetActive(false);

            hudText.text = "GAME OVER";
            return;
        }

        if (castle == null) castle = FindFirstObjectByType<CastleHealth>();
        if (waves == null) waves = FindFirstObjectByType<WaveController>();
        if (hand == null) hand = FindFirstObjectByType<WallHandManager>();
        if (enclosure == null) enclosure = FindFirstObjectByType<EnclosureDebug>();

        if (castle == null || waves == null) return;

        TryFindUiOnce();

        bool isBuild = waves.CurrentPhase == WaveController.Phase.Build;

        if (startWaveButton != null)
            startWaveButton.SetActive(isBuild);

        // FIX: скрываем магазин в COMBAT
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
