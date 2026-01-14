using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    [SerializeField] private GameObject gameOverPanel;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    public void Show()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
    }

    // Привяжем к кнопке Restart
    public void Restart()
    {
        GameState.Reset();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Привяжем к кнопке Quit
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
