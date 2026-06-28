using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CarControll))]
[RequireComponent(typeof(Rigidbody))]
public class AICarController : MonoBehaviour
{
    [Header("Road Detection")]
    public LayerMask roadMask;
    public float rayHeight = 2.5f;
    public float rayDistance = 6f;
    public float sideRayOffset = 4f;
    public float forwardRayOffset = 7f;

    [Header("Race Route")]
    public bool useRacePath = true;
    public float pathPointReachDistance = 8f;
    public float pathLookAhead = 5;

    private readonly List<Vector3> racePath = new();
    private int currentPathIndex;
    private bool hasRacePath;

    [Header("Lane Offset")]
    public bool useLaneOffset = true;
    public float laneOffset = 0f;
    public float randomLaneOffsetRange = 3.2f;
    public float laneOffsetSmooth = 3f;

    [Header("Lane Variation")]
    public float laneChangeIntervalMin = 2.5f;
    public float laneChangeIntervalMax = 6f;
    public float laneChangeChance = 0.45f;

    private float laneChangeTimer;

    [Header("Wide Vision")]
    public float farLookAheadDistance = 45f;
    public float farSideOffset = 18f;
    public float cutCornerStrength = 0.45f;

    private float currentLaneOffset;
    [Header("Corner Anticipation")]
    public float lookAheadDistance = 18f;
    public float speedLookAheadMultiplier = 0.10f;
    public float turnMemoryTime = 0.45f;
    public float lookAheadSideOffset = 7f;

    [Header("AI Driving")]
    public float targetSpeedKPH = 90f;
    public float cornerSlowdownSpeedKPH = 45f;
    public float steeringSensitivity = 1.35f;

    [Header("Recovery")]
    public float stuckSpeedKPH = 4f;
    public float reverseIfStuckAfter = 2f;
    public float reverseDuration = 1.2f;
    public float recoverySteerStrength = 1f;

    [Header("Offroad Recovery V2")]
    public float offroadSearchRadius = 22f;
    public int offroadSearchDirections = 24;
    public float offroadForwardProbe = 8f;
    public float offroadTurnToRoadStrength = 1.6f;
    public float offroadMaxThrottle = 0.65f;
    public float offroadBrakeWhenWrongWay = -0.25f;

    private Vector3 lastRoadPoint;
    private bool hasLastRoadPoint;

    [Header("AI Avoidance")]
    public bool useAIAvoidance = true;
    public float avoidCheckDistance = 10f;
    public float avoidCheckRadius = 2.2f;
    public float avoidBrakeDistance = 5f;
    public float avoidSteerStrength = 0.75f;
    public float avoidThrottle = 0.15f;

    [Header("Obstacle Avoidance")]
    public LayerMask obstacleMask;
    public float obstacleCheckDistance = 28f;
    public float obstacleCheckRadius = 2.2f;
    public float obstacleSideOffset = 4f;
    public float obstacleAvoidSteer = 1.2f;
    public float obstacleBrakeThrottle = -0.15f;

    [Header("SpeedTrap Nitro")]
    public bool useNitroBeforeSpeedTrap = true;
    public float speedTrapNitroDistance = 65f;
    public LayerMask speedTrapMask;

    [Header("Start")]
    public float startRecoveryDelay = 1.2f;

    private float startTimer = 0f;

    [Header("Wrong Way Prevention")]
    public float wrongWayCheckSpeedKPH = 15f;
    public float wrongWayFixTime = 1.0f;
    public float wrongWayTurnStrength = 1f;

    private float wrongWayTimer;

    [Header("AI Nitro")]
    public bool useNitro = true;
    public float nitroMinSpeedKPH = 70f;
    public float nitroMaxSteer = 0.18f;

    private NitroSystem nitro;

    [Header("AI Nitro Burst")]
    public float nitroBurstDuration = 1.8f;
    public float nitroCooldown = 4f;
    public float nitroOvertakeBonusChance = 0.65f;

    private float nitroBurstTimer;
    private float nitroCooldownTimer;

    [Header("AI Drift / Handbrake")]
    public bool useAIDrift = true;
    public float driftMinSpeedKPH = 120f;
    public float driftSteerThreshold = 0.55f;
    public float driftDuration = 0.35f;
    public float driftCooldown = 1.2f;

    private float driftTimer;
    private float driftCooldownTimer;

    [Header("Offroad Recovery")]
    public float offroadRaySideStep = 3f;
    public int offroadSearchRays = 6;
    public float offroadRecoveryThrottle = 0.45f;
    public float offroadRecoverySteerStrength = 1.2f;
    public float wrongWayDotLimit = -0.25f;
    public float wrongWayFixDuration = 1.2f;

    private float wrongWayFixTimer;
    private Vector3 lastValidForward;

    [Header("Overtaking")]
    public bool useOvertaking = true;
    public LayerMask carMask;
    public float overtakeCheckDistance = 18f;
    public float overtakeCheckRadius = 2.2f;
    public float overtakeLaneOffset = 3.5f;
    public float overtakeDuration = 2.2f;
    public float minSpeedDifferenceForOvertake = 6f;

    private float overtakeTimer;
    private float baseLaneOffset;

    [Header("Debug")]
    public bool drawDebug = true;
    private Vector3 lastOvertakeHitPoint;
    private Vector3 lastOvertakeTargetPoint;
    private bool debugOvertakingActive;

    private CarControll car;
    private Rigidbody rb;

    private float stuckTimer;
    private float reverseTimer;

    private float rememberedSteer;
    private float turnMemoryTimer;

    private Vector3 lastForwardRayOrigin;
    private Vector3 lastLeftRayOrigin;
    private Vector3 lastRightRayOrigin;
    private Vector3 lastLookAheadOrigin;
    private Vector3 lastLookAheadLeft;
    private Vector3 lastLookAheadRight;

    private Vector3 lastFarCenterOrigin;
    private Vector3 lastFarLeftOrigin;
    private Vector3 lastFarRightOrigin;

    void Start()
    {
        car = GetComponent<CarControll>();
        rb = GetComponent<Rigidbody>();

        car.isControlled = true;
        car.useExternalInput = true;
        car.SetExternalInput(0f, 0f);
        startTimer = startRecoveryDelay;

        if (useLaneOffset)
            laneOffset = Random.Range(-randomLaneOffsetRange, randomLaneOffsetRange);

        nitro = GetComponent<NitroSystem>();

        if (nitro != null)
        {
            nitro.useExternalNitroInput = true;
            nitro.SetExternalNitroInput(0f);
        }

        currentLaneOffset = laneOffset;
        baseLaneOffset = laneOffset;
        laneChangeTimer = Random.Range(laneChangeIntervalMin, laneChangeIntervalMax);
        lastValidForward = transform.forward;
    }

    void FixedUpdate()
    {
        if (startTimer > 0f)
            startTimer -= Time.fixedDeltaTime;

        float speedKPH = rb.linearVelocity.magnitude * 3.6f;


        if (useRacePath && hasRacePath)
        {
            DriveRacePathOptimized(speedKPH);
            return;
        }

        HandleOvertaking(speedKPH);
        HandleLaneVariation();

        bool onRoad = RayHitsRoad(transform.position + Vector3.up * rayHeight);

        if (onRoad)
        {
            lastRoadPoint = transform.position;
            hasLastRoadPoint = true;
        }

        float steer;
        float throttle;

        if (!onRoad)
        {
            HandleOffroadRecoveryV2(speedKPH, out steer, out throttle);

            HandleStuckRecovery(speedKPH, ref steer, ref throttle);
            HandleAINitro(speedKPH, steer, throttle);

            car.SetExternalInput(steer, throttle);
            return;
        }

        float directionDot = Vector3.Dot(transform.forward, lastValidForward);

        if (directionDot < wrongWayDotLimit)
            wrongWayFixTimer = wrongWayFixDuration;

        if (wrongWayFixTimer > 0f)
        {
            wrongWayFixTimer -= Time.fixedDeltaTime;

            steer = Mathf.Sign(Vector3.SignedAngle(transform.forward, lastValidForward, Vector3.up));
            throttle = 0.35f;

            HandleAINitro(speedKPH, steer, throttle);

            car.SetExternalInput(Mathf.Clamp(steer, -1f, 1f), throttle);
            return;
        }

        steer = CalculateRoadSteer(speedKPH);
        throttle = CalculateThrottle(speedKPH, steer);

        if (Mathf.Abs(steer) < 0.65f)
            lastValidForward = Vector3.Lerp(lastValidForward, transform.forward, Time.fixedDeltaTime * 2f).normalized;

        HandleWrongWay(speedKPH, ref steer, ref throttle);

        ApplyObstacleAvoidance(ref steer, ref throttle);

        HandleStuckRecovery(speedKPH, ref steer, ref throttle);
        HandleAINitro(speedKPH, steer, throttle);

        car.SetExternalInput(steer, throttle);
    }

    void DriveRacePathOptimized(float speedKPH)
    {
        if (car == null || rb == null || racePath.Count < 2)
            return;

        Vector3 carPos = transform.position;

        AdvanceRacePathIndex(carPos);

        Vector3 target = GetRaceLookAheadPoint(carPos);

        Vector3 localTarget = transform.InverseTransformPoint(target);
        localTarget.y = 0f;

        float steer = Mathf.Clamp(
            localTarget.x / Mathf.Max(1f, localTarget.magnitude),
            -1f,
            1f
        );

        steer *= steeringSensitivity;
        steer = Mathf.Clamp(steer, -1f, 1f);

        float steerAbs = Mathf.Abs(steer);

        float corner01 = Mathf.Clamp01(steerAbs * 1.35f);

        float desiredSpeed = Mathf.Lerp(
            targetSpeedKPH,
            cornerSlowdownSpeedKPH,
            corner01
        );

        float throttle = speedKPH < desiredSpeed ? 1f : 0.05f;

        bool handbrake = false;

        if (useAIDrift)
        {
            if (driftCooldownTimer > 0f)
                driftCooldownTimer -= Time.fixedDeltaTime;

            if (driftTimer > 0f)
            {
                driftTimer -= Time.fixedDeltaTime;
                handbrake = true;
            }
            else
            {
                bool shouldDrift =
                    speedKPH >= driftMinSpeedKPH &&
                    Mathf.Abs(steer) >= driftSteerThreshold &&
                    driftCooldownTimer <= 0f;

                if (shouldDrift)
                {
                    driftTimer = driftDuration;
                    driftCooldownTimer = driftCooldown;
                    handbrake = true;
                }
            }
        }

        // mocniejsze hamowanie przed ostrym zakrêtem
        if (steerAbs > 0.45f && speedKPH > desiredSpeed + 8f)
            throttle = -0.45f;

        if (steerAbs > 0.75f && speedKPH > cornerSlowdownSpeedKPH)
            throttle = -0.65f;

        HandleAIAvoidance(ref steer, ref throttle);

        ApplyObstacleAvoidance(ref steer, ref throttle);

        HandleSpeedTrapNitroAssist(speedKPH, steer, throttle);

        HandleStuckRecovery(speedKPH, ref steer, ref throttle);
        HandleAINitro(speedKPH, steer, throttle);

        car.SetExternalInput(steer, throttle);
        car.SetExternalHandbrake(handbrake);
    }

    void HandleAIAvoidance(ref float steer, ref float throttle)
    {
        if (!useAIAvoidance)
            return;

        Vector3 origin = transform.position + Vector3.up * 1.2f;

        if (!Physics.SphereCast(
                origin,
                avoidCheckRadius,
                transform.forward,
                out RaycastHit hit,
                avoidCheckDistance,
                carMask,
                QueryTriggerInteraction.Ignore))
            return;

        if (hit.transform.IsChildOf(transform))
            return;

        float distance = hit.distance;

        Vector3 localHit = transform.InverseTransformPoint(hit.point);

        float sideSteer = localHit.x >= 0f ? -avoidSteerStrength : avoidSteerStrength;
        steer = Mathf.Clamp(steer + sideSteer, -1f, 1f);

        if (distance <= avoidBrakeDistance)
            throttle = Mathf.Min(throttle, -0.25f);
        else
            throttle = Mathf.Min(throttle, avoidThrottle);
    }

    void HandleSpeedTrapNitroAssist(float speedKPH, float steer, float throttle)
    {
        if (!useNitroBeforeSpeedTrap)
            return;

        if (nitro == null)
            return;

        if (!useNitro)
            return;

        if (Mathf.Abs(steer) > nitroMaxSteer * 2f)
            return;

        if (throttle < 0.5f)
            return;

        Vector3 origin = transform.position + Vector3.up * 1.5f;

        bool seesSpeedTrap = Physics.SphereCast(
            origin,
            4f,
            transform.forward,
            out RaycastHit hit,
            speedTrapNitroDistance,
            speedTrapMask,
            QueryTriggerInteraction.Collide
        );

        if (!seesSpeedTrap)
            return;

        if (nitro.currentNitro <= 5f)
            return;

        nitroBurstTimer = Mathf.Max(nitroBurstTimer, nitroBurstDuration);
        nitroCooldownTimer = Mathf.Min(nitroCooldownTimer, 0.2f);
        nitro.SetExternalNitroInput(1f);
    }

    void AdvanceRacePathIndex(Vector3 carPos)
    {
        if (racePath.Count < 2)
            return;

        int bestForward = FindBestForwardPathIndex(carPos);

        if (bestForward > currentPathIndex)
            currentPathIndex = bestForward;

        while (currentPathIndex < racePath.Count - 2)
        {
            float dist = Vector3.Distance(carPos, racePath[currentPathIndex + 1]);

            if (dist <= pathPointReachDistance)
                currentPathIndex++;
            else
                break;
        }
    }

    Vector3 GetRaceLookAheadPoint(Vector3 carPos)
    {
        if (racePath.Count == 0)
            return transform.position + transform.forward * 10f;

        float remaining = Mathf.Max(1f, pathLookAhead);
        int index = currentPathIndex;

        Vector3 previous = racePath[index];

        while (index < racePath.Count - 1)
        {
            Vector3 next = racePath[index + 1];
            float dist = Vector3.Distance(previous, next);

            if (dist >= remaining)
                return Vector3.Lerp(previous, next, remaining / dist);

            remaining -= dist;
            previous = next;
            index++;
        }

        return racePath[racePath.Count - 1];
    }

    float CalculateRoadSteer(float speedKPH)
    {
        currentLaneOffset = Mathf.Lerp(currentLaneOffset, laneOffset, Time.fixedDeltaTime * laneOffsetSmooth);

        Vector3 laneShift = transform.right * currentLaneOffset;
        Vector3 baseOrigin = transform.position + laneShift + Vector3.up * rayHeight;

        lastForwardRayOrigin = baseOrigin + transform.forward * forwardRayOffset;
        lastLeftRayOrigin = lastForwardRayOrigin - transform.right * sideRayOffset;
        lastRightRayOrigin = lastForwardRayOrigin + transform.right * sideRayOffset;

        bool roadForward = RayHitsRoad(lastForwardRayOrigin);
        bool roadLeft = RayHitsRoad(lastLeftRayOrigin);
        bool roadRight = RayHitsRoad(lastRightRayOrigin);

        float steer = 0f;

        // trzymanie siê œrodka pasa, ¿eby AI nie jecha³o po krawêdzi
        if (!roadLeft && roadRight)
            steer += 0.8f;

        if (!roadRight && roadLeft)
            steer -= 0.8f;

        if (!roadForward)
        {
            if (roadLeft && !roadRight)
                steer = -1f;
            else if (roadRight && !roadLeft)
                steer = 1f;
            else
                steer = rememberedSteer;
        }

        float dynamicLookAhead = lookAheadDistance + speedKPH * speedLookAheadMultiplier;

        lastLookAheadOrigin = baseOrigin + transform.forward * dynamicLookAhead;
        lastLookAheadLeft = lastLookAheadOrigin - transform.right * lookAheadSideOffset;
        lastLookAheadRight = lastLookAheadOrigin + transform.right * lookAheadSideOffset;

        bool roadAhead = RayHitsRoad(lastLookAheadOrigin);
        bool roadAheadLeft = RayHitsRoad(lastLookAheadLeft);
        bool roadAheadRight = RayHitsRoad(lastLookAheadRight);

        if (!roadAhead)
        {
            if (roadAheadLeft && !roadAheadRight)
            {
                rememberedSteer = -1f;
                turnMemoryTimer = turnMemoryTime;
            }
            else if (roadAheadRight && !roadAheadLeft)
            {
                rememberedSteer = 1f;
                turnMemoryTimer = turnMemoryTime;
            }
        }

        if (turnMemoryTimer > 0f)
        {
            turnMemoryTimer -= Time.fixedDeltaTime;
            steer = Mathf.Lerp(steer, rememberedSteer, 0.75f);
        }

        // WIDE VISION / szybsze wejœcie w zakrêt
        Vector3 farCenterOrigin = baseOrigin + transform.forward * farLookAheadDistance;
        Vector3 farLeftOrigin = farCenterOrigin - transform.right * farSideOffset;
        Vector3 farRightOrigin = farCenterOrigin + transform.right * farSideOffset;

        lastFarCenterOrigin = baseOrigin + transform.forward * farLookAheadDistance;
        lastFarLeftOrigin = lastFarCenterOrigin - transform.right * farSideOffset;
        lastFarRightOrigin = lastFarCenterOrigin + transform.right * farSideOffset;

        bool farCenter = RayHitsRoad(lastFarCenterOrigin);
        bool farLeft = RayHitsRoad(lastFarLeftOrigin);
        bool farRight = RayHitsRoad(lastFarRightOrigin);

        float wideSteer = 0f;

        if (!farCenter)
        {
            if (farLeft && !farRight)
                wideSteer = -cutCornerStrength;
            else if (farRight && !farLeft)
                wideSteer = cutCornerStrength;
        }
        else
        {
            if (farLeft && !farRight)
                wideSteer = -cutCornerStrength * 0.5f;
            else if (farRight && !farLeft)
                wideSteer = cutCornerStrength * 0.5f;
        }

        steer += wideSteer;

        steer *= steeringSensitivity;
        return Mathf.Clamp(steer, -1f, 1f);
    }

    float CalculateThrottle(float speedKPH, float steer)
    {
        float desiredSpeed = Mathf.Abs(steer) > 0.45f
            ? cornerSlowdownSpeedKPH
            : targetSpeedKPH;

        float throttle = speedKPH < desiredSpeed ? 1f : 0.15f;

        if (turnMemoryTimer > 0f)
            throttle = Mathf.Min(throttle, 0.45f);

        return throttle;
    }

    void HandleStuckRecovery(float speedKPH, ref float steer, ref float throttle)
    {
        if (startTimer > 0f)
        {
            stuckTimer = 0f;
            reverseTimer = 0f;
            return;
        }

        if (reverseTimer > 0f)
        {
            reverseTimer -= Time.fixedDeltaTime;

            throttle = -0.65f;
            steer = rememberedSteer != 0f
                ? -rememberedSteer * recoverySteerStrength
                : recoverySteerStrength;

            return;
        }

        if (speedKPH < stuckSpeedKPH && throttle > 0.2f)
            stuckTimer += Time.fixedDeltaTime;
        else
            stuckTimer = 0f;

        if (stuckTimer >= reverseIfStuckAfter)
        {
            reverseTimer = reverseDuration;
            stuckTimer = 0f;
        }
    }

    void HandleOffroadRecoveryV2(float speedKPH, out float steer, out float throttle)
    {
        Vector3 roadPoint;

        if (TryFindNearestRoadPoint(out roadPoint))
        {
            lastRoadPoint = roadPoint;
            hasLastRoadPoint = true;
        }
        else if (hasLastRoadPoint)
        {
            roadPoint = lastRoadPoint;
        }
        else
        {
            steer = rememberedSteer != 0f ? rememberedSteer : 0.6f;
            throttle = 0.35f;
            return;
        }

        Vector3 toRoad = roadPoint - transform.position;
        toRoad.y = 0f;

        if (toRoad.sqrMagnitude < 0.1f)
        {
            steer = 0f;
            throttle = offroadMaxThrottle;
            return;
        }

        Vector3 localRoad = transform.InverseTransformDirection(toRoad.normalized);

        steer = Mathf.Clamp(localRoad.x * offroadTurnToRoadStrength, -1f, 1f);

        float angle = Vector3.Angle(transform.forward, toRoad.normalized);

        if (angle > 120f && speedKPH > 8f)
            throttle = offroadBrakeWhenWrongWay;
        else
            throttle = offroadMaxThrottle;

        rememberedSteer = steer;
    }

    void HandleWrongWay(float speedKPH, ref float steer, ref float throttle)
    {
        if (speedKPH < wrongWayCheckSpeedKPH)
        {
            wrongWayTimer = 0f;
            return;
        }

        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);

        bool drivingBackwards = localVel.z < -1f;

        if (drivingBackwards)
            wrongWayTimer += Time.fixedDeltaTime;
        else
            wrongWayTimer = 0f;

        if (wrongWayTimer >= wrongWayFixTime)
        {
            throttle = 0.8f;

            if (rememberedSteer != 0f)
                steer = rememberedSteer * wrongWayTurnStrength;
            else
                steer = steer >= 0f ? wrongWayTurnStrength : -wrongWayTurnStrength;
        }
    }

    void HandleAINitro(float speedKPH, float steer, float throttle)
    {
        if (nitro == null)
            return;

        if (!useNitro)
        {
            nitro.SetExternalNitroInput(0f);
            return;
        }

        if (nitroCooldownTimer > 0f)
            nitroCooldownTimer -= Time.fixedDeltaTime;

        if (nitroBurstTimer > 0f)
        {
            nitroBurstTimer -= Time.fixedDeltaTime;

            bool canContinue =
                throttle > 0.5f &&
                speedKPH >= nitroMinSpeedKPH &&
                Mathf.Abs(steer) <= nitroMaxSteer * 2.5f &&
                nitro.currentNitro > 1f;

            nitro.SetExternalNitroInput(canContinue ? 1f : 0f);

            if (!canContinue)
                nitroBurstTimer = 0f;

            return;
        }

        bool goodStraight =
            throttle > 0.8f &&
            speedKPH >= nitroMinSpeedKPH &&
            Mathf.Abs(steer) <= nitroMaxSteer &&
            turnMemoryTimer <= 0f &&
            nitro.currentNitro > 20f;

        bool wantsOvertakeNitro =
            overtakeTimer > 0f &&
            Random.value < nitroOvertakeBonusChance;

        if (goodStraight && nitroCooldownTimer <= 0f)
        {
            nitroBurstTimer = wantsOvertakeNitro
                ? nitroBurstDuration * 1.35f
                : nitroBurstDuration;

            nitroCooldownTimer = nitroCooldown;
            nitro.SetExternalNitroInput(1f);
        }
        else
        {
            nitro.SetExternalNitroInput(0f);
        }
    }

    void HandleLaneVariation()
    {
        if (!useLaneOffset)
            return;

        if (overtakeTimer > 0f)
            return;

        laneChangeTimer -= Time.fixedDeltaTime;

        if (laneChangeTimer > 0f)
            return;

        laneChangeTimer = Random.Range(laneChangeIntervalMin, laneChangeIntervalMax);

        if (Random.value > laneChangeChance)
            return;

        float newOffset = Random.Range(-randomLaneOffsetRange, randomLaneOffsetRange);

        if (Mathf.Abs(newOffset - baseLaneOffset) < 1.2f)
            newOffset += Mathf.Sign(newOffset == 0f ? 1f : newOffset) * 1.5f;

        laneOffset = Mathf.Clamp(newOffset, -randomLaneOffsetRange, randomLaneOffsetRange);
        baseLaneOffset = laneOffset;
    }

    void HandleOvertaking(float speedKPH)
    {
        if (!useOvertaking)
            return;

        if (Mathf.Abs(rememberedSteer) > 0.4f || turnMemoryTimer > 0f)
            return;

        if (overtakeTimer > 0f)
        {
            overtakeTimer -= Time.fixedDeltaTime;

            lastOvertakeTargetPoint =
                transform.position
                + transform.forward * overtakeCheckDistance
                + transform.right * laneOffset;

            if (overtakeTimer <= 0f)
            {
                laneOffset = baseLaneOffset;
                debugOvertakingActive = false;
            }

            return;
        }

        Vector3 origin = transform.position + Vector3.up * 1.2f;

        if (!Physics.SphereCast(origin, overtakeCheckRadius, transform.forward, out RaycastHit hit,
                overtakeCheckDistance, carMask, QueryTriggerInteraction.Ignore))
        {
            debugOvertakingActive = false;
            return;
        }

        if (hit.transform.IsChildOf(transform))
            return;

        Rigidbody otherRb = hit.collider.GetComponentInParent<Rigidbody>();
        if (otherRb == null)
            return;

        float otherSpeedKPH = otherRb.linearVelocity.magnitude * 3.6f;

        if (speedKPH < otherSpeedKPH + minSpeedDifferenceForOvertake)
            return;

        bool leftRoad = RayHitsRoad(origin - transform.right * overtakeLaneOffset + transform.forward * 10f);
        bool rightRoad = RayHitsRoad(origin + transform.right * overtakeLaneOffset + transform.forward * 10f);

        if (leftRoad && !rightRoad)
            laneOffset = -Mathf.Abs(overtakeLaneOffset);
        else if (rightRoad && !leftRoad)
            laneOffset = Mathf.Abs(overtakeLaneOffset);
        else if (leftRoad && rightRoad)
            laneOffset = Random.value > 0.5f ? overtakeLaneOffset : -overtakeLaneOffset;
        else
            return;

        lastOvertakeHitPoint = hit.point;
        lastOvertakeTargetPoint =
            transform.position
            + transform.forward * overtakeCheckDistance
            + transform.right * laneOffset;

        debugOvertakingActive = true;
        overtakeTimer = overtakeDuration;
    }

    bool TryFindNearestRoadPoint(out Vector3 roadPoint)
    {
        roadPoint = Vector3.zero;

        Vector3 center = transform.position + Vector3.up * rayHeight;

        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < offroadSearchDirections; i++)
        {
            float angle = (360f / offroadSearchDirections) * i;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            for (float d = offroadForwardProbe; d <= offroadSearchRadius; d += offroadForwardProbe)
            {
                Vector3 origin = center + dir * d;

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, roadMask, QueryTriggerInteraction.Ignore))
                {
                    float sqr = (hit.point - transform.position).sqrMagnitude;

                    if (sqr < bestDistance)
                    {
                        bestDistance = sqr;
                        roadPoint = hit.point;
                        found = true;
                    }
                }
            }
        }

        return found;
    }

    void ApplyObstacleAvoidance(ref float steer, ref float throttle)
    {
        if (obstacleMask.value == 0)
            return;

        Vector3 baseOrigin = transform.position + Vector3.up * 1.2f + transform.forward * 3f;

        bool centerHit = Physics.SphereCast(
            baseOrigin,
            obstacleCheckRadius,
            transform.forward,
            out RaycastHit center,
            obstacleCheckDistance,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        bool leftHit = Physics.SphereCast(
            baseOrigin - transform.right * obstacleSideOffset,
            obstacleCheckRadius,
            transform.forward,
            out RaycastHit left,
            obstacleCheckDistance,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        bool rightHit = Physics.SphereCast(
            baseOrigin + transform.right * obstacleSideOffset,
            obstacleCheckRadius,
            transform.forward,
            out RaycastHit right,
            obstacleCheckDistance,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );

        bool validCenter = centerHit && !IsIgnoredObstacleHit(center);
        bool validLeft = leftHit && !IsIgnoredObstacleHit(left);
        bool validRight = rightHit && !IsIgnoredObstacleHit(right);

        if (!validCenter && !validLeft && !validRight)
            return;

        float avoid = 0f;
        float nearest = obstacleCheckDistance;

        if (validCenter)
        {
            Vector3 local = transform.InverseTransformPoint(center.point);

            avoid += local.x >= 0f
                ? -obstacleAvoidSteer
                : obstacleAvoidSteer;

            nearest = Mathf.Min(nearest, center.distance);
        }

        if (validLeft)
        {
            avoid += obstacleAvoidSteer;
            nearest = Mathf.Min(nearest, left.distance);
        }

        if (validRight)
        {
            avoid -= obstacleAvoidSteer;
            nearest = Mathf.Min(nearest, right.distance);
        }

        steer = Mathf.Clamp(steer + avoid, -1f, 1f);

        float danger01 = 1f - Mathf.Clamp01(nearest / obstacleCheckDistance);

        float brake = Mathf.Lerp(
            0.1f,
            obstacleBrakeThrottle,
            danger01
        );

        throttle = Mathf.Min(throttle, brake);
    }

    bool IsIgnoredObstacleHit(RaycastHit hit)
    {
        if (hit.collider == null)
            return true;

        if (hit.collider.isTrigger)
            return true;

        if (hit.transform.IsChildOf(transform))
            return true;

        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("RaceVisual"))
            return true;

        return false;
    }

    public void SetRacePath(List<Vector3> points)
    {
        racePath.Clear();

        if (points != null)
            racePath.AddRange(points);

        hasRacePath = racePath.Count > 1;

        if (!hasRacePath)
        {
            currentPathIndex = 0;
            Debug.LogWarning($"AI {name}: brak punktów RacePath.");
            return;
        }

        currentPathIndex = FindBestForwardPathIndex(transform.position);

        Debug.Log($"AI {name}: RacePath loaded. Points={racePath.Count}, StartIndex={currentPathIndex}");
    }

    int FindBestForwardPathIndex(Vector3 pos)
    {
        int bestIndex = 0;
        float bestScore = float.MaxValue;

        for (int i = 0; i < racePath.Count; i++)
        {
            Vector3 toPoint = racePath[i] - pos;
            toPoint.y = 0f;

            float dist = toPoint.magnitude;
            if (dist < 0.01f)
                continue;

            float dot = Vector3.Dot(transform.forward, toPoint.normalized);

            // ignoruj punkty mocno za autem
            if (dot < -0.25f)
                continue;

            float score = dist - dot * 12f;

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    int FindNearestPathIndex(Vector3 pos)
    {
        int bestIndex = 0;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < racePath.Count; i++)
        {
            float sqr = (racePath[i] - pos).sqrMagnitude;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    bool RayHitsRoad(Vector3 origin)
    {
        return Physics.Raycast(
            origin,
            Vector3.down,
            rayDistance,
            roadMask,
            QueryTriggerInteraction.Ignore
        );
    }

    void DrawRay(Vector3 origin, Color color)
    {
        if (origin == Vector3.zero)
            return;

        Gizmos.color = color;
        Gizmos.DrawLine(origin, origin + Vector3.down * rayDistance);
        Gizmos.DrawSphere(origin + Vector3.down * rayDistance, 0.25f);
    }

    void OnDrawGizmos()
    {
        if (!drawDebug)
            return;

        DrawRay(lastForwardRayOrigin, Color.green);
        DrawRay(lastLeftRayOrigin, Color.green);
        DrawRay(lastRightRayOrigin, Color.green);

        DrawRay(lastLookAheadOrigin, Color.red);
        DrawRay(lastLookAheadLeft, Color.red);
        DrawRay(lastLookAheadRight, Color.red);

        DrawRay(lastFarCenterOrigin, Color.yellow);
        DrawRay(lastFarLeftOrigin, Color.yellow);
        DrawRay(lastFarRightOrigin, Color.yellow);

        if (debugOvertakingActive)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(lastOvertakeHitPoint, 0.8f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(lastOvertakeTargetPoint, 0.9f);
            Gizmos.DrawLine(transform.position, lastOvertakeTargetPoint);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(
                transform.position + Vector3.up * 1.2f + transform.forward * overtakeCheckDistance,
                overtakeCheckRadius
            );
        }
    }
}