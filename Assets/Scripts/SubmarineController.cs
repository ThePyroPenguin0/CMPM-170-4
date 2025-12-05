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
    [SerializeField] private AudioSource engineAudio;
    [SerializeField] private AudioSource pingAudio;

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
    [SerializeField] private float batteryDrainRate = 0f; // unused now but kept for inspector
    [SerializeField] private float pingBatteryCost = 5f;
    [SerializeField] public float oxygen = 100f;
    [SerializeField] private float oxygenConsumptionRate = 0.02f;

    [SerializeField] private Transform terrainRoot;


    [SerializeField] private float baseBatteryDrainPerSecond = 1f;
    [SerializeField] private float idleDrainFactor = 0.1f;
    [SerializeField] private float shallowDepthY = 0f;
    [SerializeField] private float deepDepthY = -50f;
    [SerializeField] private float shallowDepthDrainMultiplier = 2f;
    [SerializeField] private float deepDepthDrainMultiplier = 0.5f;
    [SerializeField] AudioSource buttonAudioSource;
    [SerializeField] AudioSource errorAudioSource;
    [SerializeField] AudioSource collisionAudioSource;

    [Header("Steering")]
    [SerializeField] private float rudderAngle = 0f;
    [SerializeField] private float maxRudder = 1f;
    [SerializeField] private float rudderStep = 1f;
    [SerializeField] private float turnRate = 10f;

    [Header("Pitch / Depth Control")]
    [SerializeField] private float maxPitchDegrees = 10f;
    [SerializeField] private float pitchRateDegreesPerSecond = 0.5f;

    [Header("Roll Control")]
    [SerializeField] private float rollPerStep = 15f;
    [SerializeField] private float maxRollDegrees = 15f;
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
    [Header("Dashboard Text Fields")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI rudderText;
    [SerializeField] private TextMeshProUGUI pitchText;

    private float currentSpeed = 0f;
    private Rigidbody rb;
    private float pingTimer = 0f;
    private float pingInterval = 5f;
    private float dashboardTimer = 0f;
    private float dashboardInterval = 1f;
    private float O2Timer = 0f;
    private float O2Interval = 1f;

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
        rb.linearDamping = 2f;
        rb.angularDamping = 2f;

        UpdateBatteryText();
        StartCoroutine(BatteryDrainRoutine()); // now uses speed + depth (NEW logic inside)
        StartCoroutine(OxygenDrainRoutine());

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
            StartCoroutine(PlayPingAudioHalfway());
        }
        dashboardTimer += Time.fixedDeltaTime;
        if (dashboardTimer >= dashboardInterval)
        {
            dashboard();
            dashboardTimer = 0f;
        }

        O2Timer += Time.fixedDeltaTime;
        if (O2Timer >= O2Interval)
        {
            O2Timer = 0f;
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
                {
                    currentState++;
                    buttonAudioSource.Play();
                }
                else
                {
                    errorAudioSource.Play();
                }
                batteryCharge -= batteryDrainRate * 3;
                break;

            case NavigationButton.NavButtonType.Back:
                if (currentState > NavigationStates.Back)
                {
                    currentState--;
                    buttonAudioSource.Play();
                }
                else
                {
                    errorAudioSource.Play();
                }
                batteryCharge -= batteryDrainRate * 3;
                break;

            case NavigationButton.NavButtonType.Port:
                if (rudderAngle > -maxRudder)
                {
                    rudderAngle -= rudderStep;
                    targetRoll += rollPerStep;
                    batteryCharge -= batteryDrainRate * 1;
                    buttonAudioSource.Play();
                }
                else
                {
                    errorAudioSource.Play();
                }
                break;

            case NavigationButton.NavButtonType.Starboard:
                if (rudderAngle < maxRudder)
                {
                    rudderAngle += rudderStep;
                    targetRoll -= rollPerStep;
                    batteryCharge -= batteryDrainRate * 1;
                    buttonAudioSource.Play();
                    
                }
                else
                {
                    errorAudioSource.Play();
                }
                break;

            case NavigationButton.NavButtonType.Dive:
                if (targetPitch >= maxPitchDegrees)
                {
                    targetPitch = maxPitchDegrees;
                    batteryCharge -= batteryDrainRate * 5;
                    errorAudioSource.Play();
                }
                else
                {
                    targetPitch += 5f;
                    batteryCharge -= batteryDrainRate * 5;
                    buttonAudioSource.Play();
                }
                break;

            case NavigationButton.NavButtonType.Surface:
                if (targetPitch <= -1 * maxPitchDegrees)
                {
                    targetPitch = -1 * maxPitchDegrees;
                }
                else
                {
                    targetPitch -= 5f;
                    buttonAudioSource.Play();
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

        if (Mathf.Abs(rudderAngle) < 0.01f)
        {
            targetRoll = 0f;
        }
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
        ModifyBattery(-pingBatteryCost);

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

            // if (depth < 0) <= This should be constant.
            // {
            //     // deeper = more O2 consumption
            //     depthMultiplier = 1f + Mathf.Abs(depth) / 20f;
            // }

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

    // private void OnCollisionEnter(Collision collision)
    // {
    //     if (!canTakeCollision) return;

    //     // Only handle collision if the collided object is on the "terrain" layer
    //     if (collision.gameObject.layer != LayerMask.NameToLayer("Terrain")){
    //         Debug.Log($"Collided with non-terrain object. Layer: {collision.gameObject.layer} ({LayerMask.LayerToName(collision.gameObject.layer)})");
    //         return;
    //     }
    //     Vector3 hitNormal = collision.contacts.Length > 0
    //         ? collision.contacts[0].normal
    //         : -transform.forward;

    //     float relativeSpeed = collision.relativeVelocity.magnitude;

    //     HandleCollision(hitNormal, relativeSpeed);
    // }

    public void HandleCollision()
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

        // // Nudge the sub away from the wall a bit
        // // Since the world moves around us, we move the terrain in the opposite direction
        // if (terrainRoot != null)
        // {
        //     Vector3 knockback = hitNormal * knockbackDistance + Vector3.up * knockbackUpAmount;
        //     terrainRoot.position += knockback;
        // }
        // else
        // {
        //     // fallback: move the sub instead
        //     transform.position += (-hitNormal * knockbackDistance) + Vector3.up * knockbackUpAmount;
        // }

        //Apply damage based on impact speed
        ApplyDamage(5f);

        if (playerCamera != null)
        {
            StartCoroutine(ScreenShakeCoroutine());
            collisionAudioSource.Play();
        }
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
    private void dashboard()
    {
        float speed = currentSpeed * 2f;
        float rudder = rudderAngle * 30f;
        float pitch = currentPitch;
        float o2 = oxygen;
        float battery = batteryCharge;

        string stateString = System.Text.RegularExpressions.Regex.Replace(currentState.ToString(), "([a-z])([A-Z])", "$1 $2");
        string rudderDirection = rudder > 0 ? "starboard" : (rudder < 0 ? "port" : "center");
        float intendedPitch = targetPitch;

        speedText.text = $"Making {speed:F1} knots; {stateString}";
        rudderText.text = $"Rudder: {rudder:F1}°, yawing {rudderDirection}";
        pitchText.text = $"Pitch: {-1 * pitch:F1}°, target {-1 * intendedPitch:F1}°";
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
        engineAudio.pitch = Mathf.Lerp(0.8f, 1.6f, speedFactor);
    }

    private System.Collections.IEnumerator PlayPingAudioHalfway()
    {
        yield return new WaitForSeconds(pingInterval / 2f);
        pingAudio.Play();
    }

}
