using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen buttons.
public class MainMenu : MonoBehaviour
{
    [SerializeField] Button startButton;
    [SerializeField] Button quitButton;
    [SerializeField] string arenaScene = "Arena";

    void Awake()
    {
        TimeControl.ResetAll();
        if (startButton != null) startButton.onClick.AddListener(StartMatch);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
    }

    public void StartMatch()
    {
        SceneManager.LoadScene(arenaScene);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
