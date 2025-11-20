using UnityEngine;
using UnityEngine.UI; // Add this for Text
using TMPro;

public class SubmarineController : MonoBehaviour
{
    public enum NavigationStates
    {
        Back, AllStop, AheadOneThird, AheadStandard, AheadFull
    }

    public NavigationStates currentState = NavigationStates.AllStop;

    [Header("Navigation Controls")]
    [SerializeField] private GameObject aheadButton;
    [SerializeField] private GameObject backButton;
    [SerializeField] private GameObject portButton;
    [SerializeField] private GameObject starboardButton;
    [SerializeField] private GameObject surfaceButton;
    [SerializeField] private GameObject diveButton;

    [Header("Control variables")]
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float acceleration = 0.5f;
    [SerializeField] private float diveRate = 0.2f;
    [SerializeField] public float batteryCharge = 100f;
    [SerializeField] private float batteryDrainRate = 0f;
    [SerializeField] public float oxygen = 100f;
    [SerializeField] private float oxygenConsumptionRate = 0.02f;

    [Header("Steering")]
    [SerializeField] private float rudderAngle = 0f; // -1 (full left) to 1 (full right)
    [SerializeField] private float maxRudder = 1f;
    [SerializeField] private float rudderStep = 0.2f;
    [SerializeField] private float turnRate = 30f; // degrees per second at max speed and max rudder

    [Header("Sonar Suite (only active system for now)")]
    [SerializeField] private GameObject portSonar; // Left
    [SerializeField] private GameObject starboardSonar; // Right
    [SerializeField] private GameObject bowSonar; // Front
    [SerializeField] private GameObject aftSonar; // Back
    [SerializeField] private GameObject keelSonar; // Bottom (no laughing!)

    [Header("Active Sonar Display")]
    [SerializeField] private GameObject activeSonarDisplay;

    [Header("Sonar Text Fields")]
    [SerializeField] private TextMeshProUGUI bowText;
    [SerializeField] private TextMeshProUGUI sternText;
    [SerializeField] private TextMeshProUGUI portText;
    [SerializeField] private TextMeshProUGUI starboardText;
    [SerializeField] private TextMeshProUGUI keelText;

    private float currentSpeed = 0f;
    private Rigidbody rb;
    private float pingTimer = 0f;
    private float pingInterval = 5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentState = NavigationStates.AllStop;
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
        }
        rb.linearDamping = 2f;
        rb.angularDamping = 2f;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        NavigationUpdate();
        ApplySteering();

        pingTimer += Time.fixedDeltaTime;
        if (pingTimer >= pingInterval)
        {
            Ping();
            pingTimer = 0f;
        }
    }

    private void NavigationUpdate()
    {
        float targetSpeed = GetSpeedForState(currentState);

        if (currentSpeed < targetSpeed)
        {
            currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.fixedDeltaTime, targetSpeed);
        }
        else if (currentSpeed > targetSpeed)
        {
            currentSpeed = Mathf.Max(currentSpeed - acceleration * Time.fixedDeltaTime, targetSpeed);
        }

        Vector3 moveDirection = transform.forward;
        if (currentState == NavigationStates.Back)
            moveDirection = -transform.forward;

        rb.linearVelocity = moveDirection * Mathf.Abs(currentSpeed);
    }

    public void OnNavigationButtonPressed(NavigationButton.NavButtonType buttonType)
    {
        switch (buttonType)
        {
            case NavigationButton.NavButtonType.Ahead:
                if (currentState < NavigationStates.AheadFull)
                    currentState++;
                break;
            case NavigationButton.NavButtonType.Back:
                if (currentState > NavigationStates.Back)
                    currentState--;
                break;
            case NavigationButton.NavButtonType.Port:
                rudderAngle = Mathf.Clamp(rudderAngle - rudderStep, -maxRudder, maxRudder);
                break;
            case NavigationButton.NavButtonType.Starboard:
                rudderAngle = Mathf.Clamp(rudderAngle + rudderStep, -maxRudder, maxRudder);
                break;
            default:
                break;
        }
        Debug.Log("Navigation State: " + currentState + ", Rudder Angle: " + rudderAngle);
    }

    private void ApplySteering()
    {
        float speedFactor = GetSpeedForState(currentState) / maxSpeed;
        float turnAmount = rudderAngle * turnRate * speedFactor * Time.fixedDeltaTime;
        if (Mathf.Abs(turnAmount) > 0.001f)
        {
            Quaternion deltaRotation = Quaternion.Euler(0f, turnAmount, 0f);
            rb.MoveRotation(rb.rotation * deltaRotation);
        }
    }

    private float GetSpeedForState(NavigationStates state)
    {
        switch (state)
        {
            case NavigationStates.Back: return maxSpeed * 0.25f;
            case NavigationStates.AllStop: return 0f;
            case NavigationStates.AheadOneThird: return maxSpeed * 0.33f;
            case NavigationStates.AheadStandard: return maxSpeed * 0.8f;
            case NavigationStates.AheadFull: return maxSpeed * 0.95f;
            default: return 0f;
        }
    }

    private void Ping()
    {
        float portDist = portSonar.GetComponent<SonarController>().Ping();
        float starboardDist = starboardSonar.GetComponent<SonarController>().Ping();
        float bowDist = bowSonar.GetComponent<SonarController>().Ping();
        float aftDist = aftSonar.GetComponent<SonarController>().Ping();
        float keelDist = keelSonar.GetComponent<SonarController>().Ping();

        bowText.text = $"Bow: {bowDist:F1}m";
        sternText.text = $"Stern: {aftDist:F1}m";
        portText.text = $"Port: {portDist:F1}m";
        starboardText.text = $"Starboard: {starboardDist:F1}m";
        keelText.text = $"Keel: {keelDist:F1}m";
    }
}
