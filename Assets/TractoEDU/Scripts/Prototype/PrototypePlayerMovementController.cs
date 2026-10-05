using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PrototypePlayerMovementController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5.5f;
    [SerializeField] private float gravity = -20f;

    private CharacterController characterController;
    private float verticalVelocity;
    private bool inspectionLocked;

    public bool IsInspectionLocked => inspectionLocked;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (inspectionLocked) return;

        Keyboard keyboard = Keyboard.current;
        Vector2 input = Vector2.zero;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        }
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 moveDirection = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);

        if (characterController.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        characterController.Move((moveDirection * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    public void SetInspectionLocked(bool locked)
    {
        inspectionLocked = locked;
    }
}