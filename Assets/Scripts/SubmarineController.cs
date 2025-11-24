using UnityEngine;
using TMPro;

public class SubmarineController : MonoBehaviour
{
    public enum NavigationStates
    {
        Back, AllStop, AheadOneThird, AheadStandard, AheadFull
    }

    public NavigationStates currentState = NavigationStates.AllStop;
    [SerializeField] private AudioSource engineAudio;

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

    [SerializeField] public float batteryCharge = 100f;
    [SerializeField] private float batteryDrainRate = 0f;
    [SerializeField] public float oxygen = 100f;
    [SerializeField] private float oxygenConsumptionRate = 0.02f;

    [SerializeField] private Transform terrainRoot;

    [Header("Steering")]
    [SerializeField] private float rudderAngle = 0f;
    [SerializeField] private float maxRudder = 1f;
    [SerializeField] private float rudderStep = 0.2f;
    [SerializeField] private float turnRate = 30f;

    [Header("Pitch / Depth Control")]
    [SerializeField] private float maxPitchDegrees = 10f;
    [SerializeField] private float pitchRateDegreesPerSecond = 0.5f;

    [Header("Roll Control")]
    [SerializeField] private float rollPerStep = 3f;
    [SerializeField] private float maxRollDegrees = 25f;
    [SerializeField] private float rollRateDegreesPerSecond = 5f;

    [Header("Vessel Object (Child Mesh)")]
    [SerializeField] private Transform vessel;

    [Header("Sonar Suite")]
    [SerializeField] private GameObject portSonar;
    [SerializeField] private GameObject starboardSonar;
    [SerializeField] private GameObject bowSonar;
    [SerializeField] private GameObject aftSonar;
    [SerializeField] private GameObject keelSonar;

    [Header("Sonar Text Fields")]
    [SerializeField] private TextMeshProUGUI bowText;
    [SerializeField] private TextMeshProUGUI sternText;
    [SerializeField] private TextMeshProUGUI portText;
    [SerializeField] private TextMeshProUGUI starboardText;
    [SerializeField] private TextMeshProUGUI keelText;

    [Header("Dashboard Text Fields")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI rudderText;
    [SerializeField] private TextMeshProUGUI pitchText;
    [SerializeField] private TextMeshProUGUI oxygenText;
    [SerializeField] private TextMeshProUGUI batteryText;

    private float currentSpeed = 0f;
    private Rigidbody rb;
    private float pingTimer = 0f;
    private float pingInterval = 5f;
    private float dashboardTimer = 0f;
    private float dashboardInterval = 3f;

    private float currentPitch = 0f;
    private float targetPitch = 0f;

    private float currentYaw = 0f;
    public float currentRoll = 0f;
    private float targetRoll = 0f;
    private float speedSoundImpact = 1;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
        }

        speedSoundImpact = currentSpeed / maxSpeed;

        currentYaw = 0f;
        currentPitch = 0f;
        currentRoll = 0f;

        targetPitch = 0f;
        targetRoll = 0f;

        if (vessel == null)
            Debug.LogError("SubmarineController: Vessel transform is not assigned!");
    }

    void FixedUpdate()
    {
        speedSoundImpact = currentSpeed / maxSpeed;

        NavigationUpdate();
        ApplySteeringPitchRoll();

        pingTimer += Time.fixedDeltaTime;
        if (pingTimer >= pingInterval)
        {
            Ping();
            pingTimer = 0f;
        }

        dashboardTimer += Time.fixedDeltaTime;
        if (dashboardTimer >= dashboardInterval)
        {
            dashboard();
            dashboardTimer = 0f;
        }

        WaterFlowUpdate();
    }

    private void NavigationUpdate()
    {
        float targetSpeed = GetSpeedForState(currentState);

        if (currentSpeed < targetSpeed)
            currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.fixedDeltaTime, targetSpeed);
        else if (currentSpeed > targetSpeed)
            currentSpeed = Mathf.Max(currentSpeed - acceleration * Time.fixedDeltaTime, targetSpeed);

        Vector3 moveDir = vessel.forward;

        if (currentState == NavigationStates.Back)
            moveDir = -vessel.forward;

        terrainRoot.position -= moveDir * Mathf.Abs(currentSpeed) * Time.fixedDeltaTime;
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
                targetRoll = Mathf.Clamp(targetRoll + rollPerStep, -maxRollDegrees, maxRollDegrees);
                break;

            case NavigationButton.NavButtonType.Starboard:
                rudderAngle = Mathf.Clamp(rudderAngle + rudderStep, -maxRudder, maxRudder);
                targetRoll = Mathf.Clamp(targetRoll - rollPerStep, -maxRollDegrees, maxRollDegrees);
                break;

            case NavigationButton.NavButtonType.Dive:
                if (targetPitch >= maxPitchDegrees)
                {
                    targetPitch = maxPitchDegrees;
                }
                else
                {
                    targetPitch += 5f;
                }
                break;

            case NavigationButton.NavButtonType.Surface:
                if(targetPitch <= -1*maxPitchDegrees)
                {
                    targetPitch = -1*maxPitchDegrees;
                }
                else
                {
                    targetPitch -= 5f;
                }
                break;
        }
    }

    private void ApplySteeringPitchRoll()
    {
        float speedFactor = GetSpeedForState(currentState) / maxSpeed;
        float yawDelta = rudderAngle * turnRate * speedFactor * Time.fixedDeltaTime;

        currentYaw += yawDelta;

        float pitchStep = pitchRateDegreesPerSecond * Time.fixedDeltaTime;
        currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, pitchStep);

        float rollStep = rollRateDegreesPerSecond * Time.fixedDeltaTime;
        currentRoll = Mathf.MoveTowards(currentRoll, targetRoll, rollStep);

        currentPitch = Mathf.Clamp(currentPitch, -maxPitchDegrees, maxPitchDegrees);
        currentRoll = Mathf.Clamp(currentRoll, -maxRollDegrees, maxRollDegrees);
        Quaternion vesselRot = Quaternion.Euler(currentPitch, currentYaw, currentRoll);
        vessel.localRotation = vesselRot;
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
        }
        return 0f;
    }

    private void Ping()
    {
        float portDist = portSonar.GetComponent<SonarController>().Ping();
        float starDist = starboardSonar.GetComponent<SonarController>().Ping();
        float bowDist = bowSonar.GetComponent<SonarController>().Ping();
        float aftDist = aftSonar.GetComponent<SonarController>().Ping();
        float keelDist = keelSonar.GetComponent<SonarController>().Ping();

        bowText.text = $"Bow: {bowDist:F1}m";
        sternText.text = $"Stern: {aftDist:F1}m";
        portText.text = $"Port: {portDist:F1}m";
        starboardText.text = $"Starboard: {starDist:F1}m";
        keelText.text = $"Keel: {keelDist:F1}m";
    }

    private void dashboard()
    {
        float speed = currentSpeed * 2f;
        float rudder = rudderAngle * 30f;
        float pitch = currentPitch;
        float o2 = oxygen;
        float battery = batteryCharge;

        speedText.text = $"Speed: {speed:F1} knots";
        rudderText.text = $"Rudder: {rudder:F1}°";
        pitchText.text = $"Pitch: {-1*pitch:F1}°";
        oxygenText.text = $"O2: {o2:F1}%";
        batteryText.text = $"Battery: {battery:F1}%";
    }

    private float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        if (a < -180f) a += 360f;
        return a;
    }

    private void WaterFlowUpdate()
    {
        float speedFactor = currentSpeed / maxSpeed;
        speedFactor = Mathf.Clamp01(speedFactor);
        engineAudio.volume = Mathf.Lerp(0.2f, 1.0f, speedFactor);
        engineAudio.pitch = Mathf.Lerp(0.8f, 1.6f, speedFactor);
    }

}
