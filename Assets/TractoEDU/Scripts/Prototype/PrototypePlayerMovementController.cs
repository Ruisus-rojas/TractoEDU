using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
// Movimiento básico del jugador; la cámara maneja la orientación y este script el desplazamiento.
public sealed class PrototypePlayerMovementController : MonoBehaviour
{
    // Parámetros que pueden ajustarse desde el Inspector.
    [SerializeField] private bool movementEnabled = true;
    [SerializeField, Min(0f)] private float moveSpeed = 5.5f;
    [SerializeField] private float gravity = -20f;

    // Estado de movimiento vertical y bloqueos temporales de movimiento/teclas laterales.
    private CharacterController characterController;
    private float verticalVelocity;
    private bool inspectionLocked;
    private bool lateralLookControlsActive;

    public bool IsInspectionLocked => inspectionLocked;
    public bool IsMovementEnabled => movementEnabled;

    private void Awake()
    {
        // RequireComponent garantiza que exista este controlador de colisiones.
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // Evita mover al jugador si la escena lo tiene inmovilizado o si inspecciona una pieza.
        if (!movementEnabled || inspectionLocked) return;

        Keyboard keyboard = Keyboard.current;
        Vector2 input = Vector2.zero;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed || (!lateralLookControlsActive && keyboard.aKey.isPressed)) input.x -= 1f;
            if (keyboard.rightArrowKey.isPressed || (!lateralLookControlsActive && keyboard.dKey.isPressed)) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        }
        input = Vector2.ClampMagnitude(input, 1f);

        // Convierte las entradas 2D a direcciones horizontales respecto a la orientación del jugador.
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 moveDirection = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);

        if (characterController.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        characterController.Move((moveDirection * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    public void SetInspectionLocked(bool locked)
    {
        // El interactor bloquea el desplazamiento al abrir una ficha de inspección.
        inspectionLocked = locked;
    }

    public void SetMovementEnabled(bool enabled)
    {
        // Permite activar/desactivar el movimiento desde otros componentes.
        movementEnabled = enabled;
        if (!movementEnabled) verticalVelocity = 0f;
    }

    public void SetLateralLookControlsActive(bool active)
    {
        // Cuando A/D miran a los lados, evita que también desplacen al jugador.
        lateralLookControlsActive = active;
    }
}
