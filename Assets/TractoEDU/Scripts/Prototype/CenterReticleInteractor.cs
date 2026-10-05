using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class CenterReticleInteractor : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private PrototypeInteractionHUD hud;
    [SerializeField] private PrototypeFirstPersonCameraController cameraController;
    [SerializeField] private PrototypePlayerMovementController playerMovement;
    [SerializeField, Min(0.1f)] private float interactionDistance = 4.5f;
    [SerializeField] private LayerMask interactionLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.5f)] private float inspectionDistance = 2.5f;
    [SerializeField, Min(0f)] private float inspectionHeight = 0.2f;
    [SerializeField, Min(0f)] private float inspectionTransitionDuration = 0.3f;

    private Interactable currentTarget;
    private Interactable inspectedTarget;
    private bool isInspecting;

    public float InteractionDistance => interactionDistance;
    public Interactable CurrentTarget => currentTarget;
    public Interactable InspectedTarget => inspectedTarget;
    public bool IsInspecting => isInspecting;

    private void Awake()
    {
        if (viewCamera == null) viewCamera = GetComponent<Camera>();
        if (hud == null) hud = FindFirstObjectByType<PrototypeInteractionHUD>();
        if (cameraController == null) cameraController = GetComponent<PrototypeFirstPersonCameraController>();
        if (playerMovement == null) playerMovement = GetComponentInParent<PrototypePlayerMovementController>();
        if (hud != null) hud.SetTarget(null);
    }

    private void Update()
    {
        if (isInspecting)
        {
            if (inspectedTarget == null || !inspectedTarget.enabled || !inspectedTarget.gameObject.activeInHierarchy)
            {
                EndInspection();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame) EndInspection();
            return;
        }

        if (viewCamera == null)
        {
            SetCurrentTarget(null);
            return;
        }

        Ray centerRay = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Interactable target = null;
        if (Physics.Raycast(centerRay, out RaycastHit hit, interactionDistance, interactionLayers, QueryTriggerInteraction.Ignore))
        {
            target = hit.collider.GetComponentInParent<Interactable>();
            if (target != null && (!target.enabled || !target.gameObject.activeInHierarchy)) target = null;
        }

        SetCurrentTarget(target);
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryInteractWithCurrentTarget();
    }

    private void OnDisable()
    {
        if (isInspecting) EndInspection();
        SetCurrentTarget(null);
        if (hud != null) hud.SetTarget(null);
    }

    private void SetCurrentTarget(Interactable target)
    {
        if (currentTarget == target) return;
        if (currentTarget != null) currentTarget.SetHighlighted(false);
        currentTarget = target;
        if (currentTarget != null) currentTarget.SetHighlighted(true);
        if (hud != null) hud.SetTarget(currentTarget);
    }

    public bool TryInteractWithCurrentTarget()
    {
        if (isInspecting || currentTarget == null || hud == null) return false;

        inspectedTarget = currentTarget;
        isInspecting = true;
        if (playerMovement != null) playerMovement.SetInspectionLocked(true);
        if (cameraController != null)
            cameraController.BeginInspection(inspectedTarget, inspectionDistance, inspectionHeight, inspectionTransitionDuration);
        hud.ShowInspection(inspectedTarget);
        return true;
    }

    public void EndInspection()
    {
        if (!isInspecting) return;

        Interactable previousTarget = inspectedTarget;
        isInspecting = false;
        inspectedTarget = null;
        if (cameraController != null) cameraController.EndInspection();
        if (playerMovement != null) playerMovement.SetInspectionLocked(false);
        if (hud != null) hud.EndInspection();
        if (previousTarget != null) previousTarget.SetHighlighted(false);
        currentTarget = null;
        if (hud != null) hud.SetTarget(null);
    }

    public void Configure(Camera cameraToUse, PrototypeInteractionHUD hudToUse, float maxDistance)
    {
        SetCurrentTarget(null);
        viewCamera = cameraToUse;
        hud = hudToUse;
        interactionDistance = Mathf.Max(0.1f, maxDistance);
        if (hud != null) hud.SetTarget(null);
    }
}