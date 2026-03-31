using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform carTarget;

    [Header("Position")]
    public Vector3 moveOffset = new Vector3(0f, 4f, -8f);
    public float moveSmoothness = 5f;

    [Header("Rotation")]
    public Vector3 rotationOffset = new Vector3(0f, 0f, 0f);
    public float rotationSmoothness = 5f;

    private float shakeTimer = 0f;
    private float shakeIntensity = 0f;

    void LateUpdate()
    {
        if (carTarget == null) return;
        FollowTarget();
    }

    void FollowTarget()
    {
        HandleMovement();
        HandleRotation();

        if (shakeTimer > 0f)
        {
            Vector3 shakeOffset = Random.insideUnitSphere * shakeIntensity;
            transform.position += shakeOffset;
            shakeTimer -= Time.deltaTime;
        }
    }

    public void TriggerShake(float intensity, float duration)
    {
        shakeIntensity = Mathf.Min(intensity, 1.5f); // Cap the shake intensity
        shakeTimer = duration;
    }

    void HandleMovement()
    {
        Vector3 targetPos = carTarget.TransformPoint(moveOffset);
        transform.position = Vector3.Lerp(transform.position, targetPos, moveSmoothness * Time.deltaTime);
    }

    void HandleRotation()
    {
        var direction = carTarget.position - transform.position;
        var rotation = Quaternion.LookRotation(direction + rotationOffset, Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation, rotation, rotationSmoothness * Time.deltaTime);
    }
}
