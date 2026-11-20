using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
// Maneja mirada en primera persona, enfoque lateral, cursor e inspección.
public sealed class PrototypeFirstPersonCameraController : MonoBehaviour
{
    private const string DashboardDisplayName = "Tablero";
    private const string SteeringWheelDisplayName = "Volante";
    private const string SeatDisplayName = "Asiento del conductor";

    // Ajustes de altura, sensibilidad, campo visual y límites de mirada.
    [SerializeField, Min(0.5f)] private float cameraHeight = 1.62f;
    [SerializeField, Min(0f)] private float sensitivity = 0.55f;
    [SerializeField, Range(40f, 100f)] private float fieldOfView = 72f;
    [SerializeField, Range(30f, 70f)] private float dashboardFocusFieldOfView = 48f;
    [SerializeField, Min(0.01f)] private float dashboardFocusSmoothTime = 0.22f;
    [SerializeField, Range(-89f, 0f)] private float minimumPitch = -80f;
    [SerializeField, Range(0f, 89f)] private float maximumPitch = 80f;
    [SerializeField, Range(-80f, 80f)] private float startingPitch = 14.5f;
    // El enfoque lateral acerca la cámara al grupo de palancas sin sacar al jugador del puesto.
    [SerializeField, Range(30f, 60f)] private float sideFocusMinimumFieldOfView = 36f;
    [SerializeField, Range(40f, 70f)] private float sideFocusMaximumFieldOfView = 54f;
    [SerializeField, Range(1f, 1.5f)] private float sideFocusFramingMargin = 1.18f;

    // Referencias y ángulos acumulados durante la exploración.
    private Camera attachedCamera;
    private Transform playerRoot;
    private PrototypePlayerMovementController playerMovement;
    private Quaternion sideFocusReferenceRotation;
    private float yaw;
    private float pitch;
    private bool cursorCaptured;
    private bool isPaused;
    private bool dashboardFocusActive;
    private bool sideFocusActive;
    private Quaternion sideFocusTargetRotation;
    private float sideFocusTargetFieldOfView;
    private float fieldOfViewVelocity;
    private Renderer[] temporarilyHiddenSteeringWheelRenderers;
    private bool[] steeringWheelRendererStates;
    private Renderer[] temporarilyHiddenSeatRenderers;
    private bool[] seatRendererStates;

    // Guarda la pose/cursor para poder volver al punto exacto tras inspeccionar.
    private bool inspectionActive;
    private Transform inspectionTarget;
    private float inspectionSmoothTime;
    private Vector3 savedLocalPosition;
    private Quaternion savedLocalRotation;
    private bool savedCursorCaptured;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;

    public bool IsInspectionActive => inspectionActive;
    public bool IsDashboardFocusActive => dashboardFocusActive;
    public bool IsSideFocusActive => sideFocusActive;
    public bool IsCursorCaptured => cursorCaptured;
    public bool IsPaused => isPaused;

    /// <summary>Devuelve la posición relativa al tractor sin depender de hacia dónde mira el jugador.</summary>
    public Vector3 GetSideFocusReferenceLocalPosition(Vector3 worldPosition)
    {
        Vector3 referencePosition = playerRoot != null ? playerRoot.position : Vector3.zero;
        return Quaternion.Inverse(sideFocusReferenceRotation) * (worldPosition - referencePosition);
    }

    private void Awake()
    {
        // Inicializa la cámara desde el objeto padre del jugador y oculta sus mallas en primera persona.
        attachedCamera = GetComponent<Camera>();
        playerRoot = transform.parent;
        playerMovement = playerRoot != null ? playerRoot.GetComponent<PrototypePlayerMovementController>() : null;
        sideFocusReferenceRotation = playerRoot != null
            ? Quaternion.Euler(0f, playerRoot.eulerAngles.y, 0f)
            : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (playerMovement != null) playerMovement.SetLateralLookControlsActive(true);
        yaw = playerRoot != null ? playerRoot.eulerAngles.y : transform.eulerAngles.y;
        pitch = Mathf.Clamp(startingPitch, minimumPitch, maximumPitch);
        transform.localPosition = new Vector3(0f, cameraHeight, 0f);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        attachedCamera.fieldOfView = fieldOfView;

        if (playerRoot != null)
        {
            foreach (Renderer playerRenderer in playerRoot.GetComponentsInChildren<Renderer>(true))
                playerRenderer.enabled = false;
        }
    }

    private void OnEnable()
    {
        // Al habilitarse, captura el cursor para permitir mirar con el ratón.
        isPaused = false;
        if (playerMovement != null) playerMovement.SetLateralLookControlsActive(true);
        SetCursorCaptured(true);
    }

    private void OnDisable()
    {
        // Restablece controles y cursor para no dejar el ratón bloqueado al salir/desactivar.
        RestoreDashboardView();
        RestoreSideFocusSeat();
        if (playerMovement != null) playerMovement.SetLateralLookControlsActive(false);
        sideFocusActive = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorCaptured = false;
        isPaused = false;
        inspectionActive = false;
        inspectionTarget = null;
        if (attachedCamera != null) attachedCamera.fieldOfView = fieldOfView;
    }

    private void LateUpdate()
    {
        // La inspección tiene prioridad; en pausa no se actualiza la vista.
        if (inspectionActive)
        {
            UpdateInspectionCamera();
            return;
        }
        if (isPaused) return;

        // El movimiento del ratón modifica yaw/pitch solo con el cursor capturado y la ventana enfocada.
        Mouse mouse = Mouse.current;
        if (cursorCaptured && mouse != null && Application.isFocused)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            yaw += mouseDelta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * sensitivity, minimumPitch, maximumPitch);
        }

        if (playerRoot != null) playerRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
        transform.localPosition = new Vector3(0f, cameraHeight, 0f);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        UpdateFieldOfView();
    }

    public void BeginInspection(Interactable target, float distance, float height, float transitionDuration)
    {
        // Guarda el estado una sola vez, libera el cursor y orienta la vista al componente.
        if (target == null) return;
        SaveInspectionState();

        inspectionTarget = target.transform;
        inspectionSmoothTime = Mathf.Max(0.01f, transitionDuration);
        SetDashboardFocus(target.DisplayName == DashboardDisplayName);
        inspectionActive = true;
        SetCursorCaptured(false);
        // Keep the existing API, but inspect from the driver's eye point so the
        // camera cannot leave the cab while focusing on a selected component.
        UpdateInspectionCamera();
    }

    /// <summary>
    /// Enfoca el conjunto de controles de un lado, oculta temporalmente obstáculos visuales
    /// y libera el cursor para seleccionar con el mouse.
    /// </summary>
    public void BeginSideFocus(Bounds focusBounds, float transitionDuration)
    {
        if (inspectionActive) return;

        SaveInspectionState();
        inspectionTarget = null;
        inspectionSmoothTime = Mathf.Max(0.01f, transitionDuration);
        Vector3 focusDirection = focusBounds.center - transform.position;
        sideFocusTargetRotation = focusDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(focusDirection, Vector3.up)
            : sideFocusReferenceRotation;
        sideFocusTargetFieldOfView = CalculateSideFocusFieldOfView(focusBounds, sideFocusTargetRotation);
        sideFocusActive = true;
        inspectionActive = true;

        HideSteeringWheelForDashboard();
        HideSeatForSideFocus();
        SetCursorCaptured(false);
        UpdateInspectionCamera();
    }

    private void SaveInspectionState()
    {
        if (inspectionActive) return;

        savedLocalPosition = transform.localPosition;
        savedLocalRotation = transform.localRotation;
        savedCursorCaptured = cursorCaptured;
        savedCursorLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
    }

    public void EndInspection()
    {
        // Restaura la posición local, rotación y estado del cursor guardados al comenzar.
        if (!inspectionActive) return;
        sideFocusActive = false;
        RestoreSideFocusSeat();
        RestoreDashboardView();
        inspectionActive = false;
        inspectionTarget = null;
        transform.localPosition = savedLocalPosition;
        transform.localRotation = savedLocalRotation;
        cursorCaptured = savedCursorCaptured;
        Cursor.lockState = savedCursorLockMode;
        Cursor.visible = savedCursorVisible;
    }

    public void SetPaused(bool paused)
    {
        // En pausa libera el cursor y conserva la vista activa durante una inspección.
        isPaused = paused;
        if (paused && !inspectionActive)
        {
            transform.localPosition = new Vector3(0f, cameraHeight, 0f);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        SetCursorCaptured(!paused && !inspectionActive);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Recupera el cursor al regresar a la ventana, salvo durante pausa o inspección.
        if (!hasFocus) return;
        SetCursorCaptured(!isPaused && !inspectionActive);
    }

    private void UpdateInspectionCamera(bool snap = false)
    {
        if (sideFocusActive)
        {
            transform.rotation = snap
                ? sideFocusTargetRotation
                : Quaternion.Slerp(transform.rotation, sideFocusTargetRotation,
                    1f - Mathf.Exp(-Time.deltaTime / inspectionSmoothTime));
            UpdateFieldOfView();
            return;
        }

        // Encuentra el centro visual de la pieza y gira suavemente hacia ella desde el puesto.
        if (inspectionTarget == null) return;

        Renderer[] renderers = inspectionTarget.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(inspectionTarget.position, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null || targetRenderer.name.EndsWith("_Outline")) continue;
            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else bounds.Encapsulate(targetRenderer.bounds);
        }

        Vector3 center = hasBounds ? bounds.center : inspectionTarget.position;
        Quaternion desiredRotation = Quaternion.LookRotation(center - transform.position, Vector3.up);
        transform.rotation = snap
            ? desiredRotation
            : Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-Time.deltaTime / inspectionSmoothTime));
        UpdateFieldOfView();
    }

    /// <summary>
    /// Acerca la vista al tablero durante su inspección iniciada con clic y despeja el volante.
    /// El objeto y su collider permanecen activos; solo se ocultan sus renderizadores.
    /// </summary>
    public void SetDashboardFocus(bool focused)
    {
        if (dashboardFocusActive == focused) return;

        dashboardFocusActive = focused;
        fieldOfViewVelocity = 0f;
        if (focused) HideSteeringWheelForDashboard();
        else RestoreSteeringWheelRenderers();
    }

    private void UpdateFieldOfView()
    {
        if (attachedCamera == null) return;

        float targetFieldOfView = dashboardFocusActive
            ? dashboardFocusFieldOfView
            : sideFocusActive ? sideFocusTargetFieldOfView : fieldOfView;
        float smoothTime = sideFocusActive ? inspectionSmoothTime : dashboardFocusSmoothTime;
        attachedCamera.fieldOfView = Mathf.SmoothDamp(
            attachedCamera.fieldOfView,
            targetFieldOfView,
            ref fieldOfViewVelocity,
            smoothTime);
    }

    private void HideSteeringWheelForDashboard()
    {
        // Busca el volante por su nombre de interacción para no depender de una ruta de jerarquía.
        Interactable[] interactables = FindObjectsByType<Interactable>(FindObjectsSortMode.None);
        Interactable steeringWheel = null;
        foreach (Interactable candidate in interactables)
        {
            if (candidate != null && candidate.DisplayName == SteeringWheelDisplayName)
            {
                steeringWheel = candidate;
                break;
            }
        }

        if (steeringWheel == null) return;

        temporarilyHiddenSteeringWheelRenderers = steeringWheel.GetComponentsInChildren<Renderer>(true);
        steeringWheelRendererStates = new bool[temporarilyHiddenSteeringWheelRenderers.Length];
        for (int i = 0; i < temporarilyHiddenSteeringWheelRenderers.Length; i++)
        {
            Renderer wheelRenderer = temporarilyHiddenSteeringWheelRenderers[i];
            if (wheelRenderer == null) continue;

            steeringWheelRendererStates[i] = wheelRenderer.enabled;
            wheelRenderer.enabled = false;
        }
    }

    private void RestoreDashboardView()
    {
        dashboardFocusActive = false;
        fieldOfViewVelocity = 0f;
        RestoreSteeringWheelRenderers();
    }

    private float CalculateSideFocusFieldOfView(Bounds bounds, Quaternion targetRotation)
    {
        if (attachedCamera == null) return sideFocusMaximumFieldOfView;
        float aspect = Mathf.Max(0.1f, attachedCamera.aspect);
        float requiredTangent = 0.01f;
        for (int x = 0; x <= 1; x++)
        for (int y = 0; y <= 1; y++)
        for (int z = 0; z <= 1; z++)
        {
            Vector3 corner = bounds.min + Vector3.Scale(bounds.size, new Vector3(x, y, z));
            Vector3 inCameraSpace = Quaternion.Inverse(targetRotation) * (corner - transform.position);
            if (inCameraSpace.z <= 0.01f) continue;

            requiredTangent = Mathf.Max(
                requiredTangent,
                Mathf.Abs(inCameraSpace.y) / inCameraSpace.z,
                Mathf.Abs(inCameraSpace.x) / (inCameraSpace.z * aspect));
        }

        float framedTangent = requiredTangent * sideFocusFramingMargin;
        float requiredFieldOfView = 2f * Mathf.Atan(framedTangent) * Mathf.Rad2Deg;
        return Mathf.Clamp(requiredFieldOfView, sideFocusMinimumFieldOfView, sideFocusMaximumFieldOfView);
    }

    private void HideSeatForSideFocus()
    {
        Interactable[] interactables = FindObjectsByType<Interactable>(FindObjectsSortMode.None);
        foreach (Interactable candidate in interactables)
        {
            if (candidate == null || candidate.DisplayName != SeatDisplayName) continue;

            candidate.SetHighlighted(false);
            temporarilyHiddenSeatRenderers = candidate.GetComponentsInChildren<Renderer>(true);
            seatRendererStates = new bool[temporarilyHiddenSeatRenderers.Length];
            for (int i = 0; i < temporarilyHiddenSeatRenderers.Length; i++)
            {
                Renderer seatRenderer = temporarilyHiddenSeatRenderers[i];
                if (seatRenderer == null) continue;

                seatRendererStates[i] = seatRenderer.enabled;
                seatRenderer.enabled = false;
            }
            return;
        }
    }

    private void RestoreSideFocusSeat()
    {
        if (temporarilyHiddenSeatRenderers != null && seatRendererStates != null)
        {
            int count = Mathf.Min(temporarilyHiddenSeatRenderers.Length, seatRendererStates.Length);
            for (int i = 0; i < count; i++)
            {
                Renderer seatRenderer = temporarilyHiddenSeatRenderers[i];
                if (seatRenderer != null) seatRenderer.enabled = seatRendererStates[i];
            }
        }

        temporarilyHiddenSeatRenderers = null;
        seatRendererStates = null;
    }

    private void RestoreSteeringWheelRenderers()
    {
        if (temporarilyHiddenSteeringWheelRenderers != null && steeringWheelRendererStates != null)
        {
            int count = Mathf.Min(temporarilyHiddenSteeringWheelRenderers.Length, steeringWheelRendererStates.Length);
            for (int i = 0; i < count; i++)
            {
                Renderer wheelRenderer = temporarilyHiddenSteeringWheelRenderers[i];
                if (wheelRenderer != null) wheelRenderer.enabled = steeringWheelRendererStates[i];
            }
        }

        temporarilyHiddenSteeringWheelRenderers = null;
        steeringWheelRendererStates = null;
    }

    private void SetCursorCaptured(bool captured)
    {
        // Sincroniza el estado interno con el bloqueo y visibilidad reales del cursor.
        cursorCaptured = captured;
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    public void Configure(float height, float fov, float initialPitch)
    {
        // Ajuste de parámetros usado por inicializadores externos.
        cameraHeight = height;
        fieldOfView = fov;
        startingPitch = initialPitch;
    }
}
