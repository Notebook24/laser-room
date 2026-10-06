using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float baseJumpSpeed = 7f;
    public float gravity = -20f;
    [HideInInspector] public bool canMove = true;   // false during the start countdown

    [Header("Look")]
    public float mouseSensitivity = 0.1f;
    public Vector3 cameraLocalPosition = new Vector3(0f, 1.6f, 0f);

    CharacterController controller;
    Transform cam;
    float verticalVelocity;
    bool grounded;
    float lastGroundedTime;
    float pitch;
    Vector3 startPosition;
    Quaternion startRotation;

    public float CurrentJumpSpeed { get; private set; }
    public int RespawnCount { get; private set; }
    public float CurrentMoveSpeed { get; private set; }
    public CharacterController Controller => controller;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);

        // The old body cube would collide with the CharacterController, so turn its colliders off
        foreach (Collider c in GetComponentsInChildren<Collider>())
            if (c != controller) c.enabled = false;

        // First person: hide the body mesh
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        // Attach the camera to the player automatically
        Camera camera = GetComponentInChildren<Camera>();
        if (camera == null) camera = Camera.main;
        cam = camera.transform;
        cam.SetParent(transform);
        cam.localPosition = cameraLocalPosition;
        cam.localRotation = Quaternion.identity;

        startPosition = transform.position;
        startRotation = transform.rotation;
        CurrentJumpSpeed = baseJumpSpeed;
        CurrentMoveSpeed = moveSpeed;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null) return;

        // Cursor lock toggle
        if (kb.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Mouse look
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f);
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // WASD
        Vector2 input = Vector2.zero;
        if (canMove)
        {
            if (kb.wKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed) input.x -= 1f;
        }

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        move = Vector3.ClampMagnitude(move, 1f) * CurrentMoveSpeed;

        // Jump and gravity.
        // We track "grounded" from our own Move call. controller.isGrounded gets overwritten by
        // other Move calls (like a moving platform carrying us), which made jumping fail.
        bool canJump = grounded || Time.time - lastGroundedTime < 0.15f;   // small grace period

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        if (canMove && canJump && kb.spaceKey.wasPressedThisFrame)
        {
            verticalVelocity = CurrentJumpSpeed;
            lastGroundedTime = -10f;   // no double jump during the grace period
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        CollisionFlags flags = controller.Move(move * Time.deltaTime);
        grounded = (flags & CollisionFlags.Below) != 0;
        if (grounded) lastGroundedTime = Time.time;
    }

    // Instantly moves the player somewhere else (keeps facing direction)
    public void TeleportTo(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
        verticalVelocity = 0f;
    }

    public void AddMoveSpeed(float bonus)
    {
        CurrentMoveSpeed += bonus;
    }

    public void AddJumpBonus(float bonus)
    {
        CurrentJumpSpeed += bonus;   // adds on top of the current jump speed
    }

    public void Respawn()
    {
        RespawnCount++;
        controller.enabled = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        controller.enabled = true;
        verticalVelocity = 0f;
        pitch = 0f;
        cam.localRotation = Quaternion.identity;
        CurrentJumpSpeed = baseJumpSpeed;
        CurrentMoveSpeed = moveSpeed;
    }
}