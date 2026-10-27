using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
// Pausa la exploración y crea en ejecución un menú para continuar o volver al menú principal.
public sealed class PrototypePauseMenu : MonoBehaviour
{
    // Referencias al control de cámara/interacción y nombre de la escena inicial.
    [SerializeField] private PrototypeFirstPersonCameraController cameraController;
    [SerializeField] private CenterReticleInteractor interactor;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // Elementos del Canvas creado por código y estado necesario para restaurar el tiempo.
    private Canvas pauseCanvas;
    private GameObject continueButtonObject;
    private bool isPaused;
    private float previousTimeScale = 1f;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        // Completa dependencias, garantiza un EventSystem y construye la interfaz de pausa.
        if (cameraController == null) cameraController = GetComponent<PrototypeFirstPersonCameraController>();
        if (interactor == null) interactor = GetComponent<CenterReticleInteractor>();
        MainMenuController.EnsureSingleEventSystem();
        CreatePauseInterface();
    }

    private void Update()
    {
        // Escape alterna pausa/reanudación; si había una inspección abierta, primero la cierra.
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

        if (isPaused)
        {
            ResumeExploration();
            return;
        }

        if (interactor != null && interactor.IsInspecting) interactor.EndInspection();
        PauseExploration();
    }

    private void OnDestroy()
    {
        // Evita dejar el juego congelado si este objeto se destruye estando pausado.
        if (isPaused) Time.timeScale = 1f;
    }

    public void PauseExploration()
    {
        // Congela el tiempo y suspende cámara/retículo, conservando la escala anterior del juego.
        if (isPaused) return;

        if (interactor != null && interactor.IsInspecting) interactor.EndInspection();
        previousTimeScale = Time.timeScale;
        isPaused = true;
        Time.timeScale = 0f;
        if (interactor != null) interactor.SetInteractionSuspended(true);
        if (cameraController != null) cameraController.SetPaused(true);
        if (pauseCanvas != null) pauseCanvas.gameObject.SetActive(true);
        if (EventSystem.current != null && continueButtonObject != null)
            EventSystem.current.SetSelectedGameObject(continueButtonObject);
    }

    public void ResumeExploration()
    {
        // Restaura tiempo y controles de exploración guardados al pausar.
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = previousTimeScale;
        if (pauseCanvas != null) pauseCanvas.gameObject.SetActive(false);
        if (interactor != null) interactor.SetInteractionSuspended(false);
        if (cameraController != null) cameraController.SetPaused(false);
    }

    public void ReturnToMainMenu()
    {
        // Restablece el tiempo antes de cambiar de escena para no heredar una pausa.
        Time.timeScale = 1f;
        if (cameraController != null) cameraController.SetPaused(true);
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogError("PrototypePauseMenu needs a main menu scene name.", this);
            return;
        }
        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private void CreatePauseInterface()
    {
        // Construye un Canvas superpuesto con fondo, panel, título y botones conectados.
        GameObject canvasObject = new GameObject("PauseMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        pauseCanvas = canvasObject.GetComponent<Canvas>();
        pauseCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pauseCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform overlay = CreateRect("Overlay", canvasObject.transform);
        StretchToParent(overlay);
        Image overlayImage = overlay.gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0.015f, 0.025f, 0.02f, 0.78f);

        RectTransform panel = CreateRect("PausePanel", overlay);
        SetCenteredRect(panel, Vector2.zero, new Vector2(480f, 330f));
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.11f, 0.16f, 0.12f, 1f);

        CreateLabel("Title", panel, "Pausa", Vector2.up * 105f, new Vector2(400f, 56f), 34, FontStyle.Bold, Color.white);
        continueButtonObject = CreateButton("ContinueButton", panel, "Continuar", Vector2.up * 20f, ResumeExploration);
        CreateButton("MainMenuButton", panel, "Volver al menú principal", Vector2.down * 62f, ReturnToMainMenu);

        pauseCanvas.gameObject.SetActive(false);
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        // Crea un RectTransform hijo para estructurar los elementos de la interfaz.
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void StretchToParent(RectTransform rect)
    {
        // Hace que el rectángulo ocupe todo el espacio de su elemento padre.
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        // Centra el rectángulo y aplica posición/tamaño en coordenadas del Canvas.
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Text CreateLabel(string objectName, Transform parent, string value, Vector2 position, Vector2 size, int fontSize, FontStyle style, Color color)
    {
        // Añade un texto sencillo al panel con el estilo y la posición indicados.
        RectTransform rect = CreateRect(objectName, parent);
        SetCenteredRect(rect, position, size);
        rect.gameObject.AddComponent<CanvasRenderer>();
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateButton(string objectName, Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        // Construye un botón uGUI completo y conecta su evento onClick.
        RectTransform rect = CreateRect(objectName, parent);
        SetCenteredRect(rect, position, new Vector2(360f, 58f));
        rect.gameObject.AddComponent<CanvasRenderer>();
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.78f, 0.38f, 0.12f, 1f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        RectTransform labelRect = CreateRect("Label", rect);
        StretchToParent(labelRect);
        labelRect.gameObject.AddComponent<CanvasRenderer>();
        Text text = labelRect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        text.fontSize = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return rect.gameObject;
    }
}
