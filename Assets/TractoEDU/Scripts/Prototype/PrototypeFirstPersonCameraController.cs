using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
// Maneja mirada en primera persona, asomarse lateralmente, cursor y enfoque de inspección.
public sealed class PrototypeFirstPersonCameraController : MonoBehaviour
{
    private const string DashboardDisplayName = "Tablero";
    private const string SteeringWheelDisplayName = "Volante";

    // Ajustes de altura, sensibilidad, campo visual y límites de mirada.
    [SerializeField, Min(0.5f)] private float cameraHeight = 1.62f;
    [SerializeField, Min(0f)] private float sensitivity = 0.55f;
    [SerializeField, Range(40f, 100f)] private float fieldOfView = 72f;
    [SerializeField, Range(30f, 70f)] private float dashboardFocusFieldOfView = 48f;
    [SerializeField, Min(0.01f)] private float dashboardFocusSmoothTime = 0.22f;
    [SerializeField, Range(-89f, 0f)] private float minimumPitch = -80f;
    [SerializeField, Range(0f, 89f)] private float maximumPitch = 80f;
    [SerializeField, Range(-80f, 80f)] private float startingPitch = 14.5f;
    // Asomarse con A/D gira suavemente y desplaza un poco la cámara, sujeto a colisiones.
    [SerializeField, Range(5f, 45f)] private float sideLookAngle = 28f;
    [SerializeField, Min(0.01f)] private float sideLookSmoothTime = 0.18f;
    [SerializeField, Range(0.05f, 0.45f)] private float sidePeekDistance = 0.22f;
    [SerializeField, Range(0.02f, 0.16f)] private float sidePeekCollisionRadius = 0.08f;

    // Referencias y ángulos acumulados durante la exploración.
    private Camera attachedCamera;
    private Transform playerRoot;
    private PrototypePlayerMovementController playerMovement;
    private float yaw;
    private float pitch;
    private float sideLookYaw;
    private float sideLookYawVelocity;
    private float sidePeekOffset;
    private float sidePeekOffsetVelocity;
    private int sidePeekDirection;
    private bool cursorCaptured;
    private bool isPaused;
    private bool dashboardFocusActive;
    private float fieldOfViewVelocity;
    private Renderer[] temporarilyHiddenSteeringWheelRenderers;
    private bool[] steeringWheelRendererStates;

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
    public bool IsCursorCaptured => cursorCaptured;
    public bool IsPaused => isPaused;

    private void Awake()
    {
        // Inicializa la cámara desde el objeto padre del jugador y oculta sus mallas en primera persona.
        attachedCamera = GetComponent<Camera>();
        playerRoot = transform.parent;
        playerMovement = playerRoot != null ? playerRoot.GetComponent<PrototypePlayerMovementController>() : null;
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
        if (playerMovement != null) playerMovement.SetLateralLookControlsActive(false);
        sidePeekDirection = 0;
        sideLookYaw = 0f;
        sideLookYawVelocity = 0f;
        sidePeekOffset = 0f;
        sidePeekOffsetVelocity = 0f;
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

        // A y D alternan el vistazo a cada lado; pulsar el mismo lado vuelve al centro.
        Keyboard keyboard = Keyboard.current;
        if (cursorCaptured && keyboard != null)
        {
            bool leftPressed = keyboard.aKey.wasPressedThisFrame;
            bool rightPressed = keyboard.dKey.wasPressedThisFrame;
            if (leftPressed && rightPressed)
                sidePeekDirection = 0;
            else if (leftPressed)
                sidePeekDirection = sidePeekDirection == -1 ? 0 : -1;
            else if (rightPressed)
                sidePeekDirection = sidePeekDirection == 1 ? 0 : 1;
        }
        float sideLookInput = sidePeekDirection;
        float desiredSideLookYaw = sideLookInput * sideLookAngle;
        sideLookYaw = Mathf.SmoothDampAngle(sideLookYaw, desiredSideLookYaw, ref sideLookYawVelocity, sideLookSmoothTime);
        float desiredSidePeekOffset = GetSafeSidePeekOffset(sideLookInput * sidePeekDistance);
        sidePeekOffset = Mathf.SmoothDamp(sidePeekOffset, desiredSidePeekOffset, ref sidePeekOffsetVelocity, sideLookSmoothTime);

        // El movimiento del ratón modifica yaw/pitch solo con el cursor capturado y la ventana enfocada.
        Mouse mouse = Mouse.current;
        if (cursorCaptured && mouse != null && Application.isFocused)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            yaw += mouseDelta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * sensitivity, minimumPitch, maximumPitch);
        }

        if (playerRoot != null) playerRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
        transform.localPosition = new Vector3(sidePeekOffset, cameraHeight, 0f);
        transform.localRotation = Quaternion.Euler(pitch, sideLookYaw, 0f);
        UpdateFieldOfView();
    }

    public void BeginInspection(Interactable target, float distance, float height, float transitionDuration)
    {
        // Guarda el estado una sola vez, libera el cursor y orienta la vista hacia el componente.
        if (target == null) return;
        if (!inspectionActive)
        {
            savedLocalPosition = transform.localPosition;
            savedLocalRotation = transform.localRotation;
            savedCursorCaptured = cursorCaptured;
            savedCursorLockMode = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
        }

        inspectionTarget = target.transform;
        inspectionSmoothTime = Mathf.Max(0.01f, transitionDuration);
        SetDashboardFocus(target.DisplayName == DashboardDisplayName);
        inspectionActive = true;
        SetCursorCaptured(false);
        // Keep the existing API, but inspect from the driver's eye point so the
        // camera cannot leave the cab while focusing on a selected component.
        UpdateInspectionCamera();
    }

    public void EndInspection()
    {
        // Restaura la posición local, rotación y estado del cursor guardados al comenzar.
        if (!inspectionActive) return;
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
        // En pausa centra el vistazo lateral y libera el cursor; al reanudar lo vuelve a capturar.
        isPaused = paused;
        if (paused)
        {
            sidePeekDirection = 0;
            sideLookYaw = 0f;
            sideLookYawVelocity = 0f;
            sidePeekOffset = 0f;
            sidePeekOffsetVelocity = 0f;
            transform.localPosition = new Vector3(0f, cameraHeight, 0f);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        SetCursorCaptured(!paused);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Recupera el cursor al regresar a la ventana, salvo durante pausa o inspección.
        if (!hasFocus) return;
        SetCursorCaptured(!isPaused && !inspectionActive);
    }

    private void UpdateInspectionCamera(bool snap = false)
    {
        // Encuentra el centro visual de la pieza y gira suavemente hacia él sin mover la cámara del asiento.
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

        float targetFieldOfView = dashboardFocusActive ? dashboardFocusFieldOfView : fieldOfView;
        attachedCamera.fieldOfView = Mathf.SmoothDamp(
            attachedCamera.fieldOfView,
            targetFieldOfView,
            ref fieldOfViewVelocity,
            dashboardFocusSmoothTime);
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

    private float GetSafeSidePeekOffset(float requestedOffset)
    {
        // Un SphereCast reduce el desplazamiento lateral para evitar que la cámara atraviese geometría.
        if (Mathf.Abs(requestedOffset) < 0.001f || playerRoot == null) return 0f;

        Vector3 neutralPosition = playerRoot.TransformPoint(new Vector3(0f, cameraHeight, 0f));
        Vector3 direction = playerRoot.right * Mathf.Sign(requestedOffset);
        float distance = Mathf.Abs(requestedOffset);
        if (Physics.SphereCast(neutralPosition, sidePeekCollisionRadius, direction, out RaycastHit hit,
                distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            distance = Mathf.Max(0f, hit.distance - 0.015f);
        }

        return Mathf.Sign(requestedOffset) * distance;
    }

    public void Configure(float height, float fov, float initialPitch)
    {
        // Ajuste de parámetros usado por inicializadores externos.
        cameraHeight = height;
        fieldOfView = fov;
        startingPitch = initialPitch;
    }
}
