using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private Vector2 moveInput;
    private Vector2 lookInput;
    public float mouseSensitivity = 1.0f;

    public float moveSpeed = 5f;
    [SerializeField] private Rigidbody rb;
    private float pitch = 0f;
    private Camera cam;

    private Rigidbody submarineRb;
    private Quaternion lastSubmarineRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        cam = Camera.main;

        submarineRb = transform.parent.GetComponent<Rigidbody>();
        lastSubmarineRotation = submarineRb.rotation;
        rb.linearDamping = 2f;
        rb.angularDamping = 2f;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            Ray ray = Camera.main.ScreenPointToRay(screenCenter);
            Debug.DrawRay(ray.origin, ray.direction, Color.green, 1.0f);
            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                NavigationButton navButton = hit.collider.GetComponent<NavigationButton>();
                if (navButton != null)
                    navButton.OnMouseDown();
            }
        }
    }

    void FixedUpdate()
    {
        Quaternion currentSubmarineRotation = submarineRb.rotation;
        Quaternion deltaRotation = currentSubmarineRotation * Quaternion.Inverse(lastSubmarineRotation);
        rb.MoveRotation(deltaRotation * rb.rotation);
        lastSubmarineRotation = currentSubmarineRotation;

        Vector3 submarineVelocity = submarineRb.linearVelocity;
        Vector3 angularVelocity = submarineRb.angularVelocity;
        Vector3 relativePosition = rb.worldCenterOfMass - submarineRb.worldCenterOfMass;
        Vector3 rotationalVelocity = Vector3.Cross(angularVelocity, relativePosition);
        Vector3 relativeMove = (transform.forward * moveInput.y + transform.right * moveInput.x) * moveSpeed;
        rb.linearVelocity = submarineVelocity + rotationalVelocity + relativeMove;

        HandleLook();
    }

    void HandleLook()
    {
        float yaw = lookInput.x * mouseSensitivity;
        transform.Rotate(0f, yaw, 0f);

        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
