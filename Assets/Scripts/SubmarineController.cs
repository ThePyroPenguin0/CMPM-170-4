using UnityEngine;
using TMPro;
using System.Collections;

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
    [SerializeField] private float batteryDrainRate = 0f; // unused now but kept for inspector
    [SerializeField] private float pingBatteryCost = 5f;
    [SerializeField] public float oxygen = 100f;
    [SerializeField] private float oxygenConsumptionRate = 0.02f;
    [SerializeField] private Transform terrainRoot;

    [Header("Battery Tuning (NEW)")]
    [Tooltip("Base drain at full speed at 1s tick, before depth modifiers.")]
    [SerializeField] private float baseBatteryDrainPerSecond = 1f;
    [Tooltip("Fraction of drain that still happens when stopped (life support, etc).")]
    [SerializeField] private float idleDrainFactor = 0.1f;
    [Tooltip("Y position considered 'surface' for efficiency math.")]
    [SerializeField] private float shallowDepthY = 0f;
    [Tooltip("Y position considered 'deep' for efficiency math (more negative).")]
    [SerializeField] private float deepDepthY = -50f;
    [Tooltip("Multiplier at surface: >1 means less efficient near surface.")]
    [SerializeField] private float shallowDepthDrainMultiplier = 2f;
    [Tooltip("Multiplier at deep depth: <1 means more efficient deeper.")]
    [SerializeField] private float deepDepthDrainMultiplier = 0.5f;

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
    [SerializeField] private GameObject collisionPointSphere;

    [Header("Sonar Text Fields")]
    [SerializeField] private TextMeshProUGUI bowText;
    [SerializeField] private TextMeshProUGUI sternText;
    [SerializeField] private TextMeshProUGUI portText;
    [SerializeField] private TextMeshProUGUI starboardText;
    [SerializeField] private TextMeshProUGUI keelText;

    [Header("Power Text Fields")]
    [SerializeField] public TextMeshProUGUI batteryText;
    [SerializeField] public TextMeshProUGUI oxygenText;

    [Header("Damage & Collision (NEW)")]
    [SerializeField] private float hullIntegrity = 100f;
    [SerializeField] private float collisionDamageMultiplier = 2f;
    [SerializeField] private float minCollisionDamage = 5f;
    [SerializeField] private float maxCollisionDamage = 30f;
    [SerializeField] private float collisionInvulnerabilityTime = 1f;
    [Tooltip("How far to push the sub away from the collision normal.")]
    [SerializeField] private float knockbackDistance = 1f;
    [Tooltip("Slight upward component to keep you from grinding into the wall.")]
    [SerializeField] private float knockbackUpAmount = 0.5f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float shakeDuration = 0.4f;
    [SerializeField] private float shakeMagnitude = 0.2f;

    private bool canTakeCollision = true;

    private float currentSpeed = 0f;
    private Rigidbody rb;
    private float pingTimer = 0f;
    private float pingInterval = 5f;

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

        UpdateBatteryText();
        StartCoroutine(BatteryDrainRoutine()); // now uses speed + depth (NEW logic inside)
        StartCoroutine(OxygenDrainRoutine());
    }

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

        // World moves around the sub
        terrainRoot.position -= moveDirection * Mathf.Abs(currentSpeed) * Time.fixedDeltaTime;
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
        ModifyBattery(-pingBatteryCost);

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

    // --- RESOURCE SYSTEMS ---

    // NEW: dynamic battery drain that depends on speed + depth
    private IEnumerator BatteryDrainRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);  // tick every second

            // 0..1 based on currentSpeed / maxSpeed
            float rawSpeedFactor = (maxSpeed > 0f) ? Mathf.Abs(currentSpeed) / maxSpeed : 0f;
            // even at AllStop we still have some base load
            float speedFactor = Mathf.Lerp(idleDrainFactor, 1f, rawSpeedFactor);

            // depth based on terrainRoot Y (deeper = more negative)
            float depthY = terrainRoot != null ? terrainRoot.position.y : transform.position.y;

            // t = 0 at surface, 1 at deepDepthY (or beyond)
            float t = Mathf.InverseLerp(shallowDepthY, deepDepthY, depthY);
            // we clamp to keep it sane if you go beyond
            t = Mathf.Clamp01(t);

            // near surface -> shallowDepthDrainMultiplier (e.g. 2x drain)
            // deep -> deepDepthDrainMultiplier (e.g. 0.5x drain)
            float depthMultiplier = Mathf.Lerp(shallowDepthDrainMultiplier, deepDepthDrainMultiplier, t);

            float drainThisTick = baseBatteryDrainPerSecond * speedFactor * depthMultiplier;

            if (drainThisTick > 0f)
            {
                ModifyBattery(-drainThisTick);
            }
        }
    }

    private IEnumerator OxygenDrainRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            float depth = transform.position.y;
            float depthMultiplier = 1f;

            if (depth < 0)
            {
                // deeper = more O2 consumption
                depthMultiplier = 1f + Mathf.Abs(depth) / 20f;
            }

            ModifyOxygen(-oxygenConsumptionRate * depthMultiplier);
        }
    }

    private void ModifyBattery(float amount)
    {
        batteryCharge += amount;
        batteryCharge = Mathf.Clamp(batteryCharge, 0f, 100f);
        UpdateBatteryText();
    }

    private void ModifyOxygen(float amount)
    {
        oxygen += amount;
        oxygen = Mathf.Clamp(oxygen, 0f, 100f);
        UpdateOxygenText();
    }

    private void UpdateBatteryText()
    {
        if (batteryText != null)
        {
            batteryText.text = $"Battery: {Mathf.RoundToInt(batteryCharge)}%";
        }
    }

    private void UpdateOxygenText()
    {
        if (oxygenText != null)
        {
            oxygenText.text = $"Oxygen: {oxygen:F2}%";
        }
    }

    // --- COLLISION 

    private void OnCollisionEnter(Collision collision)
    {
        if (!canTakeCollision) return;

        // If you want to be stricter, you can check tag or layer:
        // if (!collision.transform.IsChildOf(terrainRoot)) return;

        Vector3 hitNormal = collision.contacts.Length > 0
            ? collision.contacts[0].normal
            : -transform.forward;

        float relativeSpeed = collision.relativeVelocity.magnitude;

        HandleCollision(hitNormal, relativeSpeed);
    }

    private void HandleCollision(Vector3 hitNormal, float relativeSpeed)
    {
        canTakeCollision = false;

        //Stop movement immediately
        currentState = NavigationStates.AllStop;
        currentSpeed = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Nudge the sub away from the wall a bit
        // Since the world moves around us, we move the terrain in the opposite direction
        if (terrainRoot != null)
        {
            Vector3 knockback = hitNormal * knockbackDistance + Vector3.up * knockbackUpAmount;
            terrainRoot.position += knockback;
        }
        else
        {
            // fallback: move the sub instead
            transform.position += (-hitNormal * knockbackDistance) + Vector3.up * knockbackUpAmount;
        }

        //Apply damage based on impact speed
        float rawDamage = relativeSpeed * collisionDamageMultiplier;
        float damage = Mathf.Clamp(rawDamage, minCollisionDamage, maxCollisionDamage);
        ApplyDamage(damage);

        if (playerCamera != null)
            StartCoroutine(ScreenShakeCoroutine());

        StartCoroutine(CollisionCooldownCoroutine());
    }

    private void ApplyDamage(float amount)
    {
        hullIntegrity -= amount;
        hullIntegrity = Mathf.Clamp(hullIntegrity, 0f, 100f);
        Debug.Log($"Hull integrity: {hullIntegrity}% (took {amount} damage)");

        // TODO: hook into UI or game-over state when hullIntegrity <= 0
    }

    private IEnumerator CollisionCooldownCoroutine()
    {
        yield return new WaitForSeconds(collisionInvulnerabilityTime);
        canTakeCollision = true;
    }

    private IEnumerator ScreenShakeCoroutine()
    {
        Vector3 originalPos = playerCamera.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float t = elapsed / shakeDuration;
            float falloff = 1f - t; // simple linear falloff

            Vector3 offset = Random.insideUnitSphere * shakeMagnitude * falloff;
            playerCamera.transform.localPosition = originalPos + offset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        playerCamera.transform.localPosition = originalPos;
    }
}
