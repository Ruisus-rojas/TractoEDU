using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class PrototypeFirstPersonCameraController : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float cameraHeight = 1.62f;
    [SerializeField, Min(0f)] private float sensitivity = 0.12f;
    [SerializeField, Range(40f, 100f)] private float fieldOfView = 72f;
    [SerializeField, Range(1f, 89f)] private float verticalLookLimit = 80f;
    [SerializeField, Range(-80f, 80f)] private float startingPitch = 14.5f;

    private Camera attachedCamera;
    private Transform playerRoot;
    private float yaw;
    private float pitch;
    private bool cursorCaptured;

    private bool inspectionActive;
    private Transform inspectionTarget;
    private float inspectionDistance;
    private float inspectionHeight;
    private float inspectionSmoothTime;
    private Vector3 inspectionPositionVelocity;
    private Vector3 savedLocalPosition;
    private Quaternion savedLocalRotation;
    private bool savedCursorCaptured;
    private CursorLockMode savedCursorLockMode;
    private bool savedCursorVisible;

    public bool IsInspectionActive => inspectionActive;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        playerRoot = transform.parent;
        yaw = playerRoot != null ? playerRoot.eulerAngles.y : transform.eulerAngles.y;
        pitch = startingPitch;
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
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorCaptured = false;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        inspectionActive = false;
        inspectionTarget = null;
    }

    private void LateUpdate()
    {
        if (inspectionActive)
        {
            UpdateInspectionCamera();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetCursorCaptured(false);
        if (!cursorCaptured && mouse != null && mouse.leftButton.wasPressedThisFrame) SetCursorCaptured(true);

        if (cursorCaptured && mouse != null && Application.isFocused)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            yaw += mouseDelta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * sensitivity, -verticalLookLimit, verticalLookLimit);
        }

        if (playerRoot != null) playerRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        attachedCamera.fieldOfView = fieldOfView;
    }

    public void BeginInspection(Interactable target, float distance, float height, float transitionDuration)
    {
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
        inspectionDistance = Mathf.Max(0.5f, distance);
        inspectionHeight = Mathf.Max(0f, height);
        inspectionSmoothTime = Mathf.Max(0.01f, transitionDuration);
        inspectionPositionVelocity = Vector3.zero;
        inspectionActive = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UpdateInspectionCamera();
    }

    public void EndInspection()
    {
        if (!inspectionActive) return;
        inspectionActive = false;
        inspectionTarget = null;
        transform.localPosition = savedLocalPosition;
        transform.localRotation = savedLocalRotation;
        cursorCaptured = savedCursorCaptured;
        Cursor.lockState = savedCursorLockMode;
        Cursor.visible = savedCursorVisible;
    }

    private void UpdateInspectionCamera(bool snap = false)
    {
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
        Vector3 away = Vector3.ProjectOnPlane(transform.position - center, Vector3.up);
        if (away.sqrMagnitude < 0.001f) away = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
        away.Normalize();

        Vector3 desiredPosition = center + away * inspectionDistance + Vector3.up * inspectionHeight;
        desiredPosition.y = Mathf.Max(0.75f, desiredPosition.y);
        if (snap)
            transform.position = desiredPosition;
        else
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref inspectionPositionVelocity, inspectionSmoothTime);

        Quaternion desiredRotation = Quaternion.LookRotation(center - transform.position, Vector3.up);
        transform.rotation = snap
            ? desiredRotation
            : Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-12f * Time.deltaTime));
        attachedCamera.fieldOfView = fieldOfView;
    }

    private void SetCursorCaptured(bool captured)
    {
        cursorCaptured = captured;
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    public void Configure(float height, float fov, float initialPitch)
    {
        cameraHeight = height;
        fieldOfView = fov;
        startingPitch = initialPitch;
    }
}