using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Vector2 moveInput;
    private Vector2 lookInput;

    [Header("Movement Settings")]
    public float mouseSensitivity = 1.0f;
    public float moveSpeed = 5f;

    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform vessel;

    private Camera cam;

    private float pitch = 0f;

    private Quaternion lastYawRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        cam = Camera.main;

        Vector3 fwd = vessel.forward;
        fwd.y = 0f;
        lastYawRotation = Quaternion.LookRotation(fwd, Vector3.up);

        rb.linearDamping = 0f;
        rb.angularDamping = 100f;
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
            Vector3 center = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
            Ray ray = Camera.main.ScreenPointToRay(center);

            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                var btn = hit.collider.GetComponent<NavigationButton>();
                if (btn != null)
                    btn.OnMouseDown();
            }
        }
    }

    void FixedUpdate()
    {
        HandleLook();
    }

    void LateUpdate()
    {
        SubmarineYawShit();
        SubmarineVelocityShit();
    }

    void HandleLook()
    {
        float yaw = lookInput.x * mouseSensitivity;
        transform.Rotate(0f, yaw, 0f);

        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 vEuler = vessel.rotation.eulerAngles;

        float vesselPitch = NormalizeAngle(vEuler.x);
        float vesselRoll = NormalizeAngle(vEuler.z);

        cam.transform.rotation *= Quaternion.Euler(-vesselPitch, 0f, -vesselRoll);
    }
    void SubmarineYawShit()
    {
        Vector3 fwd = vessel.forward;
        fwd.y = 0f;
        fwd.Normalize();

        Quaternion yawRotation = Quaternion.LookRotation(fwd, Vector3.up);

        Quaternion deltaYaw = yawRotation * Quaternion.Inverse(lastYawRotation);

        rb.MoveRotation(deltaYaw * rb.rotation);

        lastYawRotation = yawRotation;
    }
    void SubmarineVelocityShit()
    {
        Rigidbody subRoot = vessel.root.GetComponent<Rigidbody>();
        if (subRoot == null) return;

        Vector3 submarineVelocity = subRoot.linearVelocity;
        Vector3 angularVelocity = subRoot.angularVelocity;

        Vector3 relPos = rb.worldCenterOfMass - subRoot.worldCenterOfMass;
        Vector3 rotationalVelocity = Vector3.Cross(angularVelocity, relPos);

        Vector3 relativeMove =
            (transform.forward * moveInput.y +
             transform.right * moveInput.x) * moveSpeed;

        rb.linearVelocity = submarineVelocity + rotationalVelocity + relativeMove;
    }

    float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        if (a < -180f) a += 360f;
        return a;
    }
}