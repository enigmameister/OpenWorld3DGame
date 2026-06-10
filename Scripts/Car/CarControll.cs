using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarControll : MonoBehaviour
{
    [Header("Koła (wizual / steering)")]
    public WheelControl FL;
    public WheelControl FR;
    public WheelControl RL;
    public WheelControl RR;

    [Header("Środek masy")]
    public Vector3 centerOfMass = new Vector3(0f, -0.9f, 0.22f);

    [Header("Arcade Drive")]
    public float acceleration = 35f;
    public float reverseAcceleration = 12f;
    public float brakeForce = 12f;
    public float coastDrag = 0.7f;

    [Header("Prędkość")]
    public float maxSpeedKPH = 180f;
    public float maxReverseKPH = 28f;

    [Header("Stop Assist")]
    public float fullStopSpeedKPH = 1.2f;
    public float fullStopThrottleDeadzone = 0.05f;

    [Header("Pre-Race RPM")]
    public float preRaceIdleRPM = 1100f;
    public float preRaceMaxRPM = 4200f;
    public float preRaceRpmResponse = 8f;

    [Header("Pre-Race Speedometer FX")]
    public bool usePreRaceSpeedometerFx = true;
    public float preRaceFakeSpeedMax = 150f;
    public float preRaceFakeSpeedResponse = 10f;

    private float preRaceFakeSpeedKPH = 0f;

    [Header("Top speed pacing")]
    [Tooltip("Im niżej, tym wolniej auto dochodzi do vmax. Dla szybkich aut 0.12-0.20, dla zwykłych 0.24-0.32.")]
    [Range(0.05f, 1f)] public float highSpeedAccelFactor = 0.28f;
    [Tooltip("Mały impuls do ruszenia z miejsca.")]
    public float launchBoostSpeed = 1.2f;
    [Tooltip("Minimalny gaz do impulsu startowego.")]
    [Range(0f, 1f)] public float launchMinThrottle = 0.2f;

    [Header("Skręt")]
    public float steerAtLow = 24f;
    public float steerAtHigh = 10f;
    public float steerResponse = 3.5f;
    public float maxVisualSteerAngle = 22f;

    [Header("Ground Check")]
    public float groundCheckDistance = 1.05f;
    public LayerMask groundMask = ~0;

    [Header("Grip / Stabilizacja")]
    public float lateralGrip = 3.2f;
    public float yawStability = 1.3f;
    public float extraDownforce = 12f;

    [Header("Arcade NFS Handling")]
    public float highSpeedGripAssist = 1.8f;
    public float lateralVelocityKill = 2.8f;
    public float steeringSpeedLossReducer = 0.35f;
    public float rollStability = 0.25f;
    public float pitchStability = 0.45f;

    [Header("Arcade Drift")]
    public KeyCode driftKey = KeyCode.Space;
    public float driftSideGrip = 0.65f;
    public float normalSideGrip = 3.5f;
    public float driftYawAssist = 1.4f;
    public float driftMinSpeedKmh = 45f;

    private bool externalHandbrakeInput;

    [Header("Visual Body Lean")]
    public Transform carVisualRoot;
    public float visualLeanAngle = 6f;
    public float visualYawAngle = 5f;
    public float visualLeanSmooth = 4f;
    public float visualReturnSmooth = 2.2f;

    [Header("Powietrze / hopki")]
    public float airControl = 0.12f;
    public float airDownforce = 14f;

    [Header("Nitro Physics")]
    public float nitroForwardForce = 1200f;
    public float nitroAccelerationMultiplier = 1.05f;
    public float nitroMaxSpeedBonus = 25f;

    [Header("Biegi automatyczne")]
    public int currentGear = 1;
    public int maxGear = 6;
    public float[] gearSpeedLimits = { 20f, 40f, 65f, 95f, 130f, 180f };

    [Header("Gearbox")]
    public float shiftUpBuffer = 0f;
    public float shiftDownBuffer = 25f;

    [Header("Shift smoothing")]
    public float shiftCooldown = 0.35f;
    [Range(0.85f, 1f)] public float shiftSpeedDrop = 0.96f;
    public float downshiftBufferKPH = 25f;

    [Header("RPM do zegara")]
    public float maxRPM = 6500f;
    [Range(0.01f, 0.5f)] public float rpmSmoothing = 0.12f;

    [Header("Światła")]
    public Light[] brakeLights;
    public Light[] reverseLights;

    [Header("UI / Debug")]
    public int currentSpeedKPH;
    public bool isReversing;
    public bool isControlled = true;

    private Rigidbody rb;
    private InputActions input;
    private Vector2 moveInput;

    private float throttleInput;
    private float steerInput;
    private float steerVisual;
    private float rpmSmoothed = 1000f;

    private float shiftTimer = 0f;
    private int lastGear = 1;

    [HideInInspector] public bool raceStartLock = false;

    /// <summary>
    /// AI Car
    /// </summary>

    [Header("AI Input")]
    public bool useExternalInput = false;
    private Vector2 externalMoveInput;


    public void SetExternalInput(float steer, float throttle)
    {
        externalMoveInput = new Vector2(
            Mathf.Clamp(steer, -1f, 1f),
            Mathf.Clamp(throttle, -1f, 1f)
        );

        if (Mathf.Abs(steer) < 0.01f && Mathf.Abs(throttle) < 0.01f)
            externalHandbrakeInput = false;
    }

    public void SetExternalHandbrake(bool value)
    {
        externalHandbrakeInput = value;
    }
    /// <summary>
    /// 
    /// </summary>
    void Awake()
    {
        input = new InputActions();
    }

    void OnEnable() => input.Enable();
    void OnDisable() => input.Disable();

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMass;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 2.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.maxAngularVelocity = 6f;

        EnsureWheelRefs();
    }

    void FixedUpdate()
    {
        if (!isControlled)
            return;

        if (shiftTimer > 0f)
            shiftTimer -= Time.fixedDeltaTime;

        ReadInput();

        if (raceStartLock)
        {
            currentSpeedKPH = 0;
            isReversing = false;
            currentGear = 1;

            // auto stoi w miejscu
            Vector3 vel = rb.linearVelocity;
            vel.x = 0f;
            vel.z = 0f;
            rb.linearVelocity = vel;

            rb.angularVelocity = new Vector3(0f, rb.angularVelocity.y * 0.5f, 0f);

            // koła wizualnie na wprost
            if (FL && FL.WheelCollider != null) FL.WheelCollider.steerAngle = 0f;
            if (FR && FR.WheelCollider != null) FR.WheelCollider.steerAngle = 0f;

            // RPM reaguje na gaz
            float rev01 = Mathf.Clamp01(Mathf.Max(0f, throttleInput));
            float targetRPM = Mathf.Lerp(preRaceIdleRPM, preRaceMaxRPM, rev01);
            rpmSmoothed = Mathf.Lerp(rpmSmoothed, targetRPM, preRaceRpmResponse * Time.fixedDeltaTime);

            // fake speed do HUD-u
            if (usePreRaceSpeedometerFx)
            {
                float targetFakeSpeed = Mathf.Lerp(0f, preRaceFakeSpeedMax, rev01);
                preRaceFakeSpeedKPH = Mathf.Lerp(preRaceFakeSpeedKPH, targetFakeSpeed, preRaceFakeSpeedResponse * Time.fixedDeltaTime);
            }
            else
            {
                preRaceFakeSpeedKPH = 0f;
            }

            return;
        }

        preRaceFakeSpeedKPH = 0f;

        UpdateSpeed();
        UpdateReverseState();

        bool onGround = IsGroundedSimple();

        ApplyDrive();
        ApplySteering();
        UpdateVisualBodyLean(steerInput);

        if (onGround)
        {
            ApplyLateralGrip();
            ApplyYawStability();
            ApplyBodyStability();
            ApplyDownforce();
            ApplyCurbAssist();
        }
        else
        {
            ApplyAirborneControl();
        }

        ApplyArcadeStability();
        ApplyLateralGripAssist();
        ApplyDriftAssist(steerInput);
        LimitTopSpeed();
        ApplyFullStopAssist();
        UpdateAutomaticGearbox();
        UpdateRpmForUI();
        UpdateWheelVisualSteer();
        UpdateLights();
    }

    void ApplyArcadeStability()
    {
        if (rb == null) return;

        Vector3 localAngular = transform.InverseTransformDirection(rb.angularVelocity);

        localAngular.x *= pitchStability;
        localAngular.z *= rollStability;

        rb.angularVelocity = transform.TransformDirection(localAngular);
    }

    public int GetDisplaySpeedKPH()
    {
        if (raceStartLock && usePreRaceSpeedometerFx)
            return Mathf.RoundToInt(preRaceFakeSpeedKPH);

        return currentSpeedKPH;
    }

    void ReadInput()
    {
        if (useExternalInput)
        {
            moveInput = externalMoveInput;
        }
        else
        {
            moveInput = input.Car.Movement.ReadValue<Vector2>();
        }

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        if (Mathf.Abs(moveInput.x) < 0.05f) moveInput.x = 0f;
        if (Mathf.Abs(moveInput.y) < 0.05f) moveInput.y = 0f;

        steerInput = moveInput.x;
        throttleInput = moveInput.y;
    }

    void UpdateSpeed()
    {
        currentSpeedKPH = Mathf.RoundToInt(rb.linearVelocity.magnitude * 3.6f);
    }

    void UpdateReverseState()
    {
        Vector3 localVel = GetLocalVelocity();
        float forwardSpeedKPH = localVel.z * 3.6f;

        if (throttleInput < -0.1f && forwardSpeedKPH < 3f)
            isReversing = true;

        if (throttleInput > 0.1f && forwardSpeedKPH > -3f)
            isReversing = false;

        if (Mathf.Abs(throttleInput) < 0.05f && Mathf.Abs(forwardSpeedKPH) < 1f)
            isReversing = false;
    }

    void ApplyDrive()
    {
        Vector3 localVel = GetLocalVelocity();

        if (Mathf.Abs(throttleInput) > 0.05f)
            rb.WakeUp();

        bool nitroActive = false;

        if (TryGetComponent<NitroSystem>(out var nitro))
            nitroActive = nitro.isUsingNitro;

        float currentMaxSpeedKPH = maxSpeedKPH;

        if (nitroActive)
            currentMaxSpeedKPH += nitroMaxSpeedBonus;

        float maxForwardMS = currentMaxSpeedKPH / 3.6f;
        float maxBackwardMS = maxReverseKPH / 3.6f;

        float gearMul = 1f;
        if (!isReversing)
        {
            switch (currentGear)
            {
                case 1: gearMul = 0.26f; break;
                case 2: gearMul = 0.42f; break;
                case 3: gearMul = 0.62f; break;
                case 4: gearMul = 0.78f; break;
                case 5: gearMul = 0.90f; break;
                default: gearMul = 1f; break;
            }
        }

        if (!isReversing)
        {
            if (throttleInput > 0.01f)
            {
                if (throttleInput > launchMinThrottle && localVel.z < 0.15f)
                    localVel.z = launchBoostSpeed;

                float targetSpeed = maxForwardMS * gearMul * Mathf.Clamp01(throttleInput);

                float speed01 = Mathf.InverseLerp(0f, maxForwardMS, Mathf.Max(0f, localVel.z));
                float accelMulBySpeed = Mathf.Lerp(1f, highSpeedAccelFactor, speed01);

                float finalAcceleration = acceleration;

                if (nitroActive)
                    finalAcceleration *= nitroAccelerationMultiplier;

                localVel.z = Mathf.MoveTowards(
                    localVel.z,
                    targetSpeed,
                    finalAcceleration * accelMulBySpeed * Time.fixedDeltaTime
                );

                if (nitroActive && throttleInput > 0.1f && currentSpeedKPH < currentMaxSpeedKPH)
                {
                    float nitroSpeed01 = Mathf.InverseLerp(40f, currentMaxSpeedKPH, currentSpeedKPH);
                    float softNitroForce = nitroForwardForce * (1f - nitroSpeed01);

                    rb.AddForce(transform.forward * softNitroForce, ForceMode.Acceleration);
                }
            }
            else if (throttleInput < -0.1f)
            {
                localVel.z = Mathf.MoveTowards(
                    localVel.z,
                    0f,
                    brakeForce * Time.fixedDeltaTime
                );
            }
            else
            {
                localVel.z = Mathf.MoveTowards(
                    localVel.z,
                    0f,
                    coastDrag * Time.fixedDeltaTime
                );
            }
        }
        else
        {
            if (throttleInput < -0.01f)
            {
                if (Mathf.Abs(throttleInput) > launchMinThrottle && localVel.z > -0.15f)
                    localVel.z = -launchBoostSpeed * 0.75f;

                float reverseTargetSpeed = -maxBackwardMS * Mathf.Abs(throttleInput);

                localVel.z = Mathf.MoveTowards(
                    localVel.z,
                    reverseTargetSpeed,
                    reverseAcceleration * Time.fixedDeltaTime
                );
            }
            else if (throttleInput > 0.1f)
            {
                localVel.z = Mathf.MoveTowards(
                    localVel.z,
                    0f,
                    brakeForce * Time.fixedDeltaTime
                );
            }
            else
            {
                localVel.z = Mathf.MoveTowards(
                    localVel.z,
                    0f,
                    coastDrag * Time.fixedDeltaTime
                );
            }
        }

        if (Mathf.Abs(throttleInput) < fullStopThrottleDeadzone &&
        Mathf.Abs(localVel.z * 3.6f) < fullStopSpeedKPH)
        {
            localVel.z = 0f;

            Vector3 av = rb.angularVelocity;
            av.y = 0f;
            rb.angularVelocity = av;
        }
        rb.linearVelocity = transform.TransformDirection(localVel);
    }

    void ApplySteering()
    {
        float speedFactor = Mathf.InverseLerp(0f, maxSpeedKPH, currentSpeedKPH);
        float steerPower = Mathf.Lerp(steerAtLow, steerAtHigh, speedFactor);

        float lowSpeedLimiter = Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(5f, 35f, currentSpeedKPH));
        float highSpeedResponse = Mathf.Lerp(1f, 0.65f, speedFactor);
        float reverseMul = isReversing ? -0.55f : 1f;

        float targetYaw = steerInput * steerPower * lowSpeedLimiter * reverseMul;
        steerVisual = Mathf.Lerp(
            steerVisual,
            targetYaw,
            steerResponse * highSpeedResponse * Time.fixedDeltaTime
        );

        Vector3 angVel = rb.angularVelocity;
        float targetYawVel = steerVisual * Mathf.Deg2Rad;
        angVel.y = Mathf.Lerp(angVel.y, targetYawVel, 4f * Time.fixedDeltaTime);

        rb.angularVelocity = new Vector3(angVel.x, angVel.y, angVel.z);
    }

    void ApplyLateralGrip()
    {
        Vector3 localVel = GetLocalVelocity();
        localVel.x = Mathf.Lerp(localVel.x, 0f, lateralGrip * Time.fixedDeltaTime);
        rb.linearVelocity = transform.TransformDirection(localVel);
    }

    void ApplyYawStability()
    {
        Vector3 ang = rb.angularVelocity;
        ang.y = Mathf.Lerp(ang.y, ang.y * 0.85f, yawStability * 0.1f * Time.fixedDeltaTime);
        rb.angularVelocity = new Vector3(ang.x, ang.y, ang.z);
    }

    void ApplyBodyStability()
    {
        Vector3 localAngVel = transform.InverseTransformDirection(rb.angularVelocity);

        localAngVel.x = Mathf.Lerp(localAngVel.x, 0f, 5.5f * Time.fixedDeltaTime);
        localAngVel.z = Mathf.Lerp(localAngVel.z, 0f, 4.5f * Time.fixedDeltaTime);

        rb.angularVelocity = transform.TransformDirection(localAngVel);

        bool boosting = false;
        if (TryGetComponent<NitroSystem>(out var nitro))
            boosting = nitro.isUsingNitro;

        if ((throttleInput > 0.1f || boosting) && currentSpeedKPH < 120)
        {
            rb.AddForceAtPosition(
                -transform.up * 2200f,
                transform.position + transform.forward * 1.25f,
                ForceMode.Force
            );
        }
    }

    void ApplyDownforce()
    {
        rb.AddForce(-transform.up * extraDownforce, ForceMode.Acceleration);
    }

    void ApplyAirborneControl()
    {
        rb.AddForce(Vector3.down * airDownforce, ForceMode.Acceleration);

        if (Mathf.Abs(steerInput) > 0.01f)
            rb.AddTorque(Vector3.up * steerInput * airControl, ForceMode.Acceleration);
    }

    void ApplyCurbAssist()
    {
        if (throttleInput <= 0.1f) return;
        if (currentSpeedKPH > 35) return;

        rb.AddForce(transform.forward * 6f, ForceMode.Acceleration);
        rb.AddForce(Vector3.up * 2.2f, ForceMode.Acceleration);
    }

    void ApplyFullStopAssist()
    {
        if (rb == null) return;

        bool noInput =
            Mathf.Abs(throttleInput) < fullStopThrottleDeadzone &&
            Mathf.Abs(steerInput) < 0.05f;

        if (!noInput)
            return;

        if (currentSpeedKPH <= fullStopSpeedKPH)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            currentSpeedKPH = 0;
            isReversing = false;

            rb.Sleep();
        }
    }
    void LimitTopSpeed()
    {
        Vector3 localVel = GetLocalVelocity();

        bool nitroActive = false;

        if (TryGetComponent<NitroSystem>(out var nitro))
            nitroActive = nitro.isUsingNitro;

        float currentMaxSpeedKPH = nitroActive ? maxSpeedKPH + nitroMaxSpeedBonus : maxSpeedKPH;
        float maxForwardMS = currentMaxSpeedKPH / 3.6f;
        float maxReverseMS = maxReverseKPH / 3.6f;

        localVel.z = Mathf.Clamp(localVel.z, -maxReverseMS, maxForwardMS);

        rb.linearVelocity = transform.TransformDirection(localVel);
    }

    void UpdateAutomaticGearbox()
    {
        if (isReversing)
        {
            currentGear = 1;
            return;
        }

        if (gearSpeedLimits == null || gearSpeedLimits.Length == 0)
            return;

        if (shiftTimer > 0f)
            return;

        currentGear = Mathf.Clamp(currentGear, 1, maxGear);

        int thresholds = Mathf.Min(maxGear - 1, gearSpeedLimits.Length);

        // SHIFT UP
        if (currentGear < maxGear)
        {
            int upIndex = currentGear - 1;

            if (upIndex < thresholds && currentSpeedKPH >= gearSpeedLimits[upIndex])
            {
                ChangeGear(currentGear + 1);
                return;
            }
        }

        // SHIFT DOWN z buforem, żeby nie skakało 5/6
        if (currentGear > 1)
        {
            int downIndex = currentGear - 2;

            if (downIndex >= 0 && downIndex < thresholds)
            {
                float downshiftSpeed = gearSpeedLimits[downIndex] - downshiftBufferKPH;

                if (currentSpeedKPH < downshiftSpeed)
                {
                    ChangeGear(currentGear - 1);
                    return;
                }
            }
        }
    }

    void ChangeGear(int newGear)
    {
        newGear = Mathf.Clamp(newGear, 1, maxGear);

        if (newGear == currentGear)
            return;

        lastGear = currentGear;
        currentGear = newGear;
        shiftTimer = shiftCooldown;

        Vector3 localVel = GetLocalVelocity();
        localVel.z *= shiftSpeedDrop;
        rb.linearVelocity = transform.TransformDirection(localVel);
    }

    void UpdateRpmForUI()
    {
        if (isReversing)
        {
            float revT = Mathf.Clamp01(currentSpeedKPH / Mathf.Max(1f, maxReverseKPH));
            float targetRevRPM = Mathf.Lerp(1200f, 4200f, revT);
            rpmSmoothed = Mathf.Lerp(rpmSmoothed, targetRevRPM, rpmSmoothing);
            return;
        }

        if (gearSpeedLimits == null || gearSpeedLimits.Length == 0)
        {
            rpmSmoothed = Mathf.Lerp(rpmSmoothed, 1000f, rpmSmoothing);
            return;
        }

        float minSpeed = currentGear <= 1 ? 0f : gearSpeedLimits[Mathf.Clamp(currentGear - 2, 0, gearSpeedLimits.Length - 1)];
        float maxSpeed = gearSpeedLimits[Mathf.Clamp(currentGear - 1, 0, gearSpeedLimits.Length - 1)];
        float t = Mathf.Clamp01((currentSpeedKPH - minSpeed) / Mathf.Max(1f, maxSpeed - minSpeed));

        float targetRPM = Mathf.Lerp(1000f, maxRPM, t);
        rpmSmoothed = Mathf.Lerp(rpmSmoothed, targetRPM, rpmSmoothing);
    }

    void UpdateWheelVisualSteer()
    {
        float speed01 = Mathf.InverseLerp(0f, maxSpeedKPH, currentSpeedKPH);
        float visualSteer = Mathf.Lerp(maxVisualSteerAngle, maxVisualSteerAngle * 0.55f, speed01) * steerInput;

        if (FL != null && FL.WheelCollider != null)
            FL.WheelCollider.steerAngle = visualSteer;

        if (FR != null && FR.WheelCollider != null)
            FR.WheelCollider.steerAngle = visualSteer;
    }

    void UpdateLights()
    {
        bool braking = (!isReversing && throttleInput < -0.1f) || (isReversing && throttleInput > 0.1f);
        bool reversing = isReversing;

        if (brakeLights != null)
        {
            foreach (var l in brakeLights)
                if (l) l.enabled = braking;
        }

        if (reverseLights != null)
        {
            foreach (var l in reverseLights)
                if (l) l.enabled = reversing;
        }
    }

    bool IsGroundedSimple()
    {
        Vector3 origin = transform.position + Vector3.up * 0.25f;
        float radius = 0.45f;

        return Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out _,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    void EnsureWheelRefs()
    {
        if (FL != null && FL.WheelCollider == null) FL.WheelCollider = FL.GetComponent<WheelCollider>();
        if (FR != null && FR.WheelCollider == null) FR.WheelCollider = FR.GetComponent<WheelCollider>();
        if (RL != null && RL.WheelCollider == null) RL.WheelCollider = RL.GetComponent<WheelCollider>();
        if (RR != null && RR.WheelCollider == null) RR.WheelCollider = RR.GetComponent<WheelCollider>();
    }

    Vector3 GetLocalVelocity()
    {
        return transform.InverseTransformDirection(rb.linearVelocity);
    }

    public int GetDisplayRPM()
    {
        return Mathf.RoundToInt(Mathf.Max(1000f, rpmSmoothed));
    }

    void ApplyLateralGripAssist()
    {
        if (rb == null) return;

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;
        float currentMaxSpeedKPH = maxSpeedKPH;

        if (TryGetComponent<NitroSystem>(out var nitro) && nitro.isUsingNitro)
            currentMaxSpeedKPH += nitroMaxSpeedBonus;

        float speedFactor = Mathf.InverseLerp(60f, currentMaxSpeedKPH, speedKmh);

        float kill = lateralVelocityKill * Mathf.Lerp(0.4f, highSpeedGripAssist, speedFactor);

        localVelocity.x = Mathf.Lerp(
            localVelocity.x,
            localVelocity.x * steeringSpeedLossReducer,
            Time.fixedDeltaTime * kill
        );

        rb.linearVelocity = transform.TransformDirection(localVelocity);
    }

    void ApplyDriftAssist(float steerInput)
    {
        if (rb == null) return;

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;

        bool handbrakePressed = useExternalInput
            ? externalHandbrakeInput
            : Input.GetKey(driftKey);

        bool drifting =
            handbrakePressed &&
            speedKmh >= driftMinSpeedKmh &&
            Mathf.Abs(steerInput) > 0.1f;

        float targetRearGrip = drifting ? driftSideGrip : normalSideGrip;

        SetRearSidewaysStiffness(targetRearGrip);   

        if (drifting)
        {
            Vector3 yaw = Vector3.up * steerInput * driftYawAssist;
            rb.AddTorque(yaw, ForceMode.Acceleration);

            Vector3 sidePush = transform.right * steerInput * 6f;
            rb.AddForce(sidePush, ForceMode.Acceleration);
        }
    }

    void SetRearSidewaysStiffness(float stiffness)
    {
        if (RL == null || RR == null) return;
        if (RL.WheelCollider == null || RR.WheelCollider == null) return;

        WheelFrictionCurve left = RL.WheelCollider.sidewaysFriction;
        WheelFrictionCurve right = RR.WheelCollider.sidewaysFriction;

        left.stiffness = stiffness;
        right.stiffness = stiffness;

        RL.WheelCollider.sidewaysFriction = left;
        RR.WheelCollider.sidewaysFriction = right;
    }

    void UpdateVisualBodyLean(float steerInput)
    {
        if (carVisualRoot == null)
            return;

        float speed01 = Mathf.InverseLerp(20f, maxSpeedKPH, currentSpeedKPH);

        float targetRoll = -steerInput * visualLeanAngle * speed01;
        float targetYaw = steerInput * visualYawAngle * speed01;

        Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, targetRoll);

        float smooth = Mathf.Abs(steerInput) > 0.05f
            ? visualLeanSmooth
            : visualReturnSmooth;

        carVisualRoot.localRotation = Quaternion.Slerp(
            carVisualRoot.localRotation,
            targetRotation,
            Time.deltaTime * smooth
        );
    }

    public float GetSteerInput()
    {
        return steerInput;
    }

    public void ResetRaceVisualState()
    {
        moveInput = Vector2.zero;
        throttleInput = 0f;
        steerInput = 0f;
        steerVisual = 0f;
        preRaceFakeSpeedKPH = 0f;

        currentSpeedKPH = 0;
        currentGear = 1;
        isReversing = false;

        if (carVisualRoot != null)
            carVisualRoot.localRotation = Quaternion.identity;

        if (FL && FL.WheelCollider != null) FL.WheelCollider.steerAngle = 0f;
        if (FR && FR.WheelCollider != null) FR.WheelCollider.steerAngle = 0f;
    }
}