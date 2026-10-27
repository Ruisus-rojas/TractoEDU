using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
// Controla el menú principal, las instrucciones y la transición a la escena de exploración.
public sealed class MainMenuController : MonoBehaviour
{
    // Impide que la protección de inicio devuelva al menú cuando el usuario sí eligió explorar.
    private static bool allowExplorationSceneFromMenu;

    // Referencias visuales asignadas en la escena; Explore puede localizarse automáticamente.
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject instructionsPanel;
    [SerializeField] private UnityEngine.UI.Button startButton;
    [SerializeField] private UnityEngine.UI.Button continueButton;
    [SerializeField] private UnityEngine.UI.Button exploreButton;
    [SerializeField] private string explorationSceneName = "PrototypeThirdPerson";

    public void Configure(GameObject menu, GameObject instructions, UnityEngine.UI.Button start, UnityEngine.UI.Button continueButton, string sceneName)
    {
        // Permite conectar los paneles y botones mediante otro configurador.
        mainPanel = menu;
        instructionsPanel = instructions;
        startButton = start;
        this.continueButton = continueButton;
        explorationSceneName = sceneName;
    }

    private void Awake()
    {
        // Corrige sistemas de eventos duplicados y deja el cursor visible en el menú.
        EnsureSingleEventSystem();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterStartupSceneGuard()
    {
        // Registra la protección antes de cargar cualquier escena de esta sesión.
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // La escena de exploración solo se mantiene si fue solicitada desde el menú.
        EnsureSingleEventSystem();
        if (scene.name != "PrototypeThirdPerson") return;
        if (allowExplorationSceneFromMenu)
        {
            allowExplorationSceneFromMenu = false;
            return;
        }
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }

    private void OnEnable()
    {
        // Encuentra el botón Explorar si falta la referencia y conecta los eventos sin duplicarlos.
        if (exploreButton == null && mainPanel != null)
        {
            foreach (UnityEngine.UI.Button candidate in mainPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (candidate.gameObject.name == "Button_Explore")
                {
                    exploreButton = candidate;
                    break;
                }
            }
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(ShowInstructions);
            startButton.onClick.AddListener(ShowInstructions);
        }
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ShowMainMenu);
            continueButton.onClick.AddListener(ShowMainMenu);
        }
        if (exploreButton != null)
        {
            exploreButton.onClick.RemoveListener(ContinueToExploration);
            exploreButton.onClick.AddListener(ContinueToExploration);
        }
    }

    private void OnDisable()
    {
        // Desconecta eventos al deshabilitar el controlador para evitar llamadas repetidas.
        if (startButton != null) startButton.onClick.RemoveListener(ShowInstructions);
        if (continueButton != null) continueButton.onClick.RemoveListener(ShowMainMenu);
        if (exploreButton != null) exploreButton.onClick.RemoveListener(ContinueToExploration);
    }

    private void Start()
    {
        // Muestra el menú inicial y selecciona un botón para navegación con teclado/control.
        ShowMainMenu();
        if (EventSystem.current != null)
        {
            GameObject firstChoice = exploreButton != null ? exploreButton.gameObject : startButton != null ? startButton.gameObject : null;
            if (firstChoice != null) EventSystem.current.SetSelectedGameObject(firstChoice);
        }
    }

    public static void EnsureSingleEventSystem()
    {
        // Mantiene un solo EventSystem activo y garantiza un módulo de entrada compatible con Input System.
        EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem preferred = EventSystem.current;
        if (preferred == null || !preferred.isActiveAndEnabled)
            preferred = null;
        if (preferred == null && systems.Length > 0) preferred = systems[0];

        if (preferred == null)
        {
            preferred = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        }

        preferred.gameObject.SetActive(true);
        preferred.enabled = true;

        foreach (EventSystem system in systems)
        {
            if (system == preferred || system == null) continue;
            system.enabled = false;
            Destroy(system.gameObject);
        }

        BaseInputModule inputModule = preferred.GetComponent<BaseInputModule>();
        if (inputModule == null)
        {
            InputSystemUIInputModule inputSystemModule = preferred.gameObject.AddComponent<InputSystemUIInputModule>();
            inputSystemModule.AssignDefaultActions();
        }
        else inputModule.enabled = true;
    }

    public void ShowMainMenu()
    {
        // Muestra el panel principal y oculta las instrucciones.
        if (mainPanel != null) mainPanel.SetActive(true);
        if (instructionsPanel != null) instructionsPanel.SetActive(false);
    }

    public void ShowInstructions()
    {
        // Cambia del panel principal a las instrucciones.
        if (mainPanel != null) mainPanel.SetActive(false);
        if (instructionsPanel != null) instructionsPanel.SetActive(true);
    }

    public void ContinueToExploration()
    {
        // Marca la transición como autorizada y carga el nivel interactivo.
        if (string.IsNullOrWhiteSpace(explorationSceneName))
        {
            Debug.LogError("MainMenuController needs an exploration scene name.", this);
            return;
        }
        allowExplorationSceneFromMenu = true;
        SceneManager.LoadScene(explorationSceneName, LoadSceneMode.Single);
    }
}
