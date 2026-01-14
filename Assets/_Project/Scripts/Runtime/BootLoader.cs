using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BootLoader : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "MainMenu";

    private void Awake()
    {
        Application.targetFrameRate = 60;
        DontDestroyOnLoad(gameObject);
        SceneManager.LoadScene(nextSceneName);
    }
}
