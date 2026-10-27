using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
// Presenta el retículo, nombre al apuntar e información durante la inspección.
public sealed class PrototypeInteractionHUD : MonoBehaviour
{
    // Referencias visuales configurables desde la escena.
    [SerializeField] private RectTransform reticle;
    [SerializeField] private Image[] reticleMarks;
    [SerializeField] private GameObject informationPanel;
    [SerializeField] private Text nameText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Color idleReticleColor = Color.white;
    [SerializeField] private Color targetReticleColor = new Color(1f, 0.82f, 0.28f, 1f);
    [SerializeField, Min(1f)] private float targetReticleScale = 1.25f;

    // Guarda el objetivo actual y las etiquetas que el HUD crea si no existen en la escena.
    private Interactable currentTarget;
    private Text hoverNameLabel;
    private Text exitPromptLabel;
    private bool isInspecting;

    private void Awake()
    {
        // Prepara etiquetas de uso, oculta paneles y empieza sin objeto seleccionado.
        EnsureRuntimeLabels();
        HideInformation();
        SetTarget(null);
    }

    public void Configure(RectTransform reticleTransform, Image[] marks, GameObject panel, Text title, Text description)
    {
        // Permite que otro componente conecte los elementos visuales en ejecución.
        reticle = reticleTransform;
        reticleMarks = marks;
        informationPanel = panel;
        nameText = title;
        descriptionText = description;
        EnsureRuntimeLabels();
        HideInformation();
        SetTarget(null);
    }

    public void SetTarget(Interactable target)
    {
        // Cambia color/tamaño del retículo y muestra el nombre solo durante exploración.
        currentTarget = target;
        bool hasTarget = target != null;

        if (reticle != null) reticle.localScale = hasTarget ? Vector3.one * targetReticleScale : Vector3.one;
        if (reticleMarks != null)
        {
            Color color = hasTarget ? targetReticleColor : idleReticleColor;
            foreach (Image mark in reticleMarks)
                if (mark != null) mark.color = color;
        }

        if (hoverNameLabel != null)
        {
            hoverNameLabel.text = hasTarget ? target.DisplayName : string.Empty;
            hoverNameLabel.gameObject.SetActive(hasTarget && !isInspecting);
        }
    }

    public void SetDashboardHoverTarget(Interactable target)
    {
        // Muestra el nombre del mando bajo el cursor aunque siga abierta la vista del tablero.
        SetTarget(target);
        if (hoverNameLabel == null) return;

        hoverNameLabel.text = target != null ? target.DisplayName : string.Empty;
        hoverNameLabel.gameObject.SetActive(target != null && isInspecting);
    }

    public void ShowInspection(Interactable target)
    {
        // Carga los datos de la pieza en el panel y muestra la instrucción para salir con Q.
        if (target == null) return;
        isInspecting = true;
        if (nameText != null) nameText.text = target.DisplayName;
        if (descriptionText != null)
        {
            descriptionText.text = target.Description;
            ResizeInformationPanelToFitDescription();
        }
        if (informationPanel != null) informationPanel.SetActive(true);
        if (hoverNameLabel != null) hoverNameLabel.gameObject.SetActive(false);
        if (exitPromptLabel != null) exitPromptLabel.gameObject.SetActive(true);
    }

    public void ShowInformation(Interactable target)
    {
        // Alias conservado para llamadas anteriores; usa el mismo flujo de inspección.
        ShowInspection(target);
    }

    public void EndInspection()
    {
        // Cierra la ficha y vuelve a mostrar el nombre si aún hay un objetivo apuntado.
        isInspecting = false;
        HideInformation();
        if (exitPromptLabel != null) exitPromptLabel.gameObject.SetActive(false);
        if (hoverNameLabel != null)
        {
            hoverNameLabel.text = currentTarget != null ? currentTarget.DisplayName : string.Empty;
            hoverNameLabel.gameObject.SetActive(currentTarget != null);
        }
    }

    public void HideInformation()
    {
        // Oculta panel y aviso de salida sin cambiar el objetivo actual.
        if (informationPanel != null) informationPanel.SetActive(false);
        if (exitPromptLabel != null) exitPromptLabel.gameObject.SetActive(false);
    }

    private void ResizeInformationPanelToFitDescription()
    {
        // La ficha original tenía un espacio de una sola línea; mide el texto y amplía el panel.
        if (descriptionText == null || informationPanel == null) return;

        RectTransform panelRect = informationPanel.GetComponent<RectTransform>();
        RectTransform descriptionRect = descriptionText.rectTransform;
        if (panelRect == null || descriptionRect == null) return;

        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionText.verticalOverflow = VerticalWrapMode.Overflow;

        const float topPadding = 17f;
        const float titleHeight = 32f;
        const float titleDescriptionGap = 8f;
        const float bottomPadding = 16f;

        descriptionRect.anchoredPosition = new Vector2(
            descriptionRect.anchoredPosition.x,
            -(topPadding + titleHeight + titleDescriptionGap));

        Canvas.ForceUpdateCanvases();
        float descriptionHeight = Mathf.Max(titleHeight, descriptionText.preferredHeight);
        descriptionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, descriptionHeight);
        panelRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            topPadding + titleHeight + titleDescriptionGap + descriptionHeight + bottomPadding);
    }

    private void EnsureRuntimeLabels()
    {
        // Busca o crea las etiquetas flotantes requeridas y define el texto de salida.
        if (hoverNameLabel == null)
            hoverNameLabel = FindOrCreateLabel("HoverNameLabel", 20, new Vector2(0f, -145f), new Vector2(260f, 36f), false);
        if (exitPromptLabel == null)
            exitPromptLabel = FindOrCreateLabel("InspectionExitPrompt", 17, new Vector2(0f, -28f), new Vector2(320f, 34f), true);

        if (hoverNameLabel != null) hoverNameLabel.gameObject.SetActive(false);
        if (exitPromptLabel != null)
        {
            exitPromptLabel.text = "Presiona Q para salir";
            exitPromptLabel.gameObject.SetActive(false);
        }
    }

    private Text FindOrCreateLabel(string objectName, int fontSize, Vector2 anchoredPosition, Vector2 size, bool topAnchored)
    {
        // Reutiliza un Text existente o crea uno; luego fija su posición y estilo en el Canvas.
        Transform existing = transform.Find(objectName);
        Text label = existing != null ? existing.GetComponent<Text>() : null;
        if (label == null)
        {
            GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(transform, false);
            label = labelObject.GetComponent<Text>();
        }

        RectTransform rect = label.rectTransform;
        rect.anchorMin = topAnchored ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = topAnchored ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        if (label.font == null && nameText != null) label.font = nameText.font;
        if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        Shadow shadow = label.GetComponent<Shadow>();
        if (shadow == null) shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return label;
    }
}
