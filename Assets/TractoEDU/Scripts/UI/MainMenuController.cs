using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject instructionsPanel;
    [SerializeField] private UnityEngine.UI.Button startButton;
    [SerializeField] private UnityEngine.UI.Button continueButton;
    [SerializeField] private string explorationSceneName = "PrototypeThirdPerson";

    public void Configure(GameObject menu, GameObject instructions, UnityEngine.UI.Button start, UnityEngine.UI.Button continueButton, string sceneName)
    {
        mainPanel = menu;
        instructionsPanel = instructions;
        startButton = start;
        this.continueButton = continueButton;
        explorationSceneName = sceneName;
    }

    private void OnEnable()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(ShowInstructions);
            startButton.onClick.AddListener(ShowInstructions);
        }
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueToExploration);
            continueButton.onClick.AddListener(ContinueToExploration);
        }
    }

    private void OnDisable()
    {
        if (startButton != null) startButton.onClick.RemoveListener(ShowInstructions);
        if (continueButton != null) continueButton.onClick.RemoveListener(ContinueToExploration);
    }

    private void Start()
    {
        ShowMainMenu();
        if (EventSystem.current != null && startButton != null)
        {
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }
    }

    public void ShowMainMenu()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (instructionsPanel != null) instructionsPanel.SetActive(false);
    }

    public void ShowInstructions()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (instructionsPanel != null) instructionsPanel.SetActive(true);
    }

    public void ContinueToExploration()
    {
        if (string.IsNullOrWhiteSpace(explorationSceneName))
        {
            Debug.LogError("MainMenuController needs an exploration scene name.", this);
            return;
        }
        SceneManager.LoadScene(explorationSceneName, LoadSceneMode.Single);
    }
}