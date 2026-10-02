using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float sprintSpeed = 10f;
    public float gravity = -9.81f;
    
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    public bool MovementEnabled { get; set; } = true;

    [Header("Mobile Joystick (Optional)")]
    // Agar mobile ke liye Virtual Joystick use kar rahe hain toh iska reference yahan aayega
    public VariableJoystick mobileJoystick; 

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (IsSpawned && !IsOwner)
        {
            return;
        }

        if (!MovementEnabled)
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
        float currentSpeed = moveSpeed;
        if (Input.GetKey(Key.LeftShift))
        {
            currentSpeed = sprintSpeed;
        }

        // Player ko move karana
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Gravity apply karna
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}