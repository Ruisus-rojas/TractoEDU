using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PrototypeInteractionHUD : MonoBehaviour
{
    [SerializeField] private RectTransform reticle;
    [SerializeField] private Image[] reticleMarks;
    [SerializeField] private GameObject informationPanel;
    [SerializeField] private Text nameText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Color idleReticleColor = Color.white;
    [SerializeField] private Color targetReticleColor = new Color(1f, 0.82f, 0.28f, 1f);
    [SerializeField, Min(1f)] private float targetReticleScale = 1.25f;

    private Interactable currentTarget;
    private Text hoverNameLabel;
    private Text exitPromptLabel;
    private bool isInspecting;

    private void Awake()
    {
        EnsureRuntimeLabels();
        HideInformation();
        SetTarget(null);
    }

    public void Configure(RectTransform reticleTransform, Image[] marks, GameObject panel, Text title, Text description)
    {
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

    public void ShowInspection(Interactable target)
    {
        if (target == null) return;
        isInspecting = true;
        if (nameText != null) nameText.text = target.DisplayName;
        if (descriptionText != null) descriptionText.text = target.Description;
        if (informationPanel != null) informationPanel.SetActive(true);
        if (hoverNameLabel != null) hoverNameLabel.gameObject.SetActive(false);
        if (exitPromptLabel != null) exitPromptLabel.gameObject.SetActive(true);
    }

    public void ShowInformation(Interactable target)
    {
        ShowInspection(target);
    }

    public void EndInspection()
    {
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
        if (informationPanel != null) informationPanel.SetActive(false);
        if (exitPromptLabel != null) exitPromptLabel.gameObject.SetActive(false);
    }

    private void EnsureRuntimeLabels()
    {
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