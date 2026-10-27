using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
// Coordina el raycast del retículo y conecta selección, cámara, jugador e interfaz.
public sealed class CenterReticleInteractor : MonoBehaviour
{
    // La excepción del asiento permite buscar una pieza permitida detrás de su collider.
    private const string SeatDisplayName = "Asiento del conductor";
    private const string TractorDisplayName = "Tractor TractoEDU";
    // Referencias a los sistemas que participan en la interacción; Awake completa las que falten.
    [SerializeField] private Camera viewCamera;
    [SerializeField] private PrototypeInteractionHUD hud;
    [SerializeField] private PrototypeFirstPersonCameraController cameraController;
    [SerializeField] private PrototypePlayerMovementController playerMovement;
    [SerializeField, Min(0.1f)] private float interactionDistance = 4.5f;
    [SerializeField] private LayerMask interactionLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.5f)] private float inspectionDistance = 2.5f;
    [SerializeField, Min(0f)] private float inspectionHeight = 0.2f;
    [SerializeField, Min(0f)] private float inspectionTransitionDuration = 0.3f;

    // currentTarget es lo que señala el retículo; inspectedTarget es lo abierto con clic.
    private Interactable currentTarget;
    private Interactable inspectedTarget;
    private Interactable dashboardFocusTarget;
    private bool isInspecting;
    private bool interactionSuspended;
    private bool suppressClickUntilRelease = true;

    // Propiedades de solo lectura para que otros scripts consulten el estado sin modificarlo.
    public float InteractionDistance => interactionDistance;
    public Interactable CurrentTarget => currentTarget;
    public Interactable InspectedTarget => inspectedTarget;
    public bool IsInspecting => isInspecting;

    private void Awake()
    {
        // Busca dependencias en la cámara, su padre o la escena si no se asignaron en el Inspector.
        if (viewCamera == null) viewCamera = GetComponent<Camera>();
        if (hud == null) hud = FindFirstObjectByType<PrototypeInteractionHUD>();
        if (cameraController == null) cameraController = GetComponent<PrototypeFirstPersonCameraController>();
        if (playerMovement == null) playerMovement = GetComponentInParent<PrototypePlayerMovementController>();
        if (hud != null) hud.SetTarget(null);
    }

    private void Update()
    {
        // El cursor puede apuntar a mandos individuales mientras el tablero está ampliado.
        if (isInspecting)
        {
            if (inspectedTarget == null || !inspectedTarget.enabled || !inspectedTarget.gameObject.activeInHierarchy)
            {
                EndInspection();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                if (dashboardFocusTarget != null && inspectedTarget != dashboardFocusTarget)
                    ReturnToDashboardOverview();
                else
                {
                    dashboardFocusTarget = null;
                    EndInspection();
                }
                return;
            }

            if (IsDashboardOverviewActive())
            {
                UpdateDashboardPointerTarget();
                Mouse dashboardMouse = Mouse.current;
                if (dashboardMouse != null && dashboardMouse.leftButton.wasPressedThisFrame)
                    TryInspectDashboardComponent(currentTarget);
            }
            return;
        }

        if (interactionSuspended || (cameraController != null && !cameraController.IsCursorCaptured))
        {
            SetCurrentTarget(null);
            return;
        }

        if (viewCamera == null)
        {
            SetCurrentTarget(null);
            return;
        }

        // El rayo nace en el centro exacto de la vista y respeta distancia, capas y triggers.
        Ray centerRay = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Interactable target = null;
        if (Physics.Raycast(centerRay, out RaycastHit hit, interactionDistance, interactionLayers, QueryTriggerInteraction.Collide))
        {
            target = hit.collider.GetComponentInParent<Interactable>();
            if (target != null && (!target.enabled || !target.gameObject.activeInHierarchy)) target = null;
            if (target != null && target.DisplayName == SeatDisplayName)
                target = FindTargetThroughAllowedOccluder(centerRay, hit);
        }

        SetCurrentTarget(target);
        Mouse mouse = Mouse.current;
        if (suppressClickUntilRelease)
        {
            if (mouse == null || !mouse.leftButton.isPressed) suppressClickUntilRelease = false;
            return;
        }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) TryInteractWithCurrentTarget();
    }

    private Interactable FindTargetThroughAllowedOccluder(Ray centerRay, RaycastHit nearestHit)
    {
        // Solo permite atravesar el collider más cercano si el objeto candidato lo autorizó explícitamente.
        RaycastHit[] hits = Physics.RaycastAll(centerRay, interactionDistance, interactionLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Interactable nearestInteractable = nearestHit.collider.GetComponentInParent<Interactable>();
        foreach (RaycastHit candidateHit in hits)
        {
            if (candidateHit.collider == nearestHit.collider) continue;

            Interactable candidate = candidateHit.collider.GetComponentInParent<Interactable>();
            if (candidate == nearestInteractable) continue;
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy) return nearestInteractable;

            return candidate.CanBeSelectedThrough(nearestHit.collider) ? candidate : nearestInteractable;
        }

        return nearestInteractable;
    }

    private void OnDisable()
    {
        // Limpia estado visual y restaura cámara/jugador si el componente se desactiva inspeccionando.
        if (isInspecting) EndInspection();
        SetCurrentTarget(null);
        if (hud != null) hud.SetTarget(null);
    }

    private void SetCurrentTarget(Interactable target)
    {
        // Mantiene sincronizados resaltado y nombre, evitando trabajo si el objetivo no cambió.
        if (currentTarget == target) return;
        if (currentTarget != null) currentTarget.SetHighlighted(false);
        currentTarget = target;
        if (currentTarget != null) currentTarget.SetHighlighted(true);
        if (hud == null) return;

        if (IsDashboardOverviewActive()) hud.SetDashboardHoverTarget(currentTarget);
        else hud.SetTarget(currentTarget);
    }

    public bool TryInteractWithCurrentTarget()
    {
        // Valida el estado antes de iniciar inspección y notifica a los otros sistemas.
        if (isInspecting || interactionSuspended || (cameraController != null && !cameraController.IsCursorCaptured) || currentTarget == null || hud == null) return false;

        inspectedTarget = currentTarget;
        dashboardFocusTarget = currentTarget.DisplayName == "Tablero" ? currentTarget : null;
        isInspecting = true;
        if (playerMovement != null) playerMovement.SetInspectionLocked(true);
        if (cameraController != null)
            cameraController.BeginInspection(inspectedTarget, inspectionDistance, inspectionHeight, inspectionTransitionDuration);
        hud.ShowInspection(inspectedTarget);
        return true;
    }

    private bool IsDashboardOverviewActive()
    {
        return isInspecting && dashboardFocusTarget != null && inspectedTarget == dashboardFocusTarget &&
               cameraController != null && cameraController.IsDashboardFocusActive;
    }

    private void UpdateDashboardPointerTarget()
    {
        if (viewCamera == null || Mouse.current == null)
        {
            SetCurrentTarget(null);
            return;
        }

        Ray pointerRay = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(pointerRay, interactionDistance, interactionLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Interactable candidate = hit.collider.GetComponentInParent<Interactable>();
            if (candidate != null && candidate.IsDashboardComponent && candidate.enabled && candidate.gameObject.activeInHierarchy)
            {
                SetCurrentTarget(candidate);
                return;
            }

            // El tablero y el volante oculto son excepciones específicas del modo ampliado.
            if (candidate == dashboardFocusTarget || IsHiddenSteeringWheel(candidate) || IsTractorRootOccluder(candidate)) continue;

            // Una superficie opaca ajena a estos controles sigue bloqueando la selección.
            SetCurrentTarget(null);
            return;
        }

        SetCurrentTarget(null);
    }

    private bool IsHiddenSteeringWheel(Interactable candidate)
    {
        if (candidate == null || cameraController == null || !cameraController.IsDashboardFocusActive ||
            candidate.DisplayName != "Volante") return false;

        foreach (Renderer wheelRenderer in candidate.GetComponentsInChildren<Renderer>(true))
            if (wheelRenderer != null && wheelRenderer.enabled) return false;
        return true;
    }

    private bool IsTractorRootOccluder(Interactable candidate)
    {
        // El collider envolvente del tractor puede tapar mandos del tablero desde dentro de la cabina.
        // Solo se atraviesa en el enfoque del tablero; cualquier otro obstáculo conserva su bloqueo.
        return candidate != null && cameraController != null && cameraController.IsDashboardFocusActive &&
               candidate.DisplayName == TractorDisplayName;
    }

    private bool TryInspectDashboardComponent(Interactable target)
    {
        if (!IsDashboardOverviewActive() || target == null || !target.IsDashboardComponent) return false;

        inspectedTarget = target;
        if (hud != null) hud.ShowInspection(target);
        return true;
    }

    private void ReturnToDashboardOverview()
    {
        if (inspectedTarget != null && inspectedTarget != currentTarget)
            inspectedTarget.SetHighlighted(false);

        inspectedTarget = dashboardFocusTarget;
        if (hud != null)
        {
            hud.ShowInspection(dashboardFocusTarget);
            hud.SetDashboardHoverTarget(currentTarget);
        }
    }

    public void SetInteractionSuspended(bool suspended)
    {
        // El menú de pausa suspende el puntero; al volver exige soltar el botón antes de otro clic.
        interactionSuspended = suspended;
        if (suspended) SetCurrentTarget(null);
        else suppressClickUntilRelease = true;
    }

    public void EndInspection()
    {
        // Cierra la inspección y restaura los controles, cámara, HUD y resaltado previos.
        if (!isInspecting) return;

        Interactable previousTarget = inspectedTarget;
        isInspecting = false;
        inspectedTarget = null;
        dashboardFocusTarget = null;
        if (cameraController != null) cameraController.EndInspection();
        if (playerMovement != null) playerMovement.SetInspectionLocked(false);
        if (hud != null) hud.EndInspection();
        if (previousTarget != null) previousTarget.SetHighlighted(false);
        if (currentTarget != null && currentTarget != previousTarget) currentTarget.SetHighlighted(false);
        currentTarget = null;
        if (hud != null) hud.SetTarget(null);
    }

    public void Configure(Camera cameraToUse, PrototypeInteractionHUD hudToUse, float maxDistance)
    {
        // Permite asignar dependencias y distancia desde otro inicializador.
        SetCurrentTarget(null);
        viewCamera = cameraToUse;
        hud = hudToUse;
        interactionDistance = Mathf.Max(0.1f, maxDistance);
        if (hud != null) hud.SetTarget(null);
    }
}
