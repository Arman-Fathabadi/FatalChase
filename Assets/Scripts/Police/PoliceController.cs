using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Audio;

[RequireComponent(typeof(Rigidbody))]
public class PoliceController : MonoBehaviour
{
    [Header("Target")]
    public Transform playerTarget;

    [Header("Movement Settings")]
    public float chaseSpeed = 7.5f;
    public float acceleration = 8f;
    public float turningPenalty = 0.5f;
    public float rotationSpeed = 25f;

    [Header("Path Tracking")]
    public float trailPointSpacing = 4f;
    public int maxTrailPoints = 90;
    public int trailLookAheadPoints = 4;
    public float playerPredictionSeconds = 0.55f;
    public float closeChaseDistance = 11f;
    public float closeChaseOffset = 4.5f;
    public float trailReachDistance = 6f;

    [Header("Recovery")]
    public float catchUpDistance = 35f;
    public float recoveryDistance = 90f;
    public float maxChaseSpeed = 72f;
    public float playerSpeedMatchFactor = 1.08f;
    public float catchUpSpeedMultiplier = 1.35f;
    public float recoveryAccelerationMultiplier = 1.35f;
    public float stuckSpeedThresholdKmh = 6f;
    public float stuckTimeout = 1.75f;
    public float reverseRecoverDuration = 0.9f;
    public float reverseTorque = 1500f;
    public float interceptRecoveryDistance = 120f;
    public float interceptRecoveryDelay = 1.6f;
    public float interceptRecoveryCooldown = 3.5f;
    public float interceptForwardDistance = 48f;
    public float interceptSideOffset = 12f;
    [Range(0.5f, 1.2f)] public float interceptSpeedFactor = 0.88f;

    [Header("Stability Assist")]
    [Range(0.2f, 1f)] public float highSpeedSteerReduction = 0.55f;
    public float lateralVelocityDamping = 2.2f;
    public float yawStability = 0.9f;

    [Header("Spawn")]
    public bool autoPositionAtStart = true;
    public float openingSpawnForwardDistance = 34f;
    public float openingSpawnSideOffset = 12f;
    public float openingSpawnMinDistance = 22f;
    public int recoveryTrailLookbackPoints = 5;
    public float openingSpawnEntrySpeedKmh = 18f;
    public float spawnRaycastHeight = 45f;
    public float spawnMaxRayDistance = 160f;
    [Range(0.5f, 1f)] public float spawnSurfaceNormalMinY = 0.7f;
    public float spawnHeightOffset = 0.12f;
    public float spawnSettleBrakeTime = 0.35f;
    public float spawnSettleDownForce = 8f;
    public LayerMask spawnSurfaceMask = Physics.DefaultRaycastLayers;
    public Vector3 spawnClearancePadding = new Vector3(0.35f, 0.08f, 0.45f);
    public float spawnForwardClearanceDistance = 11f;
    public float spawnForwardClearanceRadius = 1.1f;
    public float startupRolloutDuration = 1.1f;
    public float startupRolloutDistance = 12f;
    public float startupRolloutMinSpeedKmh = 24f;
    public float postSpawnGroundValidationTime = 1.2f;
    public float postSpawnGroundTolerance = 0.08f;

    [Header("High-Speed Recovery")]
    public float highSpeedPlayerThresholdKmh = 100f;
    [Range(0.25f, 1f)] public float highSpeedRecoveryDelayMultiplier = 0.55f;
    [Range(0.25f, 1f)] public float highSpeedRecoveryCooldownMultiplier = 0.65f;
    [Range(1f, 2f)] public float highSpeedCatchUpSpeedMultiplier = 1.18f;
    [Range(1f, 2f)] public float highSpeedAccelerationMultiplier = 1.2f;
    public float highSpeedRecoveryDistance = 48f;
    public float highSpeedInterceptDistance = 78f;

    [Header("Vehicle Setup")]
    public WheelCollider FL, FR, RL, RR;
    public Transform FLMesh, FRMesh;
    public float maxSteerAngle = 24f;
    public float maxMotorTorque = 2400f;
    public float maxBrakeTorque = 2600f;
    public float wheelRadius = 0.4f;
    public float wheelMass = 20f;
    public float suspensionDistance = 0.09f;
    public float antiRoll = 4000f;
    public float downforce = 70f;

    [Header("Tire Effects")]
    public AudioClip tireSquealClip;
    public ParticleSystem[] smokeParticles;
    public TrailRenderer[] skidTrails;
    public float sidewaysSlipThreshold = 0.75f;
    public float forwardSlipThreshold = 0.9f;
    public float squealMinSpeedKmh = 40f;
    public float skidMarkMinSpeedKmh = 55f;
    public float smokeMinSpeedKmh = 85f;
    public float slipEffectThreshold = 0.3f;
    public float skidEffectThreshold = 0.45f;
    public float smokeEffectThreshold = 0.58f;

    [Header("Siren Audio & Lights")]
    public AudioClip engineClip;
    public AudioClip sirenClip;
    public GameObject sirenLightsContainer;
    public AudioMixerGroup sfxMixerGroup;
    [Range(0f, 1f)] public float sirenVolume = 0.85f;
    public float sirenMinDistance = 5f;
    public float sirenMaxDistance = 100f;
    public float sirenFadeSpeed = 4.5f;
    public bool loopSiren = true;
    [Range(0f, 1f)] public float sirenDopplerLevel = 0f;
    public float sirenScheduleLeadSeconds = 0.12f;
    public float sirenSilenceThreshold = 0.0025f;
    public float sirenLoopSafetyPaddingSeconds = 0f;
    [Range(0, 512)] public int sirenEdgeFadeSamples = 96;

    [Header("UI Finale")]
    public GameObject bustedUI;

    [Header("Damage")]
    public bool infiniteHealth = true;
    public float maxHealth = 120f;
    public float currentHealth = 120f;
    public float crashDamageThreshold = 10f;
    public float collisionDamageMultiplier = 0.32f;
    public float environmentDamageMultiplier = 0.22f;
    public float damageRepeatCooldown = 0.25f;

    [Header("Debug")]
    public bool isChasing = true;

    private Rigidbody rb;
    private readonly List<Vector3> trail = new List<Vector3>();
    private float trailTimer = 0f;
    private float currentSpeed = 0f;
    private float stunTimer = 0f;
    private float stuckTimer = 0f;
    private float reverseRecoveryTimer = 0f;
    private float reverseSteerDirection = 1f;
    private float farSeparationTimer = 0f;
    private float lastInterceptRecoveryTime = -10f;
    private AudioSource engineSource;
    private AudioSource sirenSource;
    private AudioSource tireSquealSource;
    private AudioClip preparedSirenLoopClip;
    private int sirenLoopStartSample = 0;
    private int sirenLoopSampleCount = 0;
    private float maxSlipIntensity = 0f;
    private float lastDamageTime = -10f;
    private bool isWrecked = false;
    private VehicleDamageVisuals damageVisuals;
    private float lastDamageVisualHealth = -1f;
    private float spawnGroundOffset = 1f;
    private float spawnSettleTimer = 0f;
    private Vector3 spawnBoundsCenterLocal = new Vector3(0f, 0.9f, 0f);
    private Vector3 spawnBoundsHalfExtents = new Vector3(0.95f, 0.75f, 2.1f);
    private static readonly RaycastHit[] SpawnGroundHits = new RaycastHit[16];
    private static readonly Collider[] SpawnOverlapHits = new Collider[24];

    void Start()
    {
        isChasing = true;
        Time.timeScale = 1f;

        if (maxHealth <= 0f)
        {
            maxHealth = 120f;
        }

        currentHealth = infiniteHealth ? maxHealth : (currentHealth <= 0f ? maxHealth : Mathf.Clamp(currentHealth, 0f, maxHealth));
        ApplyRuntimeTuning();
        trail.Capacity = Mathf.Max(trail.Capacity, Mathf.Max(24, maxTrailPoints * 2));

        rb = GetComponent<Rigidbody>();
        ConfigureRigidbody();
        ConfigureBodyColliders();
        DisableConflictingControllers();
        ResolvePlayerTarget();
        CacheSpawnBounds();
        CacheSpawnGroundOffset();
        PositionAtOpeningSpawn();
        SeedTrailWithPlayerPosition();
        SetupWheelSystem();
        SetupAudio();
        InitializeDamageVisuals();

        SetSirenState(true);
        if (bustedUI != null) bustedUI.SetActive(false);
    }

    void OnDestroy()
    {
        if (preparedSirenLoopClip != null && preparedSirenLoopClip != sirenClip)
        {
            Destroy(preparedSirenLoopClip);
            preparedSirenLoopClip = null;
        }
    }

    void ApplyRuntimeTuning()
    {
        chaseSpeed = Mathf.Max(chaseSpeed, 30f);
        acceleration = Mathf.Max(acceleration, 13f);
        rotationSpeed = Mathf.Max(rotationSpeed, 35f);
        catchUpDistance = Mathf.Min(catchUpDistance, 24f);
        recoveryDistance = Mathf.Min(recoveryDistance, 64f);
        maxChaseSpeed = Mathf.Max(maxChaseSpeed, 95f);
        playerSpeedMatchFactor = Mathf.Max(playerSpeedMatchFactor, 1.22f);
        catchUpSpeedMultiplier = Mathf.Max(catchUpSpeedMultiplier, 1.8f);
        recoveryAccelerationMultiplier = Mathf.Max(recoveryAccelerationMultiplier, 1.75f);
        stuckTimeout = Mathf.Min(stuckTimeout, 0.85f);
        maxMotorTorque = Mathf.Max(maxMotorTorque, 3200f);
        downforce = Mathf.Max(downforce, 100f);
        lateralVelocityDamping = Mathf.Max(lateralVelocityDamping, 3f);
        yawStability = Mathf.Max(yawStability, 1.15f);
        openingSpawnForwardDistance = Mathf.Min(openingSpawnForwardDistance, 22f);
        openingSpawnSideOffset = Mathf.Min(openingSpawnSideOffset, 9f);
        openingSpawnMinDistance = Mathf.Min(openingSpawnMinDistance, 15f);
        recoveryTrailLookbackPoints = Mathf.Clamp(recoveryTrailLookbackPoints, 2, 12);
        openingSpawnEntrySpeedKmh = Mathf.Clamp(openingSpawnEntrySpeedKmh, 0f, 50f);
        interceptForwardDistance = Mathf.Min(interceptForwardDistance, 32f);
        interceptSideOffset = Mathf.Min(interceptSideOffset, 9f);
        interceptRecoveryDistance = Mathf.Clamp(interceptRecoveryDistance, recoveryDistance + 8f, 96f);
        interceptRecoveryDelay = Mathf.Clamp(interceptRecoveryDelay, 0.55f, 1.1f);
        interceptRecoveryCooldown = Mathf.Clamp(interceptRecoveryCooldown, 1.2f, 2f);
        spawnRaycastHeight = Mathf.Max(spawnRaycastHeight, 18f);
        spawnMaxRayDistance = Mathf.Max(spawnMaxRayDistance, 40f);
        spawnSurfaceNormalMinY = Mathf.Clamp(spawnSurfaceNormalMinY, 0.5f, 1f);
        spawnHeightOffset = Mathf.Max(0.02f, spawnHeightOffset);
        spawnSettleBrakeTime = Mathf.Clamp(spawnSettleBrakeTime, 0f, 1.5f);
        spawnSettleDownForce = Mathf.Max(0f, spawnSettleDownForce);
        if (spawnSurfaceMask.value == 0)
        {
            spawnSurfaceMask = Physics.DefaultRaycastLayers;
        }
        spawnClearancePadding.x = Mathf.Max(0.05f, spawnClearancePadding.x);
        spawnClearancePadding.y = Mathf.Max(0.02f, spawnClearancePadding.y);
        spawnClearancePadding.z = Mathf.Max(0.08f, spawnClearancePadding.z);
        spawnForwardClearanceDistance = Mathf.Clamp(spawnForwardClearanceDistance, 2f, 24f);
        spawnForwardClearanceRadius = Mathf.Clamp(spawnForwardClearanceRadius, 0.3f, 3f);
        startupRolloutDuration = Mathf.Clamp(startupRolloutDuration, 0f, 3f);
        startupRolloutDistance = Mathf.Clamp(startupRolloutDistance, 3f, 24f);
        startupRolloutMinSpeedKmh = Mathf.Clamp(startupRolloutMinSpeedKmh, 0f, 60f);
        postSpawnGroundValidationTime = Mathf.Clamp(postSpawnGroundValidationTime, 0f, 3f);
        postSpawnGroundTolerance = Mathf.Clamp(postSpawnGroundTolerance, 0.01f, 0.4f);
        highSpeedPlayerThresholdKmh = Mathf.Max(40f, highSpeedPlayerThresholdKmh);
        highSpeedRecoveryDelayMultiplier = Mathf.Clamp(highSpeedRecoveryDelayMultiplier, 0.25f, 1f);
        highSpeedRecoveryCooldownMultiplier = Mathf.Clamp(highSpeedRecoveryCooldownMultiplier, 0.25f, 1f);
        highSpeedCatchUpSpeedMultiplier = Mathf.Clamp(highSpeedCatchUpSpeedMultiplier, 1f, 2f);
        highSpeedAccelerationMultiplier = Mathf.Clamp(highSpeedAccelerationMultiplier, 1f, 2f);
        highSpeedRecoveryDistance = Mathf.Clamp(highSpeedRecoveryDistance, catchUpDistance + 10f, recoveryDistance);
        highSpeedInterceptDistance = Mathf.Clamp(highSpeedInterceptDistance, highSpeedRecoveryDistance + 10f, interceptRecoveryDistance);
    }

    void ConfigureRigidbody()
    {
        if (rb == null) return;

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = Mathf.Clamp(rb.mass, 1900f, 2300f);
        rb.linearDamping = Mathf.Clamp(rb.linearDamping, 0.02f, 0.08f);
        rb.angularDamping = Mathf.Max(rb.angularDamping, 6f);
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void ConfigureBodyColliders()
    {
        PhysicsMaterial antiSticky = RuntimeSharedResources.GetPoliceAntiStickyMaterial();

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            if (!col.isTrigger && !(col is WheelCollider))
            {
                col.sharedMaterial = antiSticky;
            }
        }
    }

    void DisableConflictingControllers()
    {
        CarController carCtrl = GetComponent<CarController>();
        if (carCtrl != null) carCtrl.enabled = false;

        SuperminiCarController smCtrl = GetComponent<SuperminiCarController>();
        if (smCtrl != null) smCtrl.enabled = false;
    }

    void ResolvePlayerTarget()
    {
        if (playerTarget != null) return;

        GameObject playerObj = GameObject.Find("SuperminiCar");
        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
            return;
        }

        SuperminiCarController ctrl = FindAnyObjectByType<SuperminiCarController>();
        if (ctrl != null)
        {
            playerTarget = ctrl.transform;
        }
    }

    void CacheSpawnGroundOffset()
    {
        float bodyBottomOffset = 0.2f;
        float localBodyBottom = spawnBoundsCenterLocal.y - spawnBoundsHalfExtents.y;
        if (localBodyBottom < 0f)
        {
            bodyBottomOffset = -localBodyBottom;
        }

        FLMesh = FLMesh != null ? FLMesh : FindChildTransform("P_WheelFL");
        FRMesh = FRMesh != null ? FRMesh : FindChildTransform("P_WheelFR");
        Transform rearWheelGroup = FindChildTransform("P_Wheels_B");
        Vector3 frontLeftLocal = GetRootLocalPosition(FLMesh, new Vector3(-0.7f, 0f, 1.47f));
        Vector3 frontRightLocal = GetRootLocalPosition(FRMesh, new Vector3(0.7f, 0f, 1.47f));
        Vector3 rearLocal = GetRootLocalPosition(rearWheelGroup, new Vector3(0f, 0f, -1.47f));
        float lowestWheelCenterY = Mathf.Min(frontLeftLocal.y, frontRightLocal.y, rearLocal.y);
        float wheelBottomOffset = Mathf.Max(0.2f, wheelRadius - lowestWheelCenterY);

        spawnGroundOffset = Mathf.Max(bodyBottomOffset, wheelBottomOffset);
    }

    void CacheSpawnBounds()
    {
        bool foundBounds = false;
        Bounds localBounds = default;

        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (col == null || col.isTrigger || col is WheelCollider)
            {
                continue;
            }

            Bounds colliderBounds = col.bounds;
            Vector3 extents = colliderBounds.extents;
            Vector3 center = colliderBounds.center;
            Vector3[] corners =
            {
                center + new Vector3(extents.x, extents.y, extents.z),
                center + new Vector3(extents.x, extents.y, -extents.z),
                center + new Vector3(extents.x, -extents.y, extents.z),
                center + new Vector3(extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x, extents.y, extents.z),
                center + new Vector3(-extents.x, extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y, extents.z),
                center + new Vector3(-extents.x, -extents.y, -extents.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 localCorner = transform.InverseTransformPoint(corner);
                if (!foundBounds)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    foundBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        if (!foundBounds)
        {
            return;
        }

        spawnBoundsCenterLocal = localBounds.center;
        spawnBoundsHalfExtents = Vector3.Max(localBounds.extents, new Vector3(0.75f, 0.55f, 1.6f));
    }

    void PositionAtOpeningSpawn()
    {
        if (!autoPositionAtStart || playerTarget == null)
        {
            return;
        }

        Vector3 playerVelocity = GetPlayerVelocity();
        Vector3 playerForward = GetPlayerTravelDirection(playerVelocity);
        Vector3 playerRight = Vector3.Cross(Vector3.up, playerForward).normalized;
        float preferredSide = Vector3.Dot(transform.position - playerTarget.position, playerRight) >= 0f ? 1f : -1f;
        Vector3 spawnAnchor = playerTarget.position - playerForward * openingSpawnForwardDistance;

        float lateralOffset = openingSpawnSideOffset * 0.45f;
        Vector3[] candidates =
        {
            spawnAnchor,
            spawnAnchor + playerRight * lateralOffset * preferredSide,
            spawnAnchor - playerRight * lateralOffset * preferredSide,
            playerTarget.position - playerForward * (openingSpawnForwardDistance * 1.15f)
        };

        Vector3 bestPosition = transform.position;
        float bestScore = float.MaxValue;
        bool foundCandidate = false;

        foreach (Vector3 candidate in candidates)
        {
            Vector3 desiredForward = playerTarget.position - candidate;
            desiredForward.y = 0f;
            if (!TryGetSpawnPose(candidate, desiredForward, out Vector3 groundedCandidate, out Quaternion candidateRotation, out float clearanceScore))
            {
                continue;
            }

            if (Vector3.Distance(groundedCandidate, playerTarget.position) < openingSpawnMinDistance)
            {
                continue;
            }

            float score = (groundedCandidate - transform.position).sqrMagnitude - clearanceScore * 6f;
            if (score < bestScore)
            {
                bestScore = score;
                bestPosition = groundedCandidate;
                foundCandidate = true;
            }
        }

        if (!foundCandidate)
        {
            return;
        }

        Vector3 lookDirection = playerTarget.position - bestPosition;
        if (!TryGetSpawnPose(bestPosition, lookDirection, out bestPosition, out Quaternion spawnRotation, out _))
        {
            return;
        }

        float entrySpeed = openingSpawnEntrySpeedKmh / 3.6f;
        ApplySpawnPose(bestPosition, spawnRotation, entrySpeed);
    }

    void SetupWheelSystem()
    {
        FLMesh = FLMesh != null ? FLMesh : FindChildTransform("P_WheelFL");
        FRMesh = FRMesh != null ? FRMesh : FindChildTransform("P_WheelFR");

        if (FL == null || FR == null || RL == null || RR == null)
        {
            CreateMissingWheelColliders();
        }

        ApplyGripPreset(FL, false);
        ApplyGripPreset(FR, false);
        ApplyGripPreset(RL, true);
        ApplyGripPreset(RR, true);

        CreatePoliceTireEffects();
    }

    void CreateMissingWheelColliders()
    {
        Vector3 frontLeftLocal = GetRootLocalPosition(FLMesh, new Vector3(-0.75f, 0.36f, 1.58f));
        Vector3 frontRightLocal = GetRootLocalPosition(FRMesh, new Vector3(0.75f, 0.36f, 1.58f));

        Transform rearMesh = FindChildTransform("P_Wheels_B");
        Vector3 rearLocal = GetRootLocalPosition(rearMesh, new Vector3(0f, frontLeftLocal.y, -1.36f));
        float rearZ = rearLocal.z;
        float rearX = Mathf.Max(Mathf.Abs(frontLeftLocal.x), Mathf.Abs(frontRightLocal.x), 0.72f);
        float wheelY = rearLocal.y;

        FL = FL != null ? FL : CreateWheelCollider("Police_WC_FL", frontLeftLocal, false);
        FR = FR != null ? FR : CreateWheelCollider("Police_WC_FR", frontRightLocal, false);
        RL = RL != null ? RL : CreateWheelCollider("Police_WC_RL", new Vector3(-rearX, wheelY, rearZ), true);
        RR = RR != null ? RR : CreateWheelCollider("Police_WC_RR", new Vector3(rearX, wheelY, rearZ), true);
    }

    WheelCollider CreateWheelCollider(string name, Vector3 localPosition, bool rear)
    {
        GameObject wheelGo = new GameObject(name);
        wheelGo.transform.SetParent(transform, false);
        wheelGo.transform.localPosition = localPosition;

        WheelCollider collider = wheelGo.AddComponent<WheelCollider>();
        collider.radius = wheelRadius;
        collider.mass = wheelMass;
        collider.suspensionDistance = suspensionDistance;

        JointSpring spring = collider.suspensionSpring;
        spring.spring = rear ? 32000f : 35000f;
        spring.damper = 4200f;
        spring.targetPosition = 0.5f;
        collider.suspensionSpring = spring;
        collider.wheelDampingRate = 0.25f;

        return collider;
    }

    Vector3 GetRootLocalPosition(Transform target, Vector3 fallback)
    {
        return target != null ? transform.InverseTransformPoint(target.position) : fallback;
    }

    static void ApplyGripPreset(WheelCollider wc, bool rear)
    {
        if (wc == null) return;

        WheelFrictionCurve fwd = wc.forwardFriction;
        fwd.extremumSlip = rear ? 0.34f : 0.32f;
        fwd.extremumValue = rear ? 1.7f : 1.8f;
        fwd.asymptoteSlip = rear ? 0.86f : 0.82f;
        fwd.asymptoteValue = 0.95f;
        wc.forwardFriction = fwd;

        WheelFrictionCurve side = wc.sidewaysFriction;
        side.extremumSlip = rear ? 0.24f : 0.22f;
        side.extremumValue = rear ? 1.6f : 1.75f;
        side.asymptoteSlip = rear ? 0.62f : 0.58f;
        side.asymptoteValue = 0.95f;
        wc.sidewaysFriction = side;
    }

    void CreatePoliceTireEffects()
    {
        if (smokeParticles == null || smokeParticles.Length == 0)
        {
            smokeParticles = new ParticleSystem[]
            {
                CreateSmoke("PoliceSmoke_RL", RL != null ? RL.transform.localPosition : new Vector3(-0.75f, 0.2f, -1.4f)),
                CreateSmoke("PoliceSmoke_RR", RR != null ? RR.transform.localPosition : new Vector3(0.75f, 0.2f, -1.4f))
            };
        }

        if (skidTrails == null || skidTrails.Length == 0)
        {
            skidTrails = new TrailRenderer[]
            {
                CreateSkidTrail("PoliceSkid_RL", RL != null ? RL.transform.localPosition : new Vector3(-0.75f, 0.05f, -1.4f)),
                CreateSkidTrail("PoliceSkid_RR", RR != null ? RR.transform.localPosition : new Vector3(0.75f, 0.05f, -1.4f))
            };
        }
    }

    ParticleSystem CreateSmoke(string name, Vector3 localPosition)
    {
        GameObject smokeGo = new GameObject(name);
        smokeGo.transform.SetParent(transform, false);
        smokeGo.transform.localPosition = localPosition + new Vector3(0f, -0.22f, 0f);

        ParticleSystem ps = smokeGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = 0.85f;
        main.startSpeed = 0.2f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startColor = new Color(0.82f, 0.82f, 0.82f, 0.25f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 250;

        var emission = ps.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        Material sharedMaterial = RuntimeSharedResources.GetTintedSpriteMaterial(Color.white);
        if (sharedMaterial != null)
        {
            renderer.sharedMaterial = sharedMaterial;
        }

        return ps;
    }

    TrailRenderer CreateSkidTrail(string name, Vector3 localPosition)
    {
        GameObject trailGo = new GameObject(name);
        trailGo.transform.SetParent(transform, false);
        trailGo.transform.localPosition = localPosition + new Vector3(0f, -0.36f, 0f);
        trailGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        TrailRenderer trail = trailGo.AddComponent<TrailRenderer>();
        trail.emitting = false;
        trail.time = 2.5f;
        trail.minVertexDistance = 0.15f;
        trail.alignment = LineAlignment.TransformZ;
        trail.startWidth = 0.18f;
        trail.endWidth = 0.18f;

        Material sharedMaterial = RuntimeSharedResources.GetTintedSpriteMaterial(new Color(0f, 0f, 0f, 0.55f));
        if (sharedMaterial != null)
        {
            trail.sharedMaterial = sharedMaterial;
        }

        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.black, 1f) },
            new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0.45f, 0.85f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;

        return trail;
    }

    void SetupAudio()
    {
        if (playerTarget != null && tireSquealClip == null)
        {
            SuperminiCarController playerCtrl = playerTarget.GetComponent<SuperminiCarController>();
            if (playerCtrl != null)
            {
                tireSquealClip = playerCtrl.tireSquealSound;
            }
        }

        if (engineClip != null)
        {
            engineSource = gameObject.AddComponent<AudioSource>();
            engineSource.clip = engineClip;
            engineSource.loop = true;
            engineSource.spatialBlend = 1f;
            engineSource.volume = 0.45f * GameAudioRouting.GetSfxGainMultiplier(sfxMixerGroup);
            engineSource.dopplerLevel = 0f;
            GameAudioRouting.ConfigureSfxSource(engineSource, sfxMixerGroup, 160);
            engineSource.Play();
        }

        if (sirenClip != null)
        {
            RecalculateSirenLoopWindow();
            preparedSirenLoopClip = CreatePreparedSirenLoopClip();

            sirenSource = gameObject.AddComponent<AudioSource>();
            sirenSource.clip = preparedSirenLoopClip != null ? preparedSirenLoopClip : sirenClip;
            sirenSource.loop = loopSiren;
            sirenSource.playOnAwake = false;
            sirenSource.spatialBlend = 1f;
            sirenSource.rolloffMode = AudioRolloffMode.Logarithmic;
            sirenSource.minDistance = Mathf.Max(0.1f, sirenMinDistance);
            sirenSource.maxDistance = Mathf.Max(sirenSource.minDistance + 1f, sirenMaxDistance);
            sirenSource.dopplerLevel = sirenDopplerLevel;
            sirenSource.spread = 0f;
            sirenSource.volume = 0f;
            sirenSource.pitch = 1f;
            GameAudioRouting.ConfigureSfxSource(sirenSource, sfxMixerGroup, 128);
        }

        if (tireSquealClip != null)
        {
            tireSquealSource = gameObject.AddComponent<AudioSource>();
            tireSquealSource.clip = tireSquealClip;
            tireSquealSource.loop = true;
            tireSquealSource.spatialBlend = 1f;
            tireSquealSource.volume = 0f;
            tireSquealSource.dopplerLevel = 0f;
            GameAudioRouting.ConfigureSfxSource(tireSquealSource, sfxMixerGroup, 192);
            tireSquealSource.Play();
        }
    }

    [ContextMenu("Force Find Player Target")]
    void ForceAssignTarget()
    {
        GameObject playerObj = GameObject.Find("SuperminiCar");
        if (playerObj != null)
        {
            playerTarget = playerObj.transform;
            Debug.Log("Manually linked playerTarget to SuperminiCar.");
        }
    }

    void InitializeDamageVisuals()
    {
        damageVisuals = GetComponent<VehicleDamageVisuals>();
        if (damageVisuals == null)
        {
            damageVisuals = gameObject.AddComponent<VehicleDamageVisuals>();
        }

        damageVisuals.Initialize();
        UpdateDamageVisuals(true);
    }

    void UpdateDamageVisuals(bool force = false)
    {
        if (damageVisuals == null || maxHealth <= 0f)
        {
            return;
        }

        float healthNormalized = Mathf.Clamp01(currentHealth / maxHealth);
        if (!force && Mathf.Abs(lastDamageVisualHealth - healthNormalized) < 0.005f)
        {
            return;
        }

        lastDamageVisualHealth = healthNormalized;
        damageVisuals.SetHealthNormalized(healthNormalized);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (!isChasing || playerTarget == null)
        {
            ApplyDrive(0f, maxBrakeTorque * 0.2f);
            SetSirenState(false);
            ApplyVehicleStability();
            ApplySpawnSettleAssist();
            UpdateWheelVisuals();
            UpdateTireEffects();
            UpdateEngineAudio();
            return;
        }

        RecordTrail();
        FollowTrail();
        SetSirenState(true);
        ApplyVehicleStability();
        ApplySpawnSettleAssist();
        UpdateWheelVisuals();
        UpdateTireEffects();
        UpdateEngineAudio();
    }

    void RecordTrail()
    {
        if (playerTarget == null)
        {
            return;
        }

        trailTimer += Time.fixedDeltaTime;
        if (trailTimer < 0.08f) return;
        trailTimer = 0f;

        Vector3 playerPos = playerTarget.position;
        if (trail.Count == 0)
        {
            trail.Add(playerPos);
            return;
        }

        float minSpacingSqr = trailPointSpacing * trailPointSpacing;
        if (FlatDistanceSqr(playerPos, trail[trail.Count - 1]) >= minSpacingSqr)
        {
            trail.Add(playerPos);
        }
        else
        {
            trail[trail.Count - 1] = Vector3.Lerp(trail[trail.Count - 1], playerPos, 0.35f);
        }

        int targetTrailCount = Mathf.Max(12, maxTrailPoints);
        int trimThreshold = targetTrailCount * 2;
        if (trail.Count > trimThreshold)
        {
            trail.RemoveRange(0, trail.Count - targetTrailCount);
        }
    }

    void FollowTrail()
    {
        if (playerTarget == null) return;

        if (stunTimer > 0f)
        {
            stunTimer -= Time.fixedDeltaTime;
            ApplyDrive(0f, maxBrakeTorque * 0.35f);
            return;
        }

        Rigidbody playerRb = playerTarget.GetComponent<Rigidbody>();
        Vector3 playerVel = playerRb != null ? playerRb.linearVelocity : Vector3.zero;
        float distToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        float playerSpeed = playerVel.magnitude;
        float playerSpeedKmh = playerSpeed * 3.6f;
        float highSpeedFactor = GetHighSpeedPursuitFactor(playerSpeedKmh);
        float dynamicCatchUpDistance = Mathf.Lerp(catchUpDistance, Mathf.Max(catchUpDistance * 0.85f, catchUpDistance - 3f), highSpeedFactor);
        float dynamicRecoveryDistance = Mathf.Lerp(recoveryDistance, highSpeedRecoveryDistance, highSpeedFactor);
        float dynamicInterceptDistance = Mathf.Lerp(interceptRecoveryDistance, highSpeedInterceptDistance, highSpeedFactor);

        if (TryRecoverFarBehind(playerVel, distToPlayer, highSpeedFactor, dynamicRecoveryDistance, dynamicInterceptDistance))
        {
            return;
        }

        bool recoveryMode = distToPlayer > dynamicCatchUpDistance;
        Vector3 destination = GetTrailDestination(playerVel, distToPlayer, recoveryMode, dynamicCatchUpDistance, dynamicRecoveryDistance);

        if (reverseRecoveryTimer > 0f)
        {
            reverseRecoveryTimer -= Time.fixedDeltaTime;
            RunReverseRecovery(destination);
            return;
        }

        float recoveryT = Mathf.InverseLerp(dynamicCatchUpDistance, Mathf.Max(dynamicCatchUpDistance + 1f, dynamicRecoveryDistance), distToPlayer);
        float targetSpeed = Mathf.Max(chaseSpeed, playerSpeed * playerSpeedMatchFactor);
        if (recoveryMode)
        {
            float boostedPlayerMatch = playerSpeed * Mathf.Lerp(playerSpeedMatchFactor, playerSpeedMatchFactor * (1.12f + highSpeedFactor * 0.18f), recoveryT);
            float boostedCruiseSpeed = chaseSpeed * Mathf.Lerp(1.1f, catchUpSpeedMultiplier * Mathf.Lerp(1f, highSpeedCatchUpSpeedMultiplier, highSpeedFactor), recoveryT);
            targetSpeed = Mathf.Max(targetSpeed, boostedPlayerMatch, boostedCruiseSpeed);
        }

        float maxAllowedSpeed = Mathf.Max(chaseSpeed, maxChaseSpeed * Mathf.Lerp(1f, highSpeedCatchUpSpeedMultiplier, highSpeedFactor));
        targetSpeed = Mathf.Clamp(targetSpeed * Mathf.Lerp(1f, highSpeedCatchUpSpeedMultiplier, highSpeedFactor * 0.55f), chaseSpeed * 0.85f, maxAllowedSpeed);
        float currentAccel = acceleration * (recoveryMode ? Mathf.Lerp(1f, recoveryAccelerationMultiplier, 0.6f + recoveryT * 0.4f) : 1f) * Mathf.Lerp(1f, highSpeedAccelerationMultiplier, highSpeedFactor);

        UpdateUnstuckState(targetSpeed, distToPlayer);
        if (reverseRecoveryTimer > 0f)
        {
            RunReverseRecovery(destination);
            return;
        }

        MoveTowards(destination, targetSpeed, currentAccel);
    }

    void MoveTowards(Vector3 targetPos, float targetSpeed, float currentAccel)
    {
        Vector3 myPosFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 targetFlat = new Vector3(targetPos.x, 0f, targetPos.z);
        Vector3 direction = (targetFlat - myPosFlat).normalized;
        if (direction == Vector3.zero)
        {
            ApplyDrive(0f, maxBrakeTorque * 0.2f);
            return;
        }

        float dotForward = Vector3.Dot(transform.forward, direction);
        float turnFactor = Mathf.Clamp01(dotForward);
        float penalizedSpeed = targetSpeed * Mathf.Lerp(turningPenalty, 1f, turnFactor);
        currentSpeed = Mathf.MoveTowards(currentSpeed, penalizedSpeed, currentAccel * Time.fixedDeltaTime);

        float signedTurn = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
        float speedKmh = rb.linearVelocity.magnitude * 3.6f;
        float steerReduction = Mathf.Lerp(1f, highSpeedSteerReduction, Mathf.InverseLerp(40f, 180f, speedKmh));
        float steerInput = Mathf.Clamp(signedTurn / Mathf.Max(maxSteerAngle, 1f), -1f, 1f) * steerReduction;
        UpdateFrontWheelSteer(steerInput);

        Quaternion targetRot = Quaternion.LookRotation(direction);
        float maxRotationStep = rotationSpeed * Mathf.Lerp(12f, 6f, Mathf.InverseLerp(0f, Mathf.Max(1f, maxChaseSpeed), Mathf.Abs(currentSpeed))) * Time.fixedDeltaTime;
        rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, targetRot, maxRotationStep));

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float speedError = currentSpeed - forwardSpeed;
        float throttle = Mathf.Clamp01(speedError / 6f);
        float brakeInput = 0f;
        float corneringT = Mathf.InverseLerp(18f, 75f, Mathf.Abs(signedTurn));
        throttle *= Mathf.Lerp(1f, 0.45f, corneringT * Mathf.InverseLerp(60f, 170f, speedKmh));

        if (speedError < -1.5f)
        {
            brakeInput = Mathf.InverseLerp(-8f, -1.5f, speedError);
        }

        if (Mathf.Abs(signedTurn) > 32f && forwardSpeed > currentSpeed * 0.65f)
        {
            brakeInput = Mathf.Max(brakeInput, Mathf.InverseLerp(32f, 85f, Mathf.Abs(signedTurn)) * 0.85f);
        }

        ApplyDrive(throttle * maxMotorTorque, brakeInput * maxBrakeTorque);

        rb.AddForce(transform.forward * throttle * currentAccel * 1.25f, ForceMode.Acceleration);

        if (brakeInput > 0f && forwardSpeed > 0f)
        {
            rb.AddForce(-transform.forward * brakeInput * 10f, ForceMode.Acceleration);
        }
    }

    void SeedTrailWithPlayerPosition()
    {
        if (playerTarget != null && trail.Count == 0)
        {
            trail.Add(playerTarget.position);
        }
    }

    Vector3 GetTrailDestination(Vector3 playerVelocity, float distToPlayer, bool recoveryMode, float dynamicCatchUpDistance, float dynamicRecoveryDistance)
    {
        if (playerTarget == null)
        {
            return transform.position + transform.forward * 10f;
        }

        SeedTrailWithPlayerPosition();

        int nearestIndex = FindNearestTrailIndex(transform.position);
        int extraLookAhead = Mathf.RoundToInt(Mathf.InverseLerp(12f, 45f, playerVelocity.magnitude) * 3f);
        if (recoveryMode)
        {
            extraLookAhead += Mathf.RoundToInt(Mathf.InverseLerp(dynamicCatchUpDistance, Mathf.Max(dynamicCatchUpDistance + 1f, dynamicRecoveryDistance), distToPlayer) * 6f);
        }

        int targetIndex = Mathf.Clamp(nearestIndex + Mathf.Max(1, trailLookAheadPoints) + extraLookAhead, 0, trail.Count - 1);

        float reachDistanceSqr = trailReachDistance * trailReachDistance;
        if (nearestIndex > 2 && FlatDistanceSqr(transform.position, trail[nearestIndex]) <= reachDistanceSqr)
        {
            int removeCount = Mathf.Max(0, nearestIndex - 1);
            if (removeCount > 0)
            {
                trail.RemoveRange(0, removeCount);
                targetIndex = Mathf.Clamp(targetIndex - removeCount, 0, trail.Count - 1);
            }
        }

        Vector3 destination = trail[targetIndex];
        if (targetIndex >= trail.Count - 2)
        {
            destination += playerVelocity * Mathf.Max(0.2f, playerPredictionSeconds);
        }

        if (distToPlayer < closeChaseDistance)
        {
            Vector3 playerForward = playerTarget.forward;
            playerForward.y = 0f;
            if (playerForward.sqrMagnitude < 0.001f)
            {
                playerForward = new Vector3(playerVelocity.x, 0f, playerVelocity.z);
            }
            if (playerForward.sqrMagnitude < 0.001f)
            {
                playerForward = transform.forward;
            }

            destination = playerTarget.position - playerForward.normalized * closeChaseOffset;
        }

        if (TryGetGroundHit(destination, out RaycastHit groundedHit))
        {
            destination = groundedHit.point;
        }

        return destination;
    }

    int FindNearestTrailIndex(Vector3 position)
    {
        if (trail.Count == 0)
        {
            return 0;
        }

        int bestIndex = trail.Count - 1;
        float bestDistanceSqr = float.MaxValue;
        for (int i = 0; i < trail.Count; i++)
        {
            float distanceSqr = FlatDistanceSqr(position, trail[i]);
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    void UpdateUnstuckState(float targetSpeed, float distToPlayer)
    {
        if (rb == null || distToPlayer < closeChaseDistance * 0.8f)
        {
            stuckTimer = 0f;
            return;
        }

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;
        bool shouldBeMoving = targetSpeed > 10f && currentSpeed > 5f && stunTimer <= 0f;
        bool littleProgress = speedKmh < stuckSpeedThresholdKmh;

        if (shouldBeMoving && littleProgress)
        {
            stuckTimer += Time.fixedDeltaTime;
        }
        else
        {
            stuckTimer = Mathf.Max(0f, stuckTimer - Time.fixedDeltaTime * 2f);
        }

        if (stuckTimer < stuckTimeout)
        {
            return;
        }

        reverseRecoveryTimer = reverseRecoverDuration;
        reverseSteerDirection = Random.value < 0.5f ? -1f : 1f;
        currentSpeed = 0f;
        stuckTimer = 0f;
    }

    void RunReverseRecovery(Vector3 destination)
    {
        Vector3 localTarget = transform.InverseTransformPoint(destination);
        float steerDirection = Mathf.Abs(localTarget.x) > 0.2f ? Mathf.Sign(localTarget.x) : reverseSteerDirection;
        UpdateFrontWheelSteer(-steerDirection * 0.75f);
        ApplyDrive(-Mathf.Abs(reverseTorque), 0f);
        rb.AddForce(-transform.forward * acceleration * 0.85f, ForceMode.Acceleration);
    }

    bool TryRecoverFarBehind(Vector3 playerVelocity, float distToPlayer, float highSpeedFactor, float dynamicRecoveryDistance, float dynamicInterceptDistance)
    {
        if (playerTarget == null || rb == null)
        {
            return false;
        }

        if (distToPlayer < dynamicRecoveryDistance)
        {
            farSeparationTimer = Mathf.Max(0f, farSeparationTimer - Time.fixedDeltaTime * 2f);
            return false;
        }

        farSeparationTimer += Time.fixedDeltaTime;
        float recoveryDelay = interceptRecoveryDelay * Mathf.Lerp(1f, highSpeedRecoveryDelayMultiplier, highSpeedFactor);
        float recoveryCooldown = interceptRecoveryCooldown * Mathf.Lerp(1f, highSpeedRecoveryCooldownMultiplier, highSpeedFactor);
        bool shouldIntercept = distToPlayer >= dynamicInterceptDistance &&
                               farSeparationTimer >= recoveryDelay &&
                               Time.time - lastInterceptRecoveryTime >= recoveryCooldown;

        if (!shouldIntercept)
        {
            return false;
        }

        if (!TryRepositionBehindPlayer(playerVelocity, dynamicInterceptDistance, highSpeedFactor))
        {
            return false;
        }

        farSeparationTimer = 0f;
        lastInterceptRecoveryTime = Time.time;
        reverseRecoveryTimer = 0f;
        stuckTimer = 0f;
        currentSpeed = Mathf.Max(chaseSpeed * Mathf.Lerp(0.75f, 0.95f, highSpeedFactor), Vector3.Dot(rb.linearVelocity, transform.forward));
        return true;
    }

    bool TryRepositionBehindPlayer(Vector3 playerVelocity, float dynamicInterceptDistance, float highSpeedFactor)
    {
        Vector3 playerForward = GetPlayerTravelDirection(playerVelocity);
        Vector3 playerRight = Vector3.Cross(Vector3.up, playerForward).normalized;
        float preferredSide = Random.value < 0.5f ? -1f : 1f;
        float behindDistance = Mathf.Lerp(interceptForwardDistance, interceptForwardDistance * 0.92f, highSpeedFactor);
        float sideOffset = Mathf.Lerp(interceptSideOffset, interceptSideOffset * 0.82f, highSpeedFactor);
        Vector3 trailAnchor = GetRecoverySpawnAnchor(playerForward, behindDistance);

        Vector3[] candidates =
        {
            trailAnchor,
            trailAnchor + playerRight * sideOffset * 0.55f * preferredSide,
            trailAnchor - playerRight * sideOffset * 0.55f * preferredSide,
            playerTarget.position - playerForward * (behindDistance * 1.18f)
        };

        Vector3 bestPosition = transform.position;
        Quaternion bestRotation = transform.rotation;
        bool foundPosition = false;
        float bestScore = float.MaxValue;

        foreach (Vector3 candidate in candidates)
        {
            Vector3 lookDirection = playerTarget.position + playerVelocity * 0.2f - candidate;
            if (!TryGetSpawnPose(candidate, lookDirection, out Vector3 groundedCandidate, out Quaternion candidateRotation, out float clearanceScore))
            {
                continue;
            }

            float distanceToPlayer = Vector3.Distance(groundedCandidate, playerTarget.position);
            if (distanceToPlayer < openingSpawnMinDistance || distanceToPlayer > dynamicInterceptDistance)
            {
                continue;
            }

            float score = FlatDistanceSqr(transform.position, groundedCandidate) + FlatDistanceSqr(groundedCandidate, playerTarget.position) * 0.35f - clearanceScore * 5f;
            if (score < bestScore)
            {
                bestScore = score;
                bestPosition = groundedCandidate;
                bestRotation = candidateRotation;
                foundPosition = true;
            }
        }

        if (!foundPosition)
        {
            return false;
        }

        float carrySpeed = Mathf.Clamp(playerVelocity.magnitude * interceptSpeedFactor, chaseSpeed * 0.8f, maxChaseSpeed * 0.7f);
        ApplySpawnPose(bestPosition, bestRotation, carrySpeed);

        trail.Clear();
        SeedTrailWithPlayerPosition();
        return true;
    }

    Vector3 GetRecoverySpawnAnchor(Vector3 playerForward, float behindDistance)
    {
        if (trail.Count > recoveryTrailLookbackPoints)
        {
            Vector3 trailAnchor = trail[Mathf.Max(0, trail.Count - 1 - recoveryTrailLookbackPoints)];
            Vector3 offset = trailAnchor - playerTarget.position;
            offset.y = 0f;
            if (offset.sqrMagnitude >= openingSpawnMinDistance * openingSpawnMinDistance)
            {
                return trailAnchor;
            }
        }

        return playerTarget.position - playerForward * behindDistance;
    }

    Vector3 GetPlayerVelocity()
    {
        if (playerTarget == null)
        {
            return Vector3.zero;
        }

        Rigidbody playerRb = playerTarget.GetComponent<Rigidbody>();
        return playerRb != null ? playerRb.linearVelocity : Vector3.zero;
    }

    Vector3 GetPlayerTravelDirection(Vector3 playerVelocity)
    {
        Vector3 direction = new Vector3(playerVelocity.x, 0f, playerVelocity.z);
        if (direction.sqrMagnitude < 4f)
        {
            direction = playerTarget != null ? playerTarget.forward : Vector3.forward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector3.forward;
        }

        return direction.normalized;
    }

    bool TryGetSpawnPose(Vector3 candidate, Vector3 desiredForward, out Vector3 spawnPosition, out Quaternion spawnRotation, out float clearanceScore)
    {
        spawnPosition = candidate;
        spawnRotation = transform.rotation;
        clearanceScore = 0f;

        if (!TryGetGroundHit(candidate, out RaycastHit hit))
        {
            return false;
        }

        Vector3 alignedForward = Vector3.ProjectOnPlane(desiredForward, hit.normal);
        if (alignedForward.sqrMagnitude < 0.001f)
        {
            alignedForward = Vector3.ProjectOnPlane(transform.forward, hit.normal);
        }
        if (alignedForward.sqrMagnitude < 0.001f)
        {
            alignedForward = Vector3.Cross(hit.normal, Vector3.right);
        }

        spawnRotation = Quaternion.LookRotation(alignedForward.normalized, hit.normal);
        float lift = GetSpawnLiftOffset() + spawnHeightOffset;
        spawnPosition = hit.point + hit.normal * Mathf.Max(0.02f, lift);
        return IsSpawnPoseClear(spawnPosition, spawnRotation, out clearanceScore);
    }

    bool TryGetGroundHit(Vector3 candidate, out RaycastHit bestHit)
    {
        Vector3 rayOrigin = candidate + Vector3.up * spawnRaycastHeight;
        int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, SpawnGroundHits, spawnRaycastHeight + spawnMaxRayDistance, spawnSurfaceMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        bestHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = SpawnGroundHits[i];
            if (hit.collider == null)
            {
                continue;
            }

            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.normal.y < spawnSurfaceNormalMinY)
            {
                continue;
            }

            string lowerName = hit.collider.gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("wheel") || lowerName.Contains("smoke") || lowerName.Contains("trail"))
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
            }
        }

        return bestDistance < float.MaxValue;
    }

    float GetSpawnLiftOffset()
    {
        float localBottom = spawnBoundsCenterLocal.y - spawnBoundsHalfExtents.y;
        float colliderBottomOffset = localBottom < 0f ? -localBottom : 0.2f;
        float wheelClearanceOffset = wheelRadius + suspensionDistance * 0.5f;
        return Mathf.Max(0.2f, spawnGroundOffset, colliderBottomOffset, wheelClearanceOffset);
    }

    void ApplySpawnPose(Vector3 position, Quaternion rotation, float carrySpeed)
    {
        transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();

        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = rotation * Vector3.forward * Mathf.Max(0f, carrySpeed);
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }

        currentSpeed = Mathf.Max(0f, carrySpeed);
        spawnSettleTimer = Mathf.Max(spawnSettleTimer, spawnSettleBrakeTime);
    }

    void ApplySpawnSettleAssist()
    {
        if (rb == null || spawnSettleTimer <= 0f)
        {
            return;
        }

        spawnSettleTimer = Mathf.Max(0f, spawnSettleTimer - Time.fixedDeltaTime);
        if (rb.linearVelocity.magnitude < 1.5f)
        {
            ApplyDrive(0f, maxBrakeTorque * 0.35f);
        }

        if (spawnSettleDownForce > 0f)
        {
            rb.AddForce(Vector3.down * spawnSettleDownForce, ForceMode.Acceleration);
        }
    }

    bool IsSpawnPoseClear(Vector3 position, Quaternion rotation, out float clearanceScore)
    {
        Vector3 halfExtents = spawnBoundsHalfExtents + spawnClearancePadding;
        Vector3 worldCenter = position + rotation * spawnBoundsCenterLocal;
        int overlapCount = Physics.OverlapBoxNonAlloc(worldCenter, halfExtents, SpawnOverlapHits, rotation, spawnSurfaceMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider col = SpawnOverlapHits[i];
            if (IsSpawnBlockingCollider(col))
            {
                clearanceScore = 0f;
                return false;
            }
        }

        clearanceScore = spawnForwardClearanceDistance;
        if (Physics.SphereCast(worldCenter, spawnForwardClearanceRadius, rotation * Vector3.forward, out RaycastHit hit, spawnForwardClearanceDistance, spawnSurfaceMask, QueryTriggerInteraction.Ignore) &&
            IsSpawnBlockingCollider(hit.collider))
        {
            clearanceScore = hit.distance;
            return false;
        }

        return true;
    }

    bool IsSpawnBlockingCollider(Collider col)
    {
        if (col == null || col.isTrigger)
        {
            return false;
        }

        if (col.transform.IsChildOf(transform))
        {
            return false;
        }

        string lowerName = col.gameObject.name.ToLowerInvariant();
        if (lowerName.Contains("road") ||
            lowerName.Contains("ground") ||
            lowerName.Contains("asphalt") ||
            lowerName.Contains("wheel") ||
            lowerName.Contains("smoke") ||
            lowerName.Contains("trail"))
        {
            return false;
        }

        return true;
    }

    float GetHighSpeedPursuitFactor(float playerSpeedKmh)
    {
        if (highSpeedPlayerThresholdKmh <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01((playerSpeedKmh - highSpeedPlayerThresholdKmh) / 45f);
    }

    static float FlatDistanceSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    void UpdateFrontWheelSteer(float steerInput)
    {
        float steerAngle = steerInput * maxSteerAngle;
        if (FL != null) FL.steerAngle = steerAngle;
        if (FR != null) FR.steerAngle = steerAngle;
    }

    void ApplyDrive(float motorTorque, float brakeTorque)
    {
        if (FL != null)
        {
            FL.motorTorque = 0f;
            FL.brakeTorque = brakeTorque;
        }

        if (FR != null)
        {
            FR.motorTorque = 0f;
            FR.brakeTorque = brakeTorque;
        }

        if (RL != null)
        {
            RL.motorTorque = motorTorque;
            RL.brakeTorque = brakeTorque;
        }

        if (RR != null)
        {
            RR.motorTorque = motorTorque;
            RR.brakeTorque = brakeTorque;
        }
    }

    void ApplyVehicleStability()
    {
        if (rb == null) return;

        rb.AddForce(-transform.up * downforce * rb.linearVelocity.sqrMagnitude);
        Vector3 lateralVelocity = Vector3.Project(rb.linearVelocity, transform.right);
        rb.AddForce(-lateralVelocity * lateralVelocityDamping, ForceMode.Acceleration);
        rb.AddTorque(Vector3.up * (-rb.angularVelocity.y * yawStability), ForceMode.Acceleration);
        DoAntiRoll(FL, FR, antiRoll);
        DoAntiRoll(RL, RR, antiRoll);
    }

    static void DoAntiRoll(WheelCollider leftWheel, WheelCollider rightWheel, float antiRollForce)
    {
        if (leftWheel == null || rightWheel == null) return;

        bool leftGrounded = leftWheel.GetGroundHit(out WheelHit leftHit);
        bool rightGrounded = rightWheel.GetGroundHit(out WheelHit rightHit);
        float leftTravel = 1f;
        float rightTravel = 1f;

        if (leftGrounded)
        {
            leftTravel = (-leftWheel.transform.InverseTransformPoint(leftHit.point).y - leftWheel.radius) / leftWheel.suspensionDistance;
        }

        if (rightGrounded)
        {
            rightTravel = (-rightWheel.transform.InverseTransformPoint(rightHit.point).y - rightWheel.radius) / rightWheel.suspensionDistance;
        }

        float force = (leftTravel - rightTravel) * antiRollForce;
        if (leftGrounded) leftWheel.attachedRigidbody.AddForceAtPosition(leftWheel.transform.up * -force, leftWheel.transform.position);
        if (rightGrounded) rightWheel.attachedRigidbody.AddForceAtPosition(rightWheel.transform.up * force, rightWheel.transform.position);
    }

    void UpdateWheelVisuals()
    {
        UpdateOneWheel(FL, FLMesh);
        UpdateOneWheel(FR, FRMesh);
    }

    static void UpdateOneWheel(WheelCollider wc, Transform mesh)
    {
        if (wc == null || mesh == null) return;

        wc.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.SetPositionAndRotation(pos, rot);
    }

    void UpdateTireEffects()
    {
        float speedKmh = rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;

        maxSlipIntensity = 0f;
        maxSlipIntensity = Mathf.Max(maxSlipIntensity, GetSlipIntensity(FL));
        maxSlipIntensity = Mathf.Max(maxSlipIntensity, GetSlipIntensity(FR));
        maxSlipIntensity = Mathf.Max(maxSlipIntensity, GetSlipIntensity(RL));
        maxSlipIntensity = Mathf.Max(maxSlipIntensity, GetSlipIntensity(RR));

        bool allowSqueal = maxSlipIntensity > slipEffectThreshold && speedKmh > squealMinSpeedKmh;
        bool allowSkid = maxSlipIntensity > skidEffectThreshold && speedKmh > skidMarkMinSpeedKmh;
        bool allowSmoke = maxSlipIntensity > smokeEffectThreshold && speedKmh > smokeMinSpeedKmh;

        float squealStrength = allowSqueal ? Mathf.InverseLerp(slipEffectThreshold, 1f, maxSlipIntensity) : 0f;
        float skidStrength = allowSkid ? Mathf.InverseLerp(skidEffectThreshold, 1f, maxSlipIntensity) : 0f;
        float smokeStrength = allowSmoke ? Mathf.InverseLerp(smokeEffectThreshold, 1f, maxSlipIntensity) : 0f;

        if (tireSquealSource != null)
        {
            if (allowSqueal)
            {
                float speedFactor = Mathf.InverseLerp(squealMinSpeedKmh, 160f, speedKmh);
                float targetVolume = Mathf.Lerp(0.03f, 0.18f, squealStrength * speedFactor) * GameAudioRouting.GetSfxGainMultiplier(sfxMixerGroup);
                tireSquealSource.volume = Mathf.Lerp(tireSquealSource.volume, targetVolume, Time.fixedDeltaTime * 10f);
                tireSquealSource.pitch = 0.92f + squealStrength * 0.14f;
            }
            else
            {
                tireSquealSource.volume = Mathf.Lerp(tireSquealSource.volume, 0f, Time.fixedDeltaTime * 10f);
            }
        }

        if (smokeParticles != null)
        {
            foreach (ParticleSystem ps in smokeParticles)
            {
                if (ps == null) continue;

                var emission = ps.emission;
                if (allowSmoke)
                {
                    float speedFactor = Mathf.InverseLerp(smokeMinSpeedKmh, 170f, speedKmh);
                    emission.enabled = true;
                    emission.rateOverTime = Mathf.Lerp(4f, 16f, smokeStrength * speedFactor);
                    if (!ps.isPlaying) ps.Play();
                }
                else
                {
                    emission.enabled = false;
                    emission.rateOverTime = 0f;
                    if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        if (skidTrails != null)
        {
            bool shouldSkid = allowSkid && skidStrength > 0.1f;
            foreach (TrailRenderer trail in skidTrails)
            {
                if (trail == null) continue;
                trail.emitting = shouldSkid;
            }
        }
    }

    float GetSlipIntensity(WheelCollider wc)
    {
        if (wc == null || !wc.isGrounded) return 0f;

        wc.GetGroundHit(out WheelHit hit);
        float sideSlip = Mathf.Abs(hit.sidewaysSlip);
        float forwardSlip = Mathf.Abs(hit.forwardSlip);

        float sideIntensity = 0f;
        float forwardIntensity = 0f;

        if (sideSlip > sidewaysSlipThreshold)
        {
            sideIntensity = (sideSlip - sidewaysSlipThreshold) / (1f - sidewaysSlipThreshold);
        }

        if (forwardSlip > forwardSlipThreshold)
        {
            forwardIntensity = (forwardSlip - forwardSlipThreshold) / (1f - forwardSlipThreshold);
        }

        return Mathf.Clamp01(Mathf.Max(sideIntensity, forwardIntensity));
    }

    void UpdateEngineAudio()
    {
        if (engineSource == null || rb == null) return;

        float forwardSpeed = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.forward));
        float targetPitch = 0.85f + Mathf.Clamp01(forwardSpeed / 26f) * 0.8f;
        engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetPitch, Time.fixedDeltaTime * 4f);
    }

    void RecalculateSirenLoopWindow()
    {
        sirenLoopStartSample = 0;
        sirenLoopSampleCount = 0;

        if (sirenClip == null)
        {
            return;
        }

        if (sirenClip.loadState == AudioDataLoadState.Unloaded)
        {
            sirenClip.LoadAudioData();
        }

        int clipSamples = Mathf.Max(1, sirenClip.samples);
        int frequency = Mathf.Max(1, sirenClip.frequency);
        int loopStartSample = 0;
        int loopEndSample = clipSamples;

        if (TryDetectAudibleSampleWindow(out int detectedStartSample, out int detectedEndSample))
        {
            int safetySamples = Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0f, sirenLoopSafetyPaddingSeconds) * frequency));
            loopStartSample = Mathf.Clamp(detectedStartSample, 0, clipSamples - 1);
            loopEndSample = Mathf.Clamp(detectedEndSample + safetySamples, loopStartSample + 1, clipSamples);
        }

        sirenLoopStartSample = loopStartSample;
        sirenLoopSampleCount = Mathf.Clamp(loopEndSample - loopStartSample, 1, clipSamples);
    }

    bool TryDetectAudibleSampleWindow(out int startSample, out int endSample)
    {
        startSample = 0;
        endSample = 0;

        if (sirenClip == null || sirenClip.samples <= 0)
        {
            return false;
        }

        if (sirenClip.loadState == AudioDataLoadState.Unloaded)
        {
            sirenClip.LoadAudioData();
        }

        if (sirenClip.loadState != AudioDataLoadState.Loaded)
        {
            return false;
        }

        int channelCount = Mathf.Max(1, sirenClip.channels);
        float[] sampleData = new float[sirenClip.samples * channelCount];
        if (!sirenClip.GetData(sampleData, 0))
        {
            return false;
        }

        float threshold = Mathf.Max(0.0001f, sirenSilenceThreshold);
        int firstAudibleSample = -1;
        int lastAudibleSample = -1;

        for (int sampleIndex = 0; sampleIndex < sirenClip.samples; sampleIndex++)
        {
            int dataIndex = sampleIndex * channelCount;
            float peak = 0f;
            for (int channel = 0; channel < channelCount; channel++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(sampleData[dataIndex + channel]));
            }

            if (peak >= threshold)
            {
                firstAudibleSample = sampleIndex;
                break;
            }
        }

        if (firstAudibleSample < 0)
        {
            return false;
        }

        for (int sampleIndex = sirenClip.samples - 1; sampleIndex >= firstAudibleSample; sampleIndex--)
        {
            int dataIndex = sampleIndex * channelCount;
            float peak = 0f;
            for (int channel = 0; channel < channelCount; channel++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(sampleData[dataIndex + channel]));
            }

            if (peak >= threshold)
            {
                lastAudibleSample = sampleIndex + 1;
                break;
            }
        }

        if (lastAudibleSample <= firstAudibleSample)
        {
            return false;
        }

        startSample = firstAudibleSample;
        endSample = lastAudibleSample;
        return true;
    }

    AudioClip CreatePreparedSirenLoopClip()
    {
        if (!TryExtractSirenLoopSamples(out float[] loopData, out int channelCount, out int frequency))
        {
            return null;
        }

        if (loopData == null || loopData.Length == 0 || channelCount <= 0 || frequency <= 0)
        {
            return null;
        }

        AudioClip loopClip = AudioClip.Create($"{sirenClip.name}_RuntimeLoop", loopData.Length / channelCount, channelCount, frequency, false);
        if (loopClip == null)
        {
            return null;
        }

        loopClip.SetData(loopData, 0);
        return loopClip;
    }

    bool TryExtractSirenLoopSamples(out float[] loopData, out int channelCount, out int frequency)
    {
        loopData = null;
        channelCount = 0;
        frequency = 0;

        if (sirenClip == null || sirenClip.samples <= 0)
        {
            return false;
        }

        if (sirenClip.loadState == AudioDataLoadState.Unloaded)
        {
            sirenClip.LoadAudioData();
        }

        if (sirenClip.loadState != AudioDataLoadState.Loaded)
        {
            return false;
        }

        channelCount = Mathf.Max(1, sirenClip.channels);
        frequency = Mathf.Max(1, sirenClip.frequency);
        float[] sourceData = new float[sirenClip.samples * channelCount];
        if (!sirenClip.GetData(sourceData, 0))
        {
            return false;
        }

        int startSample = Mathf.Clamp(sirenLoopStartSample, 0, Mathf.Max(0, sirenClip.samples - 1));
        int sampleCount = Mathf.Clamp(sirenLoopSampleCount, 1, sirenClip.samples - startSample);
        loopData = new float[sampleCount * channelCount];
        System.Array.Copy(sourceData, startSample * channelCount, loopData, 0, loopData.Length);

        int edgeFadeSamples = Mathf.Clamp(sirenEdgeFadeSamples, 0, sampleCount / 8);
        if (edgeFadeSamples > 0 && sampleCount > edgeFadeSamples * 2)
        {
            for (int sampleIndex = 0; sampleIndex < edgeFadeSamples; sampleIndex++)
            {
                float t = (sampleIndex + 1f) / (edgeFadeSamples + 1f);
                float fadeIn = Mathf.SmoothStep(0f, 1f, t);
                float fadeOut = Mathf.SmoothStep(1f, 0f, t);

                for (int channel = 0; channel < channelCount; channel++)
                {
                    int frontIndex = sampleIndex * channelCount + channel;
                    int backIndex = ((sampleCount - 1 - sampleIndex) * channelCount) + channel;
                    loopData[frontIndex] *= fadeIn;
                    loopData[backIndex] *= fadeOut;
                }
            }
        }

        return true;
    }

    void UpdateSirenSourceSettings()
    {
        if (sirenSource == null)
        {
            return;
        }

        sirenSource.clip = preparedSirenLoopClip != null ? preparedSirenLoopClip : sirenClip;
        sirenSource.loop = loopSiren;
        sirenSource.minDistance = Mathf.Max(0.1f, sirenMinDistance);
        sirenSource.maxDistance = Mathf.Max(sirenSource.minDistance + 1f, sirenMaxDistance);
        sirenSource.dopplerLevel = sirenDopplerLevel;
    }

    void SetSirenState(bool active)
    {
        if (sirenLightsContainer != null && sirenLightsContainer.activeSelf != active)
        {
            sirenLightsContainer.SetActive(active);
        }

        UpdateSirenAudio(active);
    }

    void UpdateSirenAudio(bool active)
    {
        if (sirenSource == null || (preparedSirenLoopClip == null && sirenClip == null))
        {
            return;
        }

        UpdateSirenSourceSettings();

        float targetVolume = 0f;
        if (active)
        {
            float distanceFactor = 1f;
            if (playerTarget != null)
            {
                float distance = Vector3.Distance(transform.position, playerTarget.position);
                distanceFactor = 1f - Mathf.InverseLerp(sirenMinDistance, sirenMaxDistance, distance);
            }

            targetVolume = sirenVolume * distanceFactor * GameAudioRouting.GetSfxGainMultiplier(sfxMixerGroup);
        }

        float fadeStep = Mathf.Max(0.01f, sirenFadeSpeed) * Time.fixedDeltaTime;
        if (active)
        {
            if (!sirenSource.isPlaying)
            {
                sirenSource.volume = targetVolume;
                sirenSource.Play();
            }
        }
        
        sirenSource.volume = Mathf.MoveTowards(sirenSource.volume, targetVolume, fadeStep);
        if (!active && sirenSource.isPlaying && sirenSource.volume <= 0.001f)
        {
            sirenSource.Stop();
            sirenSource.volume = 0f;
        }
    }

    bool IsEnvironmentCollision(Collision col)
    {
        if (col == null || col.gameObject == null)
        {
            return false;
        }

        if (col.gameObject.GetComponentInParent<SuperminiCarController>() != null)
        {
            return false;
        }

        PoliceController otherPolice = col.gameObject.GetComponentInParent<PoliceController>();
        if (otherPolice != null && otherPolice != this)
        {
            return false;
        }

        string lowerName = col.gameObject.name.ToLowerInvariant();
        if (lowerName.Contains("road") ||
            lowerName.Contains("ground") ||
            lowerName.Contains("asphalt") ||
            lowerName.Contains("wheel") ||
            lowerName.Contains("smoke") ||
            lowerName.Contains("trail"))
        {
            return false;
        }

        Rigidbody otherRb = col.rigidbody != null ? col.rigidbody : col.collider.attachedRigidbody;
        return otherRb == null || otherRb.isKinematic;
    }

    void ApplyCollisionDamage(float impactSeverity, float multiplier, string sourceName)
    {
        if (infiniteHealth)
        {
            currentHealth = maxHealth;
            return;
        }

        if (impactSeverity < crashDamageThreshold)
        {
            return;
        }

        if (Time.unscaledTime - lastDamageTime < damageRepeatCooldown)
        {
            return;
        }

        lastDamageTime = Time.unscaledTime;
        currentHealth = Mathf.Clamp(currentHealth - impactSeverity * multiplier, 0f, maxHealth);
        UpdateDamageVisuals();

        if (currentHealth <= 0f)
        {
            EnterWreckedState(sourceName);
        }
    }

    void EnterWreckedState(string sourceName)
    {
        if (isWrecked)
        {
            return;
        }

        isWrecked = true;
        isChasing = false;
        currentSpeed = 0f;
        stunTimer = Mathf.Max(stunTimer, 1.2f);
        SetSirenState(false);
        UpdateDamageVisuals(true);
        Debug.Log($"[PoliceController] Police car wrecked after impact with {sourceName}.");
    }

    void OnCollisionEnter(Collision col)
    {
        if (rb == null) return;

        bool hitPlayer = col.gameObject.CompareTag("Player") || col.gameObject.GetComponentInParent<SuperminiCarController>() != null;
        bool hitEnvironment = IsEnvironmentCollision(col);

        if (!hitPlayer && !hitEnvironment)
        {
            return;
        }

        float relativeSpeed = col.relativeVelocity.magnitude;
        float impactStrength = col.impulse.magnitude / Mathf.Max(rb.mass, 1f);
        float impactSeverity = Mathf.Max(relativeSpeed, impactStrength * 2.4f);
        float stunDuration = Mathf.Lerp(0.2f, 0.75f, Mathf.InverseLerp(2f, 16f, relativeSpeed));
        stunTimer = Mathf.Max(stunTimer, stunDuration);
        currentSpeed = Mathf.Max(0f, currentSpeed - impactStrength * 1.4f);

        if (col.contactCount > 0)
        {
            Vector3 contactNormal = col.GetContact(0).normal;
            Vector3 recoilDirection = (contactNormal - transform.forward * 0.35f).normalized;
            float recoilImpulse = Mathf.Clamp(col.impulse.magnitude * 0.22f, 0f, rb.mass * 1.2f);
            rb.AddForce(recoilDirection * recoilImpulse, ForceMode.Impulse);
        }

        float multiplier = hitPlayer ? collisionDamageMultiplier : environmentDamageMultiplier;
        ApplyCollisionDamage(impactSeverity, multiplier, col.gameObject.name);
    }

    Transform FindChildTransform(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }
}
