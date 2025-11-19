using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private InputSystem_Actions controls;
    private Vector2 moveInput;
    private Vector2 lookInput;
    public float mouseSensitivity = 1.0f;

    public float moveSpeed = 5f;
    [SerializeField] private Rigidbody rb;
    private float pitch = 0f;
    private Camera cam;
    void Awake()
    {
        controls = new InputSystem_Actions();
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        cam = Camera.main;
    }

    void OnEnable()
    {
        controls.Enable();
    }

    void OnDisable()
    {
        controls.Disable();
    }

    void Update()
    {
        moveInput = controls.Player.Move.ReadValue<Vector2>();
        lookInput = controls.Player.Look.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        // Convert 2D input into 3D direction
        Vector3 direction =
            transform.forward * moveInput.y +
            transform.right * moveInput.x;

        // Physics movement
        rb.AddForce(direction * moveSpeed, ForceMode.Acceleration);
        HandleLook();
    }

        void HandleLook()
    {
        // Horizontal rotation (player yaw)
        float yaw = lookInput.x * mouseSensitivity;
        transform.Rotate(0f, yaw, 0f);

        // Vertical rotation (camera pitch)
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
