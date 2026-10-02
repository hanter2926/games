using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(CharacterController))]

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float sprintSpeed = 10f;
    public float gravity = -9.81f;

    [Header("Stamina")]
    [Min(1f)] public float maxStamina = 100f;
    [SerializeField] private float currentStamina;
    [Min(0f)] public float sprintStaminaDrainPerSecond = 20f;
    [Min(0f)] public float staminaRecoveryPerSecond = 15f;
    [Range(0f, 1f)] public float exhaustionRecoveryThreshold = 0.25f;
    public Slider staminaSlider;
    
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    public bool MovementEnabled { get; set; } = true;
    public bool IsExhausted { get; private set; }
    public float CurrentStamina => currentStamina;

    [Header("Mobile Joystick (Optional)")]
    // Agar mobile ke liye Virtual Joystick use kar rahe hain toh iska reference yahan aayega
    public VariableJoystick mobileJoystick; 

    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentStamina = maxStamina;
        RefreshStaminaUI();
    }

    void Update()
    {
        if (IsSpawned && !IsOwner)
        {
            return;
        }

        // Check karein ki player zameen par hai ya nahi
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // --- PC Controls (WASD / Arrow Keys) ---
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // --- Mobile Controls Fallback (Agar joystick connected hai) ---
        if (mobileJoystick != null)
        {
            if (Mathf.Abs(mobileJoystick.Horizontal) > 0.1f) moveX = mobileJoystick.Horizontal;
            if (Mathf.Abs(mobileJoystick.Vertical) > 0.1f) moveZ = mobileJoystick.Vertical;
        }

        // Direction calculate karna
        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        // Sprint (Daudna) - Shift key for PC
        bool hasMovementInput = new Vector2(moveX, moveZ).sqrMagnitude > 0.01f;
        bool sprintRequested = Input.GetKey(KeyCode.LeftShift);
        bool sprinting = MovementEnabled && sprintRequested && hasMovementInput && !IsExhausted;
        UpdateStamina(sprinting);

        float currentSpeed = moveSpeed;
        if (sprinting)
        {
            currentSpeed = sprintSpeed;
        }

        if (!MovementEnabled)
        {
            return;
        }

        // Player ko move karana
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Gravity apply karna
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateStamina(bool sprinting)
    {
        if (sprinting)
        {
            currentStamina = Mathf.Max(0f, currentStamina - sprintStaminaDrainPerSecond * Time.deltaTime);
            if (currentStamina <= 0f)
            {
                IsExhausted = true;
            }
        }
        else
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRecoveryPerSecond * Time.deltaTime);
            if (IsExhausted && currentStamina >= maxStamina * exhaustionRecoveryThreshold)
            {
                IsExhausted = false;
            }
        }

        RefreshStaminaUI();
    }

    private void RefreshStaminaUI()
    {
        if (staminaSlider != null && maxStamina > 0f)
        {
            staminaSlider.value = currentStamina / maxStamina;
        }
    }
}