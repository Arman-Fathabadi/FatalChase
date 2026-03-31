using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    public enum CarType
    {
        FrontWheelDrive,
        RearWheelDrive,
        FourWheelDrive
    }

    public enum ControlMode
    {
        Keyboard, // Uses new Input System if enabled, otherwise legacy input
        Button    // For mobile/UI buttons (call the public Set* methods)
    }

    [Header("Setup")]
    public CarType carType = CarType.FourWheelDrive;
    public ControlMode control = ControlMode.Keyboard;
    public Transform centerOfMass; // optional

    [Header("Wheel Meshes (optional)")]
    public Transform frontWheelLeftMesh;
    public Transform frontWheelRightMesh;
    public Transform backWheelLeftMesh;
    public Transform backWheelRightMesh;

    [Header("Wheel Colliders")]
    public WheelCollider frontWheelLeftCollider;
    public WheelCollider frontWheelRightCollider;
    public WheelCollider backWheelLeftCollider;
    public WheelCollider backWheelRightCollider;

    [Header("Tuning")]
    [Tooltip("Max steering angle in degrees.")]
    public float maximumSteeringAngle = 20f;

    [Tooltip("Max motor torque in Newton-metres (Nm).")]
    public float maximumMotorTorque = 1500f;

    [Tooltip("Top speed in km/h.")]
    public float maximumSpeedKmh = 140f;

    [Tooltip("Brake torque in Nm (applied to all wheels when braking).")]
    public float brakePower = 3000f;

    // Runtime
    private Rigidbody rb;

    private float horizontal;
    private float vertical;
    private bool handBrake;

    // For Button control mode (UI)
    private float buttonHorizontal;
    private float buttonVertical;
    private bool buttonHandBrake;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (centerOfMass != null)
        {
            rb.centerOfMass = centerOfMass.localPosition;
        }
    }

    private void Update()
    {
        ReadInputs();
    }

    private void FixedUpdate()
    {
        ApplySteering();
        ApplyDriveAndBrakes();
        UpdateWheelVisuals();
    }

    // -------------------------
    // Input
    // -------------------------
    private void ReadInputs()
    {
        if (control == ControlMode.Button)
        {
            horizontal = Mathf.Clamp(buttonHorizontal, -1f, 1f);
            vertical = Mathf.Clamp(buttonVertical, -1f, 1f);
            handBrake = buttonHandBrake;
            return;
        }

        // Keyboard mode:
#if ENABLE_INPUT_SYSTEM
        // New Input System
        var kb = Keyboard.current;

        float h = 0f;
        float v = 0f;
        bool hb = false;

        if (kb != null)
        {
            // Horizontal: A/D or Left/Right
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;

            // Vertical: W/S or Up/Down
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v -= 1f;

            hb = kb.spaceKey.isPressed;
        }

        horizontal = Mathf.Clamp(h, -1f, 1f);
        vertical = Mathf.Clamp(v, -1f, 1f);
        handBrake = hb;

#else
        // Legacy Input Manager
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxisRaw("Vertical");
        handBrake = Input.GetKey(KeyCode.Space);
#endif
    }

    // UI button hooks (ControlMode.Button)
    public void SetSteer(float value)     => buttonHorizontal = value; // -1..1
    public void SetThrottle(float value)  => buttonVertical = value;   // -1..1
    public void SetHandBrake(bool value)  => buttonHandBrake = value;

    // -------------------------
    // Steering / Drive / Brakes
    // -------------------------
    private void ApplySteering()
    {
        float steer = maximumSteeringAngle * horizontal;

        // Typical car: steer front wheels only
        if (frontWheelLeftCollider != null)  frontWheelLeftCollider.steerAngle = steer;
        if (frontWheelRightCollider != null) frontWheelRightCollider.steerAngle = steer;
    }

    private void ApplyDriveAndBrakes()
    {
        float speedKmh = rb.linearVelocity.magnitude * 3.6f;

        // Brakes
        if (handBrake)
        {
            SetMotorTorque(0f);
            SetBrakeTorque(brakePower);
            return;
        }

        // If player is not handbraking, release brake torque
        SetBrakeTorque(0f);

        // Motor torque with speed cap (cap only forward acceleration)
        float desiredTorque = maximumMotorTorque * vertical;

        if (speedKmh >= maximumSpeedKmh && vertical > 0f)
        {
            desiredTorque = 0f;
        }

        SetMotorTorque(desiredTorque);
    }

    private void SetMotorTorque(float torque)
    {
        switch (carType)
        {
            case CarType.FrontWheelDrive:
                if (frontWheelLeftCollider != null)  frontWheelLeftCollider.motorTorque = torque;
                if (frontWheelRightCollider != null) frontWheelRightCollider.motorTorque = torque;
                if (backWheelLeftCollider != null)   backWheelLeftCollider.motorTorque = 0f;
                if (backWheelRightCollider != null)  backWheelRightCollider.motorTorque = 0f;
                break;

            case CarType.RearWheelDrive:
                if (backWheelLeftCollider != null)   backWheelLeftCollider.motorTorque = torque;
                if (backWheelRightCollider != null)  backWheelRightCollider.motorTorque = torque;
                if (frontWheelLeftCollider != null)  frontWheelLeftCollider.motorTorque = 0f;
                if (frontWheelRightCollider != null) frontWheelRightCollider.motorTorque = 0f;
                break;

            case CarType.FourWheelDrive:
                if (frontWheelLeftCollider != null)  frontWheelLeftCollider.motorTorque = torque;
                if (frontWheelRightCollider != null) frontWheelRightCollider.motorTorque = torque;
                if (backWheelLeftCollider != null)   backWheelLeftCollider.motorTorque = torque;
                if (backWheelRightCollider != null)  backWheelRightCollider.motorTorque = torque;
                break;
        }
    }

    private void SetBrakeTorque(float brakeTorque)
    {
        if (frontWheelLeftCollider != null)  frontWheelLeftCollider.brakeTorque = brakeTorque;
        if (frontWheelRightCollider != null) frontWheelRightCollider.brakeTorque = brakeTorque;
        if (backWheelLeftCollider != null)   backWheelLeftCollider.brakeTorque = brakeTorque;
        if (backWheelRightCollider != null)  backWheelRightCollider.brakeTorque = brakeTorque;
    }

    // -------------------------
    // Wheel visuals (optional)
    // -------------------------
    private void UpdateWheelVisuals()
    {
        UpdateOneWheel(frontWheelLeftCollider, frontWheelLeftMesh);
        UpdateOneWheel(frontWheelRightCollider, frontWheelRightMesh);
        UpdateOneWheel(backWheelLeftCollider, backWheelLeftMesh);
        UpdateOneWheel(backWheelRightCollider, backWheelRightMesh);
    }

    private static void UpdateOneWheel(WheelCollider wc, Transform mesh)
    {
        if (wc == null || mesh == null) return;

        wc.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot;
    }
}
