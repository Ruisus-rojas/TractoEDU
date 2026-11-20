using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
    private GameObject coverRoot;
    private GameObject settingsRoot;
    private UnityEngine.UI.Button coverStartButton;

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
        // Carga la portada suministrada si está disponible y selecciona el inicio.
        BuildReferenceCover();
        ShowMainMenu();
        if (EventSystem.current != null)
        {
            GameObject firstChoice = coverStartButton != null ? coverStartButton.gameObject :
                exploreButton != null ? exploreButton.gameObject : startButton != null ? startButton.gameObject : null;
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
        if (mainPanel != null) mainPanel.SetActive(coverRoot == null);
        if (instructionsPanel != null) instructionsPanel.SetActive(false);
        if (coverRoot != null) coverRoot.SetActive(true);
        if (settingsRoot != null) settingsRoot.SetActive(false);
    }

    public void ShowSettings()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (instructionsPanel != null) instructionsPanel.SetActive(false);
        if (coverRoot != null) coverRoot.SetActive(false);
        if (settingsRoot == null) BuildSettingsPanel();
        if (settingsRoot != null) settingsRoot.SetActive(true);
    }

    public void ShowInstructions()
    {
        // Cambia del panel principal a las instrucciones.
        if (mainPanel != null) mainPanel.SetActive(false);
        if (coverRoot != null) coverRoot.SetActive(false);
        if (settingsRoot != null) settingsRoot.SetActive(false);
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

    private void BuildReferenceCover()
    {
        if (mainPanel == null || coverRoot != null) return;
        TextAsset coverBytes = Resources.Load<TextAsset>("TractoEDU_MenuPortada");
        if (coverBytes == null || coverBytes.bytes.Length == 0) return;

        Texture2D coverTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!coverTexture.LoadImage(coverBytes.bytes))
        {
            Destroy(coverTexture);
            return;
        }

        Sprite coverSprite = Sprite.Create(coverTexture,
            new Rect(0f, 0f, coverTexture.width, coverTexture.height), new Vector2(0.5f, 0.5f));
        mainPanel.SetActive(false);
        coverRoot = new GameObject("TractoEDUReferenceCover", typeof(RectTransform), typeof(Image));
        coverRoot.transform.SetParent(mainPanel.transform.parent, false);
        RectTransform coverRect = coverRoot.GetComponent<RectTransform>();
        coverRect.anchorMin = Vector2.zero;
        coverRect.anchorMax = Vector2.one;
        coverRect.offsetMin = Vector2.zero;
        coverRect.offsetMax = Vector2.zero;
        Image coverImage = coverRoot.GetComponent<Image>();
        coverImage.sprite = coverSprite;
        coverImage.preserveAspect = false;
        coverImage.raycastTarget = false;

        // La imagen de referencia ya incluye las etiquetas de los cuatro botones;
        // estos botones transparentes conservan la portada y añaden acciones reales.
        coverStartButton = CreateCoverHotspot("EmpezarSimulacion", 0.81f, 0.612f, ContinueToExploration);
        CreateCoverHotspot("AjustesSimulador", 0.81f, 0.505f, ShowSettings);
        CreateCoverHotspot("GuiaComponentes", 0.81f, 0.399f, ShowInstructions);
        CreateCoverHotspot("SalirPrograma", 0.81f, 0.295f, Application.Quit);
    }

    private Button CreateCoverHotspot(string objectName, float anchorX, float anchorY, UnityEngine.Events.UnityAction action)
    {
        GameObject hotspot = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        hotspot.transform.SetParent(coverRoot.transform, false);
        RectTransform rect = hotspot.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(anchorX, anchorY);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(580f, 88f);

        Image image = hotspot.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        Button button = hotspot.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = new Color(1f, 1f, 1f, 0f),
            highlightedColor = new Color(1f, 1f, 1f, 0.12f),
            pressedColor = new Color(1f, 1f, 1f, 0.2f),
            selectedColor = new Color(1f, 1f, 1f, 0.08f),
            disabledColor = new Color(1f, 1f, 1f, 0f),
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };
        button.onClick.AddListener(action);
        return button;
    }

    private void BuildSettingsPanel()
    {
        if (mainPanel == null) return;
        settingsRoot = new GameObject("CoverSettingsPanel", typeof(RectTransform), typeof(Image));
        settingsRoot.transform.SetParent(mainPanel.transform.parent, false);
        RectTransform rootRect = settingsRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        settingsRoot.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.08f, 0.94f);

        CreateSettingsLabel(settingsRoot.transform, "SettingsTitle", "AJUSTES", 44,
            new Vector2(0f, 65f), new Vector2(760f, 70f), TextAnchor.MiddleCenter);
        CreateSettingsButton(settingsRoot.transform, "BackToCover", "VOLVER",
            Vector2.zero, ShowMainMenu);
    }

    private static Text CreateSettingsLabel(Transform parent, string objectName, string value, int fontSize,
        Vector2 position, Vector2 size, TextAnchor alignment)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = value;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static void CreateSettingsButton(Transform parent, string objectName, string label,
        Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 64f);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.83f, 0.35f, 0.08f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        CreateSettingsLabel(buttonObject.transform, "Label", label, 21, Vector2.zero,
            new Vector2(340f, 54f), TextAnchor.MiddleCenter);
    }
}
