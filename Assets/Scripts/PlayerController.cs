using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator armsAnimator;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float crouchSpeed = 1.8f;

    [Header("Jump and gravity")]
    [SerializeField] private float jumpHeight = 1.1f;
    [SerializeField] private float gravity = -20f;

    [Header("Crouch")]
    [SerializeField] private float standHeight = 1.8f;
    [SerializeField] private float crouchHeight = 1.0f;
    [SerializeField] private float crouchTransitionSpeed = 10f;
    [SerializeField] private float cameraHeightOffset = 0.15f;
    [SerializeField] private LayerMask ceilingMask = ~0;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxLookAngle = 80f;

    [Header("Animation")]
    [SerializeField] private float speedDampTime = 0.1f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private readonly Collider[] ceilingHits = new Collider[8];

    private CharacterController controller;
    private float verticalVelocity;
    private float cameraPitch;
    private bool isCrouching;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Rigidbody conflicts with CharacterController (makes the player fly).
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // If the camera was not assigned, take the first camera in children.
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cameraTransform = cam.transform;
            }
            else
            {
                Debug.LogError("PlayerController: no camera assigned or found in children.", this);
                enabled = false;
                return;
            }
        }

        // If the arms animator was not assigned, take the first one in children.
        if (armsAnimator == null)
        {
            armsAnimator = GetComponentInChildren<Animator>();
        }

        controller.height = standHeight;
        controller.center = new Vector3(0f, standHeight * 0.5f, 0f);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null)
        {
            return;
        }

        HandleLook();
        HandleCrouch();
        HandleMovement();
    }

    private void HandleLook()
    {
        Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        transform.Rotate(Vector3.up * delta.x);

        cameraPitch = Mathf.Clamp(cameraPitch - delta.y, -maxLookAngle, maxLookAngle);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleCrouch()
    {
        Keyboard kb = Keyboard.current;
        bool crouchHeld = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;

        if (crouchHeld)
        {
            isCrouching = true;
        }
        else if (isCrouching && CanStandUp())
        {
            isCrouching = false;
        }

        float targetHeight = isCrouching ? crouchHeight : standHeight;
        float newHeight = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);

        controller.height = newHeight;
        controller.center = new Vector3(0f, newHeight * 0.5f, 0f);

        Vector3 cameraPosition = cameraTransform.localPosition;
        cameraPosition.y = newHeight - cameraHeightOffset;
        cameraTransform.localPosition = cameraPosition;
    }

    private void HandleMovement()
    {
        Keyboard kb = Keyboard.current;

        float speed = walkSpeed;
        if (isCrouching)
        {
            speed = crouchSpeed;
        }
        else if (kb.leftShiftKey.isPressed)
        {
            speed = runSpeed;
        }

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

        Vector3 move = transform.right * x + transform.forward * z;
        move = Vector3.ClampMagnitude(move, 1f);

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        bool jumped = false;
        if (kb.spaceKey.wasPressedThisFrame && controller.isGrounded && !isCrouching)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumped = true;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        UpdateAnimator(move.magnitude * speed, jumped);
    }

    private void UpdateAnimator(float currentSpeed, bool jumped)
    {
        if (armsAnimator == null)
        {
            return;
        }

        armsAnimator.SetFloat(SpeedHash, currentSpeed, speedDampTime, Time.deltaTime);
        armsAnimator.SetBool(IsCrouchingHash, isCrouching);

        if (jumped)
        {
            armsAnimator.SetTrigger(JumpHash);
        }
    }

    private bool CanStandUp()
    {
        float radius = Mathf.Max(0.05f, controller.radius * 0.9f);
        float minTop = radius + 0.05f;
        float topHeight = Mathf.Max(minTop, standHeight - radius);

        Vector3 bottom = transform.position + Vector3.up * radius;
        Vector3 top = transform.position + Vector3.up * topHeight;

        int count = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            radius,
            ceilingHits,
            ceilingMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            Collider hit = ceilingHits[i];
            if (hit == null || hit == controller || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}