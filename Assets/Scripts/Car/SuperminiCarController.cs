using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Audio;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody))]
public class SuperminiCarController : MonoBehaviour
{
    public enum DriveType
    {
        FrontWheelDrive,
        RearWheelDrive,
        AllWheelDrive
    }

    [Header("WheelColliders")]
    public WheelCollider FL, FR, RL, RR;

    [Header("Wheel Meshes")]
    public Transform FLMesh, FRMesh, RLMesh, RRMesh;

    [Header("Auto Setup")]
    [Tooltip("Auto-resolves wheel mesh refs if they point to empty proxies or are missing.")]
    public bool autoResolveWheelMeshes = true;

    [Header("Lights")]
    public Renderer[] brakeLights;
    public Color brakeColor = Color.red;
    public float brakeEmissionIntensity = 3f;

    [Header("Engine Audio")]
    public AudioClip engineSound;
    public AudioMixerGroup sfxMixerGroup;
    private AudioSource engineSource1;
    private AudioSource engineSource2;
    private bool useSource1 = true;
    private float currentEnginePitch = 1f;
    private float currentEngineVol = 0.5f;
    private const float CROSSFADE_TIME = 0.25f;

    [Header("Effects")]
    public ParticleSystem[] smokeParticles;
    public TrailRenderer[] skidTrails;
    public AudioClip tireSquealSound;
    public float sidewaysSlipThreshold = 0.65f;
    public float forwardSlipThreshold = 0.85f;
    public float skidMarkMinSpeedKmh = 10f;
    public float squealMinSpeedKmh = 35f;
    public float smokeMinSpeedKmh = 75f;
    public float slipEffectThreshold = 0.3f;
    public float skidEffectThreshold = 0.45f;
    public float smokeEffectThreshold = 0.58f;
    private AudioSource tireSquealSource;
    private bool effectsActive = false;
    private float maxSlipIntensity = 0f;

    [Header("NOS (Nitrous Oxide)")]
    public float nosMultiplier = 1.8f;
    public float maxNosFuel = 100f;
    public float nosDrainRate = 20f;
    public float nosRefillRate = 5f;
    public float nosFovBoost = 15f;
    public ParticleSystem[] nosEffects;
    public AudioClip nosAudioClip;
    private float currentNosFuel;
    private bool nosActive = false;
    private AudioSource nosAudioSource;
    private Camera mainCam;
    private float baseFov;
    private UnityEngine.UI.Image nosBarFill;
    private UnityEngine.UI.Image healthBarFill;
    private UnityEngine.UI.Text speedText;
    private float displayedSpeedKmh = 0f;
    private float displayedSpeedVelocity = 0f;

    // Transmission & RPM
    private int currentGear = 1;
    private float currentRPM = 1000f;
    private float maxRPM = 8000f;
    private float idleRPM = 1000f;
    private float[] gearRatios = { 0f, 3.8f, 2.4f, 1.7f, 1.3f, 1.0f, 0.8f }; // index 0 unused, gears 1-6
    private float finalDrive = 3.5f;
    private float upshiftRPM = 7500f;
    private float downshiftRPM = 2500f;
    private UnityEngine.UI.Image rpmBarFill;
    private UnityEngine.UI.Text gearText;
    private float revLimiterFlashTimer = 0f;
    private float escapePulseTimer = 0f;
    private float healPulseTimer = 0f;

    public void TriggerEscapePulse()
    {
        escapePulseTimer = 15f; // Pulse and heal for the full 15 seconds
    }

    public void HealToFull()
    {
        currentHealth = maxHealth;
        isWrecked = false;
        healPulseTimer = 1f; // Flash green for 1 second

        // Stop the engine smoke
        if (smokeParticles != null)
        {
            foreach (var ps in smokeParticles)
            {
                if (ps != null)
                {
                    var em = ps.emission;
                    em.rateOverTime = 60f; // Return to normal tire smoke rate
                    em.enabled = false;
                    ps.Stop();
                }
            }
        }

        // Restore engine audio & tire sounds if they exist
        if (engineSource1 != null)
        {
            engineSource1.volume = 0.5f * GameAudioRouting.GetSfxGainMultiplier(sfxMixerGroup);
            if (!engineSource1.isPlaying) engineSource1.Play();
        }
        if (engineSource2 != null)
        {
            engineSource2.volume = 0.5f * GameAudioRouting.GetSfxGainMultiplier(sfxMixerGroup);
            if (!engineSource2.isPlaying) engineSource2.Play();
        }
        if (tireSquealSource != null)
        {
            if (!tireSquealSource.isPlaying) tireSquealSource.Play();
            tireSquealSource.volume = 0f;
        }

        // Hide game over UI
        if (gameOverUI != null) gameOverUI.SetActive(false);

        UpdateDamageVisuals(true);

        Debug.Log("[SUPERMINI] Car Repaired!");
    }

    [Header("Engine")]
    public DriveType driveType = DriveType.RearWheelDrive;
    public float maxEngineTorque = 1430f;
    public float reverseTorque = 1400f;
    public float maxSpeedKmh = 275f;
    public float maxReverseSpeedKmh = 70f;
    public float stopToReverseSpeedKmh = 6f;
    public AnimationCurve torqueBySpeed = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f);

    [Header("Brakes")]
    public float maxBrakeTorque = 5400f;
    public float handbrakeTorque = 8400f;

    [Header("Steering")]
    public float maxSteerAngle = 32f;
    public float steerSmoothing = 12f;
    public AnimationCurve steerBySpeed = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.35f);

    [Header("Stability")]
    public float minMass = 1600f;
    public float downforce = 120f;
    public float antiRoll = 6500f;
    public Transform centerOfMass;

    Rigidbody rb;
    float currentSteer;

    // Raw inputs cached from Update → FixedUpdate
    float h, v;
    bool handbrakePressed;
    private float originalSidewaysStiffness = 1f;
    private float originalForwardStiffness = 1f;
    private float originalFrontSidewaysStiffness = 1f;
    private float originalAngularDamping = 0.05f;
    private float currentDriftStiffness = 1f;

    [Header("Health & Damage")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float crashDamageThreshold = 5f; // min impact speed to take damage
    public float minImpactForce = 2f;
    public float damageMultiplier = 0.5f;
    public float environmentDamageMultiplier = 0.6f;
    public float damageRepeatCooldown = 0.2f;
    public bool isWrecked = false;
    public AudioClip heavyCrashSound;
    private float lastDamageTime = -10f;
    private VehicleDamageVisuals damageVisuals;
    private float lastDamageVisualHealth = -1f;

    // Anti-Glue timer
    private float slipTimer = 0f;

    // Game Over UI
    private GameObject gameOverUI;

    // New Input System actions
    InputAction moveAction;
    InputAction brakeAction;
    InputAction nosAction;

    void OnEnable()
    {
        Debug.Log("[SuperminiCarController] OnEnable called — setting up InputActions");

        if (moveAction == null)
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d")
                .With("Right", "<Keyboard>/rightArrow");
        }

        if (brakeAction == null)
        {
            brakeAction = new InputAction("Brake", InputActionType.Button, "<Keyboard>/space");
        }

        if (nosAction == null)
        {
            nosAction = new InputAction("NOS", InputActionType.Button, "<Keyboard>/leftShift");
        }

        moveAction.Enable();
        brakeAction.Enable();
        nosAction.Enable();

        // NOS fuel init
        currentNosFuel = maxNosFuel;

        Debug.Log("[SuperminiCarController] InputActions enabled (incl NOS)");
    }

    void OnDisable()
    {
        moveAction?.Disable();
        brakeAction?.Disable();
        nosAction?.Disable();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb.mass < minMass) rb.mass = minMass;

        // Anti-Tunneling: continuous collision so the car never clips through the floor
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Smoother visual interpolation for physics-driven vehicles
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Mass & drag normalization for stability without making the car feel overly heavy.
        rb.mass = Mathf.Clamp(rb.mass, minMass, Mathf.Max(minMass, 1700f));
        rb.linearDamping = Mathf.Clamp(rb.linearDamping, 0.02f, 0.08f);
        rb.angularDamping = Mathf.Max(rb.angularDamping, 6f);

        // Auto-discover WheelColliders by child name if references are null
        if (FL == null) FL = FindWheelCollider("WC_FL");
        if (FR == null) FR = FindWheelCollider("WC_FR");
        if (RL == null) RL = FindWheelCollider("WC_RL");
        if (RR == null) RR = FindWheelCollider("WC_RR");

        // Auto-discover wheel meshes by child name if references are null
        if (FLMesh == null) FLMesh = FindChildTransform("Wheel_FL");
        if (FRMesh == null) FRMesh = FindChildTransform("Wheel_FR");
        if (RLMesh == null) RLMesh = FindChildTransform("Wheel_RL");
        if (RRMesh == null) RRMesh = FindChildTransform("Wheel_RR");

        if (autoResolveWheelMeshes)
        {
            ResolveVisibleWheelMeshes();
        }

        // Anti-Flip: lower center of mass below the axles
        if (centerOfMass == null) centerOfMass = FindChildTransform("CenterOfMass");

        ApplyGameplayTuning();

        // Center of Mass Re-Validation: Lock it deep
        rb.centerOfMass = new Vector3(0f, -1.0f, 0f);

        ApplyGripPreset(FL); ApplyGripPreset(FR);
        ApplyGripPreset(RL); ApplyGripPreset(RR);

        // Log wheel assignments
        Debug.Log($"[SuperminiCarController] Awake — FL:{FL != null} FR:{FR != null} RL:{RL != null} RR:{RR != null}");
        Debug.Log($"[SuperminiCarController] FLMesh:{FLMesh != null} FRMesh:{FRMesh != null} RLMesh:{RLMesh != null} RRMesh:{RRMesh != null}");

        // Ensure continuous collision to prevent tunneling at high speeds
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        Debug.Log($"[SuperminiCarController] Mass:{rb.mass} CoM:{rb.centerOfMass} AngDrag:{rb.angularDamping} Collision:{rb.collisionDetectionMode}");

        // =============================================
        // FORCE NOS INITIALIZATION ON AWAKE
        // =============================================
        mainCam = Camera.main;
        if (mainCam != null) baseFov = mainCam.fieldOfView;

        bool nosNeedsGen = (nosEffects == null || nosEffects.Length == 0);
        if (!nosNeedsGen && nosEffects.Length > 0)
        {
            // Check if slots are filled but contain null refs
            nosNeedsGen = true;
            foreach (var fx in nosEffects)
                if (fx != null) { nosNeedsGen = false; break; }
        }
        if (nosNeedsGen)
            AutoGenerateNosParticles();

        if (nosAudioClip == null)
            GenerateProceduralNosAudio();

        // =============================================
        // ANTI-STICKY & STABILITY REPAIRS
        // =============================================
        rb.centerOfMass = new Vector3(0f, -1.0f, 0f);

        PhysicsMaterial antiSticky = RuntimeSharedResources.GetSuperminiAntiStickyMaterial();

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            if (!col.isTrigger && !(col is WheelCollider)) col.sharedMaterial = antiSticky;
        }

        if (nosAudioClip != null)
        {
            nosAudioSource = gameObject.AddComponent<AudioSource>();
            nosAudioSource.clip = nosAudioClip;
            nosAudioSource.loop = true;
            nosAudioSource.spatialBlend = 1f;
            nosAudioSource.volume = 0f;
            nosAudioSource.dopplerLevel = 0f;
            GameAudioRouting.ConfigureSfxSource(nosAudioSource, sfxMixerGroup, 176);
            nosAudioSource.Play();
        }

        if (RL != null)
        {
            originalSidewaysStiffness = RL.sidewaysFriction.stiffness;
            originalForwardStiffness = RL.forwardFriction.stiffness;
        }
        if (FL != null) originalFrontSidewaysStiffness = FL.sidewaysFriction.stiffness;
        if (rb != null) originalAngularDamping = rb.angularDamping;

        BuildNosBarUI();
        Debug.Log("NOS INITIALIZED ON: " + gameObject.name);
        // =============================================

        if (FL == null || FR == null || RL == null || RR == null)
        {
            Debug.LogError("[SuperminiCarController] WheelColliders still null after auto-discovery! Listing children:");
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var wc = child.GetComponent<WheelCollider>();
                Debug.LogError($"  Child[{i}]: '{child.name}' hasWheelCollider:{wc != null}");
            }
        }

        // Setup AudioSource for tire squeals
        if (tireSquealSound != null)
        {
            tireSquealSource = gameObject.AddComponent<AudioSource>();
            tireSquealSource.clip = tireSquealSound;
            tireSquealSource.loop = true;
            tireSquealSource.volume = 0f;
            tireSquealSource.spatialBlend = 1f; // 3D sound
            tireSquealSource.dopplerLevel = 0f;
            GameAudioRouting.ConfigureSfxSource(tireSquealSource, sfxMixerGroup, 192);
            tireSquealSource.Play();
        }

        // Setup AudioSource for engine (Dual setup for seamless crossfading)
        if (engineSound != null)
        {
            engineSource1 = gameObject.AddComponent<AudioSource>();
            engineSource1.clip = engineSound;
            engineSource1.loop = false; // We handle loop manually
            engineSource1.spatialBlend = 1f;
            engineSource1.dopplerLevel = 0f;
            GameAudioRouting.ConfigureSfxSource(engineSource1, sfxMixerGroup, 152);
            engineSource1.Play();

            engineSource2 = gameObject.AddComponent<AudioSource>();
            engineSource2.clip = engineSound;
            engineSource2.loop = false;
            engineSource2.spatialBlend = 1f;
            engineSource2.dopplerLevel = 0f;
            GameAudioRouting.ConfigureSfxSource(engineSource2, sfxMixerGroup, 152);
        }

        // Initialize Brake Lights
        if (brakeLights != null)
        {
            foreach (var r in brakeLights)
            {
                if (r == null) continue;
                // We must enable the EMISSION keyword for the material to glow
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", Color.black);
            }
        }

        InitializeDamageVisuals();
    }

    void ApplyGameplayTuning()
    {
        sidewaysSlipThreshold = Mathf.Max(sidewaysSlipThreshold, 0.75f);
        forwardSlipThreshold = Mathf.Max(forwardSlipThreshold, 0.9f);
        skidMarkMinSpeedKmh = Mathf.Max(skidMarkMinSpeedKmh, 55f);
        squealMinSpeedKmh = Mathf.Max(squealMinSpeedKmh, 38f);
        smokeMinSpeedKmh = Mathf.Max(smokeMinSpeedKmh, 85f);
        slipEffectThreshold = Mathf.Clamp(slipEffectThreshold, 0.32f, 0.7f);
        skidEffectThreshold = Mathf.Clamp(skidEffectThreshold, 0.48f, 0.8f);
        smokeEffectThreshold = Mathf.Clamp(smokeEffectThreshold, 0.62f, 0.9f);

        maxEngineTorque = Mathf.Max(maxEngineTorque, 3000f);
        reverseTorque = Mathf.Max(reverseTorque, 1700f);
        maxSpeedKmh = Mathf.Max(maxSpeedKmh, 250f);

        minImpactForce = Mathf.Max(minImpactForce, 4f);
        crashDamageThreshold = Mathf.Max(crashDamageThreshold, 8f);
        damageMultiplier = Mathf.Max(damageMultiplier, 0.5f);
        environmentDamageMultiplier = Mathf.Max(environmentDamageMultiplier, 0.6f);
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

    void ResolveVisibleWheelMeshes()
    {
        var usedMeshes = new HashSet<Transform>();

        FLMesh = ResolveWheelMeshReference(FLMesh, usedMeshes, "wheel_fl", "tire_fl", "trire_fl", "tyre_fl");
        FRMesh = ResolveWheelMeshReference(FRMesh, usedMeshes, "wheel_fr", "tire_fr", "tyre_fr");
        RLMesh = ResolveWheelMeshReference(RLMesh, usedMeshes, "wheel_rl", "tire_rl", "tyre_rl");
        RRMesh = ResolveWheelMeshReference(RRMesh, usedMeshes, "wheel_rr", "tire_rr", "tyre_rr");
    }

    Transform ResolveWheelMeshReference(Transform current, HashSet<Transform> used, params string[] tokens)
    {
        if (HasAnyRenderer(current))
        {
            used.Add(current);
            return current;
        }

        var found = FindVisibleMeshByToken(used, tokens);
        return found != null ? found : current;
    }

    Transform FindVisibleMeshByToken(HashSet<Transform> used, params string[] tokens)
    {
        var all = GetComponentsInChildren<Transform>(true);
        foreach (string token in tokens)
        {
            string tokenLower = token.ToLowerInvariant();
            foreach (var t in all)
            {
                if (t == null || used.Contains(t)) continue;
                if (!HasAnyRenderer(t)) continue;
                if (!t.name.ToLowerInvariant().Contains(tokenLower)) continue;

                used.Add(t);
                return t;
            }
        }

        return null;
    }

    static bool HasAnyRenderer(Transform t)
    {
        return t != null && t.GetComponentInChildren<Renderer>(true) != null;
    }

    WheelCollider FindWheelCollider(string childName)
    {
        var t = transform.Find(childName);
        if (t != null)
        {
            var wc = t.GetComponent<WheelCollider>();
            if (wc != null)
            {
                Debug.Log($"[SuperminiCarController] Auto-discovered WheelCollider: {childName}");
                return wc;
            }
        }
        return null;
    }

    Transform FindChildTransform(string childName)
    {
        var t = transform.Find(childName);
        if (t != null)
        {
            Debug.Log($"[SuperminiCarController] Auto-discovered transform: {childName}");
        }
        return t;
    }

    void Update()
    {
        if (isWrecked) return;

        // Manual Health Debugger
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            currentHealth -= 10f;
            currentHealth -= 10f;
        }

        // =============================================
        // STEALTH REPAIR BONUS
        // =============================================
        if (escapePulseTimer > 0f)
        {
            if (currentHealth < maxHealth)
            {
                currentHealth += Time.deltaTime * 2f; // Passive 2hp per second
                currentHealth = Mathf.Min(currentHealth, maxHealth); // Cap at 100
            }
        }

        UpdateDamageVisuals();

        if (moveAction == null || brakeAction == null)
        {
            Debug.LogWarning("[SuperminiCarController] InputActions are null in Update!");
            return;
        }

        Vector2 move = moveAction.ReadValue<Vector2>();
        h = move.x;
        v = move.y;
        handbrakePressed = brakeAction.IsPressed();

        // NOS input + fuel management (Hardcoded fallback)
        bool nosKeyPressed = Input.GetKey(KeyCode.LeftShift);
        // Require minimum 5 fuel to re-activate (prevents 0-fuel oscillation)
        if (nosKeyPressed && currentNosFuel > 5f)
            nosActive = true;
        else if (!nosKeyPressed || currentNosFuel <= 0f)
            nosActive = false;

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            // Silenced activation log
        }
        if (nosKeyPressed)
        {
            // Silenced fuel log
        }

        if (nosActive)
        {
            currentNosFuel -= nosDrainRate * Time.deltaTime;
            currentNosFuel = Mathf.Max(0f, currentNosFuel);
        }
        else
        {
            currentNosFuel += nosRefillRate * Time.deltaTime;
            currentNosFuel = Mathf.Min(currentNosFuel, maxNosFuel);
        }

        UpdateNosEffects();
        UpdateNosUI();
        UpdateTransmission();

        // Silenced input log
        if ((h != 0f || v != 0f || handbrakePressed) && Time.frameCount % 60 == 0)
        {
        }

        // Smoothly update engine sound in Update rather than FixedUpdate to prevent audio ticks
        if (rb != null && engineSound != null)
        {
            float speedKmh = rb.linearVelocity.magnitude * 3.6f;
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            UpdateEngineSound(speedKmh, Mathf.Abs(v), forwardSpeed < -0.5f);
        }
    }

    void FixedUpdate()
    {
        if (isWrecked) return;
        if (rb == null) return;

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;
        float speed01 = Mathf.Clamp01(speedKmh / Mathf.Max(1f, maxSpeedKmh));
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        // Silenced physics log
        if (FL != null && (h != 0 || v != 0) && Time.frameCount % 60 == 0)
        {
            float rpm = FL.rpm;
            bool hit = FL.isGrounded;
        }

        // ---- Steering ----
        float targetSteer = h * Mathf.Lerp(35f, 5f, speed01);
        currentSteer = Mathf.Lerp(currentSteer, targetSteer,
                       1f - Mathf.Exp(-steerSmoothing * Time.fixedDeltaTime));
        if (FL) FL.steerAngle = currentSteer;
        if (FR) FR.steerAngle = currentSteer;

        // ---- Drive / Brake / Reverse ----
        float motor = 0f;
        float brake = 0f;

        if (v > 0.01f)
        {
            motor = maxEngineTorque * v * torqueBySpeed.Evaluate(speed01);
            // NOS torque boost
            if (nosActive) motor *= nosMultiplier;
            float effectiveMaxSpeed = nosActive ? maxSpeedKmh * nosMultiplier : maxSpeedKmh;
            if (speedKmh >= effectiveMaxSpeed) motor = 0f;
        }
        else if (v < -0.01f)
        {
            if (forwardSpeed > 0.5f && speedKmh > stopToReverseSpeedKmh)
            {
                brake = maxBrakeTorque;
            }
            else
            {
                if (speedKmh < maxReverseSpeedKmh)
                    motor = reverseTorque * v; // v is negative → reverse
            }
        }

        float hbForce = handbrakePressed ? 5000f : 0f;

        // ---- Drift Friction + Stability Logic ----
        // 1. Friction Drop (Raised from 0.15 to 0.35 for stability)
        float targetSideways = handbrakePressed ? 0.35f : originalSidewaysStiffness;
        float targetForward = handbrakePressed ? 0.5f : originalForwardStiffness;
        float targetFrontStiff = handbrakePressed ? 1.5f : originalFrontSidewaysStiffness;

        // Anti-Glue Lateral Slide logic
        if (slipTimer > 0f)
        {
            slipTimer -= Time.fixedDeltaTime;
            targetSideways = 0.2f;
        }

        currentDriftStiffness = Mathf.Lerp(currentDriftStiffness, targetSideways, Time.fixedDeltaTime * 10f);

        // Apply rear friction
        if (RL != null)
        {
            WheelFrictionCurve sf = RL.sidewaysFriction; sf.stiffness = currentDriftStiffness; RL.sidewaysFriction = sf;
            WheelFrictionCurve ff = RL.forwardFriction; ff.stiffness = handbrakePressed ? targetForward : originalForwardStiffness; RL.forwardFriction = ff;
        }
        if (RR != null)
        {
            WheelFrictionCurve sf = RR.sidewaysFriction; sf.stiffness = currentDriftStiffness; RR.sidewaysFriction = sf;
            WheelFrictionCurve ff = RR.forwardFriction; ff.stiffness = handbrakePressed ? targetForward : originalForwardStiffness; RR.forwardFriction = ff;
        }
        // Normal front grip
        if (FL != null) { WheelFrictionCurve sf = FL.sidewaysFriction; sf.stiffness = targetFrontStiff; FL.sidewaysFriction = sf; }
        if (FR != null) { WheelFrictionCurve sf = FR.sidewaysFriction; sf.stiffness = targetFrontStiff; FR.sidewaysFriction = sf; }

        // 2. Angular Dampening "Stabilizer"
        if (rb != null)
        {
            rb.angularDamping = handbrakePressed ? 2.0f : originalAngularDamping;
        }

        ApplyDriveTorque(motor);

        // Brakes all wheels
        if (FL) FL.brakeTorque = brake;
        if (FR) FR.brakeTorque = brake;

        // Handbrake only locks rear wheels
        if (RL)
        {
            RL.brakeTorque = Mathf.Max(brake, hbForce);
            if (handbrakePressed) RL.motorTorque = 0f;
        }
        if (RR)
        {
            RR.brakeTorque = Mathf.Max(brake, hbForce);
            if (handbrakePressed) RR.motorTorque = 0f;
        }

        // Downforce + anti-roll
        rb.AddForce(-transform.up * downforce * rb.linearVelocity.sqrMagnitude);
        if (antiRoll > 0f) { DoAntiRoll(FL, FR, antiRoll); DoAntiRoll(RL, RR, antiRoll); }

        // Wheel visuals
        SpinWheel(FL, FLMesh);
        SpinWheel(FR, FRMesh);
        SpinWheel(RL, RLMesh);
        SpinWheel(RR, RRMesh);

        CheckTireSlipForEffects();
        UpdateBrakeLights(brake > 0f);
    }

    void UpdateEngineSound(float speedKmh, float throttleInput, bool isReversing)
    {
        if (engineSource1 == null || engineSource2 == null) return;

        // Map RPM → pitch (0.5 at idle, 2.5 at redline)
        float minPitch = 0.5f;
        float maxPitch = 2.5f;
        float rpmPercent = Mathf.Clamp01(currentRPM / maxRPM);
        float targetPitch = Mathf.Lerp(minPitch, maxPitch, rpmPercent);

        // Volume: louder with throttle and RPM
        float targetVolume = 0.3f + (throttleInput * 0.4f) + (rpmPercent * 0.3f);
        targetVolume = Mathf.Clamp01(targetVolume);

        // Smooth pitch changes for professional gear shift sound
        currentEnginePitch = Mathf.Lerp(currentEnginePitch, targetPitch, Time.deltaTime * 10f);
        currentEngineVol = Mathf.Lerp(currentEngineVol, targetVolume, Time.deltaTime * 5f);

        // Crossfade seamless looping logic
        AudioSource activeSource = useSource1 ? engineSource1 : engineSource2;
        AudioSource fadingSource = useSource1 ? engineSource2 : engineSource1;

        if (activeSource.isPlaying && activeSource.time >= engineSound.length - CROSSFADE_TIME)
        {
            useSource1 = !useSource1;
            fadingSource = activeSource;
            activeSource = useSource1 ? engineSource1 : engineSource2;

            activeSource.time = 0f;
            activeSource.Play();
        }
        else if (!activeSource.isPlaying)
        {
            activeSource.Play();
        }

        // Apply pitch to both
        activeSource.pitch = currentEnginePitch;
        fadingSource.pitch = currentEnginePitch;

        // Apply crossfade volume
        if (activeSource.time < CROSSFADE_TIME)
        {
            float t = activeSource.time / CROSSFADE_TIME;
            float scaledEngineVol = currentEngineVol * GameAudioRouting.GetSfxGainMultiplier(sfxMixerGroup);
            activeSource.volume = Mathf.Lerp(0f, scaledEngineVol, t);
            fadingSource.volume = Mathf.Lerp(scaledEngineVol, 0f, t);
        }
        else
        {
            activeSource.volume = currentEngineVol * GameAudioRouting.GetSfxGainMultiplier(sfxMixerGroup);
            fadingSource.volume = 0f;
            if (fadingSource.isPlaying) fadingSource.Stop();
        }
    }

    void UpdateBrakeLights(bool isBraking)
    {
        if (brakeLights == null) return;

        Color targetColor = isBraking ? (brakeColor * brakeEmissionIntensity) : Color.black;

        foreach (var r in brakeLights)
        {
            if (r == null) continue;
            r.material.SetColor("_EmissionColor", targetColor);
        }
    }

    void ApplyDriveTorque(float motor)
    {
        float frontTorque = 0f;
        float rearTorque = 0f;

        switch (driveType)
        {
            case DriveType.FrontWheelDrive:
                frontTorque = motor * 0.5f;
                break;

            case DriveType.RearWheelDrive:
                rearTorque = motor * 0.5f;
                break;

            case DriveType.AllWheelDrive:
                frontTorque = motor * 0.25f;
                rearTorque = motor * 0.25f;
                break;
        }

        if (FL) FL.motorTorque = frontTorque;
        if (FR) FR.motorTorque = frontTorque;
        if (RL) RL.motorTorque = rearTorque;
        if (RR) RR.motorTorque = rearTorque;
    }

    void SpinWheel(WheelCollider wc, Transform mesh)
    {
        if (!wc || !mesh) return;
        wc.GetWorldPose(out Vector3 pos, out Quaternion rot);

        // We removed the hardcoded Quaternion.Euler(0, 0, 90) offset here.
        // MuscleCarBuilder and SuperminiCarBuilder now handle their own specific visual mesh offsets via child nodes.
        mesh.SetPositionAndRotation(pos, rot);
    }

    static void DoAntiRoll(WheelCollider L, WheelCollider R, float force)
    {
        if (!L || !R) return;
        bool lg = L.GetGroundHit(out WheelHit lh);
        bool rg = R.GetGroundHit(out WheelHit rh);
        float lt = 1f, rt = 1f;
        if (lg) lt = (-L.transform.InverseTransformPoint(lh.point).y - L.radius) / L.suspensionDistance;
        if (rg) rt = (-R.transform.InverseTransformPoint(rh.point).y - R.radius) / R.suspensionDistance;
        float f = (lt - rt) * force;
        if (lg) L.attachedRigidbody.AddForceAtPosition(L.transform.up * -f, L.transform.position);
        if (rg) R.attachedRigidbody.AddForceAtPosition(R.transform.up * f, R.transform.position);
    }

    static void ApplyGripPreset(WheelCollider wc)
    {
        if (!wc) return;
        var fwd = wc.forwardFriction;
        fwd.extremumSlip = 0.32f; fwd.extremumValue = 1.8f;
        fwd.asymptoteSlip = 0.82f; fwd.asymptoteValue = 0.95f;
        wc.forwardFriction = fwd;

        // Keep the car planted, but avoid the overly sticky arcade feel.
        var side = wc.sidewaysFriction;
        side.extremumSlip = 0.22f; side.extremumValue = 1.75f;
        side.asymptoteSlip = 0.58f; side.asymptoteValue = 0.95f;
        wc.sidewaysFriction = side;
    }

    void CheckTireSlipForEffects()
    {
        float speedKmh = rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;

        // Get the worst slip across all wheels
        maxSlipIntensity = 0f;
        maxSlipIntensity = Mathf.Max(maxSlipIntensity, GetSlipIntensity(FL));
        maxSlipIntensity = Mathf.Max(maxSlipIntensity, GetSlipIntensity(FR));
        maxSlipIntensity = Mathf.Max(maxSlipIntensity, GetSlipIntensity(RL));
        maxSlipIntensity = Mathf.Max(maxSlipIntensity, GetSlipIntensity(RR));

        bool allowSqueal = maxSlipIntensity > slipEffectThreshold && speedKmh > squealMinSpeedKmh;
        bool allowSkid = maxSlipIntensity > skidEffectThreshold && speedKmh > skidMarkMinSpeedKmh;
        bool allowSmoke = maxSlipIntensity > smokeEffectThreshold && speedKmh > smokeMinSpeedKmh;
        bool isSlipping = allowSqueal || allowSkid || allowSmoke;

        float squealStrength = allowSqueal ? Mathf.InverseLerp(slipEffectThreshold, 1f, maxSlipIntensity) : 0f;
        float skidStrength = allowSkid ? Mathf.InverseLerp(skidEffectThreshold, 1f, maxSlipIntensity) : 0f;
        float smokeStrength = allowSmoke ? Mathf.InverseLerp(smokeEffectThreshold, 1f, maxSlipIntensity) : 0f;

        // ---- Tire Squeal: volume proportional to intensity ----
        if (tireSquealSource != null)
        {
            if (allowSqueal)
            {
                float speedFactor = Mathf.InverseLerp(squealMinSpeedKmh, 160f, speedKmh);
                float targetVol = Mathf.Lerp(0.03f, 0.2f, squealStrength * speedFactor) * GameAudioRouting.GetSfxGainMultiplier(sfxMixerGroup);
                tireSquealSource.volume = Mathf.Lerp(tireSquealSource.volume, targetVol, Time.deltaTime * 10f);
                tireSquealSource.pitch = 0.92f + (squealStrength * 0.15f);
            }
            else
            {
                tireSquealSource.volume = Mathf.Lerp(tireSquealSource.volume, 0f, Time.deltaTime * 10f);
            }
        }

        // ---- Smoke: proportional and reduced max rate ----
        if (smokeParticles != null)
        {
            foreach (var ps in smokeParticles)
            {
                if (ps == null) continue;
                var emission = ps.emission;
                if (allowSmoke)
                {
                    emission.enabled = true;
                    float speedFactor = Mathf.InverseLerp(smokeMinSpeedKmh, 170f, speedKmh);
                    emission.rateOverTime = Mathf.Lerp(4f, 18f, smokeStrength * speedFactor);
                }
                else
                {
                    emission.enabled = false;
                    emission.rateOverTime = 0f;
                }
            }
        }

        // ---- Skid Marks: higher speed and intensity requirement ----
        if (skidTrails != null)
        {
            bool shouldSkid = allowSkid && skidStrength > 0.1f;
            foreach (var tr in skidTrails)
            {
                if (tr == null) continue;
                tr.emitting = shouldSkid;
            }
        }

        effectsActive = isSlipping;
    }

    /// <summary>
    /// Returns 0 if below threshold, or a 0-1 intensity value above threshold.
    /// </summary>
    float GetSlipIntensity(WheelCollider wc)
    {
        if (wc == null || !wc.isGrounded) return 0f;

        WheelHit hit;
        wc.GetGroundHit(out hit);

        float sideSlip = Mathf.Abs(hit.sidewaysSlip);
        float fwdSlip = Mathf.Abs(hit.forwardSlip);

        float sideIntensity = 0f;
        float fwdIntensity = 0f;

        if (sideSlip > sidewaysSlipThreshold)
            sideIntensity = (sideSlip - sidewaysSlipThreshold) / (1f - sidewaysSlipThreshold);
        if (fwdSlip > forwardSlipThreshold)
            fwdIntensity = (fwdSlip - forwardSlipThreshold) / (1f - forwardSlipThreshold);

        return Mathf.Clamp01(Mathf.Max(sideIntensity, fwdIntensity));
    }

    float MathsAbs(float val) => val < 0 ? -val : val;

    // Legacy EnableEffects kept for editor auto-setup compatibility
    void EnableEffects(bool enable)
    {
        if (smokeParticles != null)
        {
            foreach (var ps in smokeParticles)
            {
                if (ps == null) continue;
                var emission = ps.emission;
                emission.enabled = enable;
            }
        }

        if (skidTrails != null)
        {
            foreach (var tr in skidTrails)
            {
                if (tr == null) continue;
                tr.emitting = enable;
            }
        }
    }

    // =============================================
    // NOS: Visual & Audio Effects
    // =============================================
    void UpdateNosEffects()
    {
        // Camera FOV shift
        if (mainCam != null)
        {
            float targetFov = nosActive ? baseFov + nosFovBoost : baseFov;
            mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFov, Time.deltaTime * 6f);
        }

        // Exhaust particles
        if (nosEffects != null)
        {
            foreach (var ps in nosEffects)
            {
                if (ps == null) continue;
                if (nosActive && !ps.isPlaying) ps.Play();
                else if (!nosActive && ps.isPlaying) ps.Stop();
            }
        }

        // NOS audio volume
        if (nosAudioSource != null)
        {
            float targetVol = nosActive ? Mathf.Clamp01(currentNosFuel / maxNosFuel) * 0.6f * GameAudioRouting.GetSfxGainMultiplier(sfxMixerGroup) : 0f;
            nosAudioSource.volume = Mathf.Lerp(nosAudioSource.volume, targetVol, Time.deltaTime * 10f);
        }
    }

    void UpdateNosUI()
    {
        if (nosBarFill != null)
        {
            float t = currentNosFuel / maxNosFuel;

            // Scale the fill bar width based on fuel remaining
            RectTransform fillRect = nosBarFill.GetComponent<RectTransform>();
            if (fillRect != null)
                fillRect.anchorMax = new Vector2(t, 1f);

            // Red warning when fuel drops below 20%
            nosBarFill.color = (t <= 0.2f)
                ? new Color(1f, 0.15f, 0.15f, 1f)
                : new Color(0.2f, 0.5f, 1f, 1f);
        }

        // ---- Update Health Bar ----
        if (healthBarFill != null)
        {
            float hpPercent = Mathf.Clamp01(currentHealth / maxHealth);
            float currentFill = healthBarFill.rectTransform.anchorMax.x;
            float newFill = Mathf.Lerp(currentFill, hpPercent, Time.deltaTime * 5f);
            healthBarFill.rectTransform.anchorMax = new Vector2(newFill, 1f); // Smooth Sync

            if (healPulseTimer > 0f)
            {
                healPulseTimer -= Time.deltaTime;
                float pulse = Mathf.PingPong(Time.time * 10f, 1f);
                healthBarFill.color = Color.Lerp(Color.green, Color.white, pulse);
            }
            else if (hpPercent < 0.25f && hpPercent > 0f)
            {
                // Panic Flash
                float pulse = Mathf.PingPong(Time.time * 8f, 1f);
                healthBarFill.color = Color.Lerp(new Color(0.8f, 0.1f, 0.1f, 1f), Color.white, pulse);
            }
            else
            {
                healthBarFill.color = new Color(0.8f, 0.1f, 0.1f, 1f);
            }
        }

        // Speedometer
        if (speedText != null && rb != null)
        {
            float speedKmh = Mathf.Abs(rb.linearVelocity.magnitude * 3.6f);
            displayedSpeedKmh = Mathf.SmoothDamp(displayedSpeedKmh, speedKmh, ref displayedSpeedVelocity, 0.18f);
            speedText.text = Mathf.RoundToInt(displayedSpeedKmh).ToString("000");

            if (escapePulseTimer > 0f)
            {
                escapePulseTimer -= Time.deltaTime;
                // Pulse white and gold to signal ESCAPED
                float pulse = Mathf.PingPong(Time.time * 5f, 1f);
                speedText.color = Color.Lerp(Color.white, new Color(1f, 0.8f, 0f, 1f), pulse);
            }
            else
            {
                // Electric blue during NOS, white otherwise
                speedText.color = nosActive ? new Color(0.2f, 0.5f, 1f, 1f) : Color.white;
            }
        }
    }

    void UpdateTransmission()
    {
        if (rb == null) return;

        float speedMs = Mathf.Abs(rb.linearVelocity.magnitude);
        float wheelRadius = 0.35f;
        float forwardDot = Vector3.Dot(transform.forward, rb.linearVelocity);
        bool isReversing = (forwardDot < -0.5f) || (v < -0.1f && speedMs < 2f);

        string gearDisplay;

        if (isReversing)
        {
            // Reverse gear: simulate 1000-4000 RPM range
            float reverseSpeedRatio = Mathf.Clamp01(speedMs / (maxReverseSpeedKmh / 3.6f));
            currentRPM = Mathf.Lerp(idleRPM, 4000f, reverseSpeedRatio);
            currentGear = 1; // Reset for when we go forward again
            gearDisplay = "R";
        }
        else
        {
            // Forward gears
            if (currentGear >= 1 && currentGear <= 6)
            {
                float wheelRPM = (speedMs / (2f * Mathf.PI * wheelRadius)) * 60f;
                currentRPM = wheelRPM * gearRatios[currentGear] * finalDrive;
                currentRPM = Mathf.Clamp(currentRPM, idleRPM, maxRPM);
            }

            // Throttle response at low speed
            if (v > 0.1f && speedMs < 2f)
                currentRPM = Mathf.Lerp(currentRPM, idleRPM + (maxRPM - idleRPM) * v, Time.deltaTime * 5f);

            // Auto-shift logic
            if (currentRPM >= upshiftRPM && currentGear < 6)
            {
                currentGear++;
                currentRPM = Mathf.Max(idleRPM, currentRPM * gearRatios[currentGear] / gearRatios[currentGear - 1]);
            }
            else if (currentRPM <= downshiftRPM && currentGear > 1 && speedMs > 1f)
            {
                currentGear--;
                currentRPM = Mathf.Min(maxRPM, currentRPM * gearRatios[currentGear] / gearRatios[currentGear + 1]);
            }

            // Reset to gear 1 when nearly stopped
            if (speedMs < 1f) currentGear = 1;

            gearDisplay = currentGear.ToString();
        }

        // ---- Update RPM Bar ----
        if (rpmBarFill != null)
        {
            float rpmNorm = currentRPM / maxRPM;
            RectTransform rpmFillRect = rpmBarFill.GetComponent<RectTransform>();
            if (rpmFillRect != null)
                rpmFillRect.anchorMax = new Vector2(rpmNorm, 1f);

            // Redline color (forward only)
            if (!isReversing && currentRPM > 7000f)
            {
                float redT = (currentRPM - 7000f) / 1000f;
                rpmBarFill.color = Color.Lerp(Color.white, Color.red, redT);
            }
            else
            {
                rpmBarFill.color = isReversing ? new Color(0.6f, 0.8f, 1f) : Color.white;
            }
        }

        // ---- Update Gear Text ----
        if (gearText != null)
        {
            gearText.text = gearDisplay;
            if (isReversing)
                gearText.color = new Color(0.3f, 0.7f, 1f); // Light blue for reverse
            else
                gearText.color = (currentRPM > 7000f) ? Color.red : Color.white;
        }

        // ---- Rev Limiter Flash (8000 RPM, forward only) ----
        if (!isReversing && currentRPM >= maxRPM && speedText != null)
        {
            revLimiterFlashTimer += Time.deltaTime * 15f;
            bool flashOn = (Mathf.Sin(revLimiterFlashTimer) > 0f);
            speedText.color = flashOn ? Color.red : new Color(0.3f, 0f, 0f, 1f);
        }
        else
        {
            revLimiterFlashTimer = 0f;
        }
    }

    void AutoGenerateNosParticles()
    {
        // STEP 1: Scan hierarchy for exhaust/pipe/muffler/tube geometry
        string[] exhaustNames = { "exhaust", "pipe", "muffler", "tube", "tip", "tail" };
        List<Transform> exhaustPoints = new List<Transform>();

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            string lowerName = child.name.ToLowerInvariant();
            foreach (string token in exhaustNames)
            {
                if (lowerName.Contains(token))
                {
                    exhaustPoints.Add(child);
                    break;
                }
            }
        }

        if (exhaustPoints.Count >= 2)
        {
            // Use the first two exhaust points found
            ParticleSystem ps1 = CreateNosParticle("NOS_Exhaust_1", exhaustPoints[0]);
            ParticleSystem ps2 = CreateNosParticle("NOS_Exhaust_2", exhaustPoints[1]);
            nosEffects = new ParticleSystem[] { ps1, ps2 };
            Debug.Log($"[NOS] Snapped to exhaust geometry: '{exhaustPoints[0].name}' and '{exhaustPoints[1].name}'");
        }
        else if (exhaustPoints.Count == 1)
        {
            // Single exhaust — create two particles offset slightly
            ParticleSystem ps1 = CreateNosParticle("NOS_Exhaust_1", exhaustPoints[0]);
            // Create a second one offset to the side
            ParticleSystem ps2 = CreateNosParticle("NOS_Exhaust_2", exhaustPoints[0]);
            ps2.transform.localPosition += new Vector3(0.3f, 0f, 0f);
            nosEffects = new ParticleSystem[] { ps1, ps2 };
            Debug.Log($"[NOS] Snapped to single exhaust: '{exhaustPoints[0].name}' (split into two)");
        }
        else
        {
            // Fallback: rear wheel positions
            Vector3 rlPos = RLMesh != null ? RLMesh.localPosition : new Vector3(-0.6f, 0.1f, -1.5f);
            Vector3 rrPos = RRMesh != null ? RRMesh.localPosition : new Vector3(0.6f, 0.1f, -1.5f);
            Vector3 offsetRL = rlPos + new Vector3(0.15f, -0.1f, -0.4f);
            Vector3 offsetRR = rrPos + new Vector3(-0.15f, -0.1f, -0.4f);

            ParticleSystem psRL = CreateNosParticleAtPos("NOS_Exhaust_RL", offsetRL);
            ParticleSystem psRR = CreateNosParticleAtPos("NOS_Exhaust_RR", offsetRR);
            nosEffects = new ParticleSystem[] { psRL, psRR };
            Debug.Log("[NOS] No exhaust geometry found — placed at rear wheels.");
        }
    }

    /// <summary>Snap particle to an existing exhaust Transform.</summary>
    ParticleSystem CreateNosParticle(string objName, Transform snapTo)
    {
        GameObject go = new GameObject(objName);
        go.layer = 0;
        go.transform.SetParent(snapTo, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one; // Scale safety
        go.transform.localRotation = Quaternion.LookRotation(-snapTo.forward, snapTo.up);
        ConfigureNosParticle(go);
        return go.GetComponent<ParticleSystem>();
    }

    /// <summary>Place particle at a local position relative to the car.</summary>
    ParticleSystem CreateNosParticleAtPos(string objName, Vector3 localPos)
    {
        GameObject go = new GameObject(objName);
        go.layer = 0;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one; // Scale safety
        go.transform.localRotation = Quaternion.LookRotation(-transform.forward, transform.up);
        ConfigureNosParticle(go);
        return go.GetComponent<ParticleSystem>();
    }

    void ConfigureNosParticle(GameObject go)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        // Main module — doubled size for visibility
        var main = ps.main;
        main.startLifetime = 0.3f;
        main.startSpeed = 20f;
        main.startSize = 0.8f; // Massive jet blast
        main.startColor = new Color(0f, 0.7f, 1f, 1f); // Solid bright blue
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.loop = true;

        var emission = ps.emission;
        emission.rateOverTime = 80f;
        emission.enabled = false;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.05f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.3f, 0.5f, 1f), 0f),
                new GradientColorKey(new Color(0.8f, 0.9f, 1f), 0.5f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.5f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Material mat = RuntimeSharedResources.GetTintedSpriteMaterial(new Color(0f, 0.7f, 1f, 1f));
        if (mat != null)
        {
            renderer.sharedMaterial = mat;
        }
        renderer.sortingOrder = 500;


    }

    void GenerateProceduralNosAudio()
    {
        // Generate a filtered white noise "jet roar" clip
        int sampleRate = 44100;
        float duration = 2f; // 2-second loop
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // Seed for repeatable noise
        System.Random rng = new System.Random(42);

        // Generate filtered noise with low-frequency emphasis (jet/whoosh)
        float prevSample = 0f;
        float filterCoeff = 0.85f; // Low-pass filter coefficient
        for (int i = 0; i < sampleCount; i++)
        {
            float raw = (float)(rng.NextDouble() * 2.0 - 1.0); // -1 to 1
            // Low-pass filter for deep rumble
            float filtered = prevSample * filterCoeff + raw * (1f - filterCoeff);
            prevSample = filtered;

            // Add subtle oscillation for "roar" character
            float osc = Mathf.Sin(i * 0.02f) * 0.15f;
            samples[i] = Mathf.Clamp(filtered * 0.7f + osc, -1f, 1f);
        }

        // Fade the loop edges for seamless looping
        int fadeLen = sampleRate / 10; // 100ms fade
        for (int i = 0; i < fadeLen; i++)
        {
            float t = (float)i / fadeLen;
            samples[i] *= t;
            samples[sampleCount - 1 - i] *= t;
        }

        nosAudioClip = AudioClip.Create("NOS_ProceduralWhoosh", sampleCount, 1, sampleRate, false);
        nosAudioClip.SetData(samples, 0);
        Debug.Log("[NOS] Procedural jet roar audio ready.");
    }

    void BuildNosBarUI()
    {
        // Only build if we don't already have one
        if (nosBarFill != null) return;

        // Find or create a Canvas
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("NOS_Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        }

        // Background bar
        GameObject bgObj = new GameObject("NOS_Bar_BG");
        bgObj.transform.SetParent(canvas.transform, false);
        UnityEngine.UI.Image bg = bgObj.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(1f, 0f);
        bgRect.anchorMax = new Vector2(1f, 0f);
        bgRect.pivot = new Vector2(1f, 0f);
        bgRect.anchoredPosition = new Vector2(-20f, 20f);
        bgRect.sizeDelta = new Vector2(200f, 20f);

        // Fill bar
        GameObject fillObj = new GameObject("NOS_Bar_Fill");
        fillObj.transform.SetParent(bgObj.transform, false);
        nosBarFill = fillObj.AddComponent<UnityEngine.UI.Image>();
        nosBarFill.color = new Color(0.2f, 0.5f, 1f, 1f);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        // Label
        GameObject labelObj = new GameObject("NOS_Label");
        labelObj.transform.SetParent(bgObj.transform, false);
        UnityEngine.UI.Text label = labelObj.AddComponent<UnityEngine.UI.Text>();
        label.text = "NOS";
        label.fontSize = 12;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        // =============================================
        // HEALTH BAR — thin red bar under NOS
        // =============================================
        GameObject hbBgObj = new GameObject("Health_Bar_BG");
        hbBgObj.transform.SetParent(canvas.transform, false);
        UnityEngine.UI.Image hbBg = hbBgObj.AddComponent<UnityEngine.UI.Image>();
        hbBg.color = new Color(0.15f, 0f, 0f, 0.7f);
        RectTransform hbBgRect = hbBgObj.GetComponent<RectTransform>();
        hbBgRect.anchorMin = new Vector2(1f, 0f);
        hbBgRect.anchorMax = new Vector2(1f, 0f);
        hbBgRect.pivot = new Vector2(1f, 0f);
        hbBgRect.anchoredPosition = new Vector2(-20f, 6f); // Under the NOS Bar
        hbBgRect.sizeDelta = new Vector2(200f, 10f);

        GameObject hbFillObj = new GameObject("Health_Bar_Fill");
        hbFillObj.transform.SetParent(hbBgObj.transform, false);
        healthBarFill = hbFillObj.AddComponent<UnityEngine.UI.Image>();
        healthBarFill.color = new Color(0.8f, 0.1f, 0.1f, 1f); // Warning Red

        // Use anchorMax for scaling (Filled type requires a Sprite to work visually)
        RectTransform hbFillRect = hbFillObj.GetComponent<RectTransform>();
        hbFillRect.anchorMin = Vector2.zero;
        hbFillRect.anchorMax = Vector2.one;
        hbFillRect.offsetMin = new Vector2(1f, 1f);
        hbFillRect.offsetMax = new Vector2(-1f, -1f);

        Debug.Log("[SuperminiCarController] NOS and Health HUD created.");

        // =============================================
        // SPEEDOMETER — positioned above the NOS bar
        // =============================================
        // Speed number
        GameObject speedObj = new GameObject("Speed_Text");
        speedObj.transform.SetParent(canvas.transform, false);
        speedText = speedObj.AddComponent<UnityEngine.UI.Text>();
        speedText.text = "000";
        speedText.fontSize = 36;
        speedText.color = Color.white;
        speedText.alignment = TextAnchor.MiddleRight;
        speedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        speedText.fontStyle = FontStyle.Bold;
        RectTransform speedRect = speedObj.GetComponent<RectTransform>();
        speedRect.anchorMin = new Vector2(1f, 0f);
        speedRect.anchorMax = new Vector2(1f, 0f);
        speedRect.pivot = new Vector2(1f, 0f);
        speedRect.anchoredPosition = new Vector2(-20f, 48f); // Above the NOS bar
        speedRect.sizeDelta = new Vector2(200f, 44f);

        // "KM/H" label
        GameObject kmhObj = new GameObject("Speed_Unit");
        kmhObj.transform.SetParent(canvas.transform, false);
        UnityEngine.UI.Text kmhLabel = kmhObj.AddComponent<UnityEngine.UI.Text>();
        kmhLabel.text = "KM/H";
        kmhLabel.fontSize = 11;
        kmhLabel.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        kmhLabel.alignment = TextAnchor.MiddleRight;
        kmhLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform kmhRect = kmhObj.GetComponent<RectTransform>();
        kmhRect.anchorMin = new Vector2(1f, 0f);
        kmhRect.anchorMax = new Vector2(1f, 0f);
        kmhRect.pivot = new Vector2(1f, 0f);
        kmhRect.anchoredPosition = new Vector2(-20f, 92f); // Above the speed number
        kmhRect.sizeDelta = new Vector2(200f, 18f);

        Debug.Log("[SuperminiCarController] Speedometer HUD created.");

        // =============================================
        // RPM BAR — thin bar above KM/H
        // =============================================
        GameObject rpmBgObj = new GameObject("RPM_Bar_BG");
        rpmBgObj.transform.SetParent(canvas.transform, false);
        UnityEngine.UI.Image rpmBg = rpmBgObj.AddComponent<UnityEngine.UI.Image>();
        rpmBg.color = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        RectTransform rpmBgRect = rpmBgObj.GetComponent<RectTransform>();
        rpmBgRect.anchorMin = new Vector2(1f, 0f);
        rpmBgRect.anchorMax = new Vector2(1f, 0f);
        rpmBgRect.pivot = new Vector2(1f, 0f);
        rpmBgRect.anchoredPosition = new Vector2(-20f, 114f);
        rpmBgRect.sizeDelta = new Vector2(200f, 10f);

        GameObject rpmFillObj = new GameObject("RPM_Bar_Fill");
        rpmFillObj.transform.SetParent(rpmBgObj.transform, false);
        rpmBarFill = rpmFillObj.AddComponent<UnityEngine.UI.Image>();
        rpmBarFill.color = Color.white;
        RectTransform rpmFillRect = rpmFillObj.GetComponent<RectTransform>();
        rpmFillRect.anchorMin = Vector2.zero;
        rpmFillRect.anchorMax = Vector2.one;
        rpmFillRect.offsetMin = new Vector2(1f, 1f);
        rpmFillRect.offsetMax = new Vector2(-1f, -1f);

        // Redline zone marker (last 12.5% of the bar)
        GameObject redlineObj = new GameObject("RPM_Redline");
        redlineObj.transform.SetParent(rpmBgObj.transform, false);
        UnityEngine.UI.Image redline = redlineObj.AddComponent<UnityEngine.UI.Image>();
        redline.color = new Color(1f, 0f, 0f, 0.3f);
        RectTransform redlineRect = redlineObj.GetComponent<RectTransform>();
        redlineRect.anchorMin = new Vector2(0.875f, 0f); // 7000/8000
        redlineRect.anchorMax = Vector2.one;
        redlineRect.offsetMin = Vector2.zero;
        redlineRect.offsetMax = Vector2.zero;

        // Gear indicator
        GameObject gearObj = new GameObject("Gear_Text");
        gearObj.transform.SetParent(canvas.transform, false);
        gearText = gearObj.AddComponent<UnityEngine.UI.Text>();
        gearText.text = "1";
        gearText.fontSize = 22;
        gearText.color = Color.white;
        gearText.alignment = TextAnchor.MiddleCenter;
        gearText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        gearText.fontStyle = FontStyle.Bold;
        RectTransform gearRect = gearObj.GetComponent<RectTransform>();
        gearRect.anchorMin = new Vector2(1f, 0f);
        gearRect.anchorMax = new Vector2(1f, 0f);
        gearRect.pivot = new Vector2(1f, 0f);
        gearRect.anchoredPosition = new Vector2(-225f, 48f); // Left of speed number
        gearRect.sizeDelta = new Vector2(30f, 44f);

        // Gear label
        GameObject gearLabelObj = new GameObject("Gear_Label");
        gearLabelObj.transform.SetParent(canvas.transform, false);
        UnityEngine.UI.Text gearLabel = gearLabelObj.AddComponent<UnityEngine.UI.Text>();
        gearLabel.text = "GEAR";
        gearLabel.fontSize = 9;
        gearLabel.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        gearLabel.alignment = TextAnchor.MiddleCenter;
        gearLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform gearLabelRect = gearLabelObj.GetComponent<RectTransform>();
        gearLabelRect.anchorMin = new Vector2(1f, 0f);
        gearLabelRect.anchorMax = new Vector2(1f, 0f);
        gearLabelRect.pivot = new Vector2(1f, 0f);
        gearLabelRect.anchoredPosition = new Vector2(-225f, 92f);
        gearLabelRect.sizeDelta = new Vector2(30f, 14f);

        // Legacy in-car game-over text removed. GameOverManager owns the final loss overlay.
        gameOverUI = null;

        Debug.Log("[SuperminiCarController] RPM & Gear HUD created.");
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-Setup Visual Effects (Smoke, Trails, Brake Lights)")]
    public void AutoSetupVisualEffects()
    {
        Undo.RecordObject(this, "Auto-Setup Visual Effects");

        // 1. Find Brake Lights
        List<Renderer> foundBrakes = new List<Renderer>();
        var allRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers)
        {
            string n = r.name.ToLowerInvariant();
            if (n.Contains("tail") || n.Contains("brake") || n.Contains("stop") || (n.Contains("rear") && (n.Contains("light") || n.Contains("lamp"))))
            {
                foundBrakes.Add(r);
                Debug.Log($"[SuperminiCarController] Found brake light: {r.name}");
            }
        }
        brakeLights = foundBrakes.ToArray();

        // 2. Setup Smoke and Trails (if wheels exist)
        if (RLMesh != null && RRMesh != null)
        {
            // Clean up old ones if they exist
            if (smokeParticles != null) foreach (var p in smokeParticles) if (p != null) Undo.DestroyObjectImmediate(p.gameObject);
            if (skidTrails != null) foreach (var t in skidTrails) if (t != null) Undo.DestroyObjectImmediate(t.gameObject);

            var smokeRL = CreateProceduralSmoke(transform, "Smoke_RL", RLMesh.localPosition);
            var smokeRR = CreateProceduralSmoke(transform, "Smoke_RR", RRMesh.localPosition);
            smokeParticles = new ParticleSystem[] { smokeRL, smokeRR };

            var trailRL = CreateProceduralSkidTrail(transform, "SkidTrail_RL", RLMesh.localPosition);
            var trailRR = CreateProceduralSkidTrail(transform, "SkidTrail_RR", RRMesh.localPosition);
            skidTrails = new TrailRenderer[] { trailRL, trailRR };
            
            Debug.Log("[SuperminiCarController] Procedural tire smoke and skid trails are ready.");
        }
        else
        {
            Debug.LogWarning("[SuperminiCarController] Cannot generate smoke/trails because RLMesh or RRMesh is not assigned.");
        }

        // 3. Setup Engine Audio
        if (engineSound == null)
        {
            engineSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audios/engine.mp3");
            if (engineSound != null) Debug.Log("[SuperminiCarController] Auto-assigned engine sound.");
        }

        EditorUtility.SetDirty(this);
    }

    private ParticleSystem CreateProceduralSmoke(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Smoke");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos - new Vector3(0, 0.25f, 0); 

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1f; main.loop = true; main.startLifetime = 1.0f; main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startColor = new Color(0.8f, 0.8f, 0.8f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 500;
        
        var emission = ps.emission; emission.enabled = false; emission.rateOverTime = 60f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.2f;

        var col = ps.colorOverLifetime; col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.gray, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        col.color = grad;

        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3f));

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        return ps;
    }

    private TrailRenderer CreateProceduralSkidTrail(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Skid Trail");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos - new Vector3(0, 0.38f, 0); 

        var tr = go.AddComponent<TrailRenderer>();
        tr.emitting = false; tr.time = 3f; tr.minVertexDistance = 0.1f; tr.alignment = LineAlignment.TransformZ;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        tr.startWidth = 0.25f; tr.endWidth = 0.25f;

        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/skidmark.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(0f, 0f, 0f, 0.6f);
        }
        tr.sharedMaterial = mat;
        
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.black, 0.0f), new GradientColorKey(Color.black, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0.0f), new GradientAlphaKey(0.6f, 0.8f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        tr.colorGradient = grad;
        return tr;
    }
#endif
    // ==========================================
    // DAMAGE & WRECK LOGIC
    // ==========================================
    bool ShouldIgnoreImpact(GameObject other)
    {
        if (other == null) return true;
        if (other == gameObject) return true;

        Transform otherTransform = other.transform;
        if (otherTransform == transform || otherTransform.IsChildOf(transform) || transform.IsChildOf(otherTransform))
        {
            return true;
        }

        return other.GetComponentInParent<RepairPickup>() != null;
    }

    static bool IsRoadLikeName(string objName)
    {
        return objName.Contains("ground") ||
               objName.Contains("road") ||
               objName.Contains("asphalt") ||
               objName.Contains("terrain") ||
               objName.Contains("street") ||
               objName.Contains("sidewalk") ||
               objName.Contains("floor");
    }

    static bool IsNamedEnvironmentObstacle(string objName)
    {
        return objName.Contains("obstacle") ||
               objName.Contains("barrier") ||
               objName.Contains("wall") ||
               objName.Contains("guard") ||
               objName.Contains("rail") ||
               objName.Contains("divider") ||
               objName.Contains("fence") ||
               objName.Contains("lamp") ||
               objName.Contains("pole") ||
               objName.Contains("bench") ||
               objName.Contains("bollard") ||
               objName.Contains("concrete") ||
               objName.Contains("building");
    }

    bool IsDamageableEnvironment(Collision collision)
    {
        if (collision == null || ShouldIgnoreImpact(collision.gameObject))
        {
            return false;
        }

        string objName = collision.gameObject.name.ToLowerInvariant();
        if (IsRoadLikeName(objName))
        {
            return false;
        }

        if (IsNamedEnvironmentObstacle(objName))
        {
            return true;
        }

        foreach (ContactPoint contact in collision.contacts)
        {
            if (Mathf.Abs(contact.normal.y) < 0.55f)
            {
                return true;
            }
        }

        return false;
    }

    bool IsDamageableEnvironment(Collider other)
    {
        if (other == null || ShouldIgnoreImpact(other.gameObject))
        {
            return false;
        }

        string objName = other.gameObject.name.ToLowerInvariant();
        if (IsRoadLikeName(objName))
        {
            return false;
        }

        if (IsNamedEnvironmentObstacle(objName))
        {
            return true;
        }

        Rigidbody otherRb = other.attachedRigidbody;
        return otherRb == null || otherRb.isKinematic;
    }

    void PlayHeavyImpactFeedback(float impactSeverity, float damageDealt)
    {
        if (damageDealt <= 10f)
        {
            return;
        }

        CameraFollow camFollow = Camera.main?.GetComponent<CameraFollow>();
        if (camFollow != null)
        {
            camFollow.TriggerShake(impactSeverity * 0.05f, 0.4f);
        }

        if (heavyCrashSound != null)
        {
            GameAudioRouting.PlaySfxClipAtPoint(heavyCrashSound, transform.position, 1f, 140);
        }

        StartCoroutine(HitStop(0.05f));
    }

    void ApplyImpactDamage(string logLabel, string sourceName, float impactSeverity, float appliedMultiplier)
    {
        float damageThreshold = Mathf.Max(minImpactForce, crashDamageThreshold);
        if (impactSeverity < damageThreshold)
        {
            return;
        }

        if (Time.unscaledTime - lastDamageTime < damageRepeatCooldown)
        {
            return;
        }

        lastDamageTime = Time.unscaledTime;

        float damageDealt = impactSeverity * appliedMultiplier;
        currentHealth -= damageDealt;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        UpdateDamageVisuals();

        Debug.Log($"[{logLabel}] Hit: {sourceName} | Impact: {impactSeverity:F1} | Damage: {damageDealt:F1} | HP: {currentHealth:F1}");

        PlayHeavyImpactFeedback(impactSeverity, damageDealt);

        if (currentHealth <= 0f)
        {
            WreckCar();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isWrecked || rb == null) return;

        // Severity Logic
        float impactSeverity = collision.relativeVelocity.magnitude;

        // Prevent upward force glitch (Anti-Flip)
        if (collision.relativeVelocity.y > 10f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            Debug.Log("[PHYSICS] Clamped extreme upward bounce!");
        }

        bool isPolice = collision.gameObject.GetComponentInParent<PoliceController>() != null;
        bool isObstacle = IsDamageableEnvironment(collision);

        if (isPolice || isObstacle)
        {
            if (isPolice)
            {
                // The Anti-Glue lateral slip trigger
                slipTimer = 0.5f;

                // We no longer apply an artificial VelocityChange bounce here!
                // Because the Police rigidbodies are now fully physical (Finite Mass), 
                // Unity calculates the momentum transfer organically without snapping your speed.
            }

            float multiplier = isPolice ? damageMultiplier : environmentDamageMultiplier;
            ApplyImpactDamage("Crash", collision.gameObject.name, impactSeverity, multiplier);
        }
    }

    IEnumerator HitStop(float duration)
    {
        if (isWrecked) yield break;
        float prevScale = Time.timeScale;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        if (!isWrecked) Time.timeScale = prevScale;
    }

    void OnTriggerEnter(Collider other)
    {
        if (isWrecked || rb == null) return;

        bool isPolice = other.gameObject.GetComponentInParent<PoliceController>() != null;
        bool isObstacle = IsDamageableEnvironment(other);

        if (!isPolice && !isObstacle) return;

        // Estimate impact severity using relative velocities when possible
        float impactSeverity = rb != null ? rb.linearVelocity.magnitude : 0f;
        Rigidbody otherRb = other.attachedRigidbody;
        if (otherRb != null)
        {
            impactSeverity = (rb.linearVelocity - otherRb.linearVelocity).magnitude;
        }

        if (isPolice)
        {
            slipTimer = 0.5f;
        }

        float multiplier = isPolice ? damageMultiplier : environmentDamageMultiplier;
        ApplyImpactDamage("Crash-Trigger", other.gameObject.name, impactSeverity, multiplier);

        // Simulate physical response for trigger-based barriers so collisions feel real
        Vector3 sepDir = (transform.position - other.transform.position);
        if (sepDir.sqrMagnitude < 0.001f) sepDir = -transform.forward;
        sepDir.y = 0f;
        sepDir.Normalize();

        float impulseMag = Mathf.Clamp(impactSeverity * 1.2f, 1.5f, 35f);
        rb.AddForce(sepDir * impulseMag, ForceMode.Impulse);

        if (otherRb != null && !otherRb.isKinematic)
        {
            otherRb.AddForce(-sepDir * Mathf.Clamp(impulseMag * 0.5f, 0.5f, 20f), ForceMode.Impulse);
        }
    }

    void WreckCar()
    {
        isWrecked = true;
        currentHealth = 0f;
        UpdateDamageVisuals(true);
        Debug.Log("[SUPERMINI] CAR WRECKED! Disabling controls & Freezing game.");
        
        // Kill audio explicitly
        AudioSource[] allSources = GetComponentsInChildren<AudioSource>();
        foreach (var s in allSources) s.Stop();

        // Freeze the game entirely
        Time.timeScale = 0f;

        // Kill audio
        if (engineSource1 != null) engineSource1.Stop();
        if (engineSource2 != null) engineSource2.Stop();
        if (tireSquealSource != null) tireSquealSource.Stop();

        // Simulate engine smoke/fire using existing smoke particles
        if (smokeParticles != null)
        {
            foreach (var ps in smokeParticles)
            {
                if (ps != null)
                {
                    var em = ps.emission;
                    em.enabled = true;
                    // Max out the smoke emission
                    em.rateOverTime = 100f;
                    ps.Play();
                }
            }
        }

        // Trigger global game-over visuals/audio fade (ensure manager exists)
        if (GameOverManager.Instance == null)
        {
            var gm = new GameObject("GameOverManager");
            gm.AddComponent<GameOverManager>();
        }
        GameOverManager.Instance.ShowGameOver();
    }
}
