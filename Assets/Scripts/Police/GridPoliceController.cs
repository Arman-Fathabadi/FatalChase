using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GridPoliceController : MonoBehaviour
{
    [Header("State Settings")]
    public bool isChasing = true;
    public Transform playerTarget;
    public Waypoint targetWaypoint;

    [Header("Movement Settings")]
    public float chaseSpeed = 15f;
    public float rotationSpeed = 25f;
    public float acceleration = 10f;

    [Header("Siren Audio & Lights")]
    public AudioClip engineClip;
    public AudioClip sirenClip;
    private AudioSource engineSource;
    private AudioSource sirenSource;
    public GameObject sirenLightsContainer;

    [Header("UI Finale")]
    public GameObject bustedUI;
    public Text bustingCountdownText;

    [Header("Busted Timer Settings")]
    public float bustedThreshold = 10f;
    public float captureRange = 8f;

    private Rigidbody rb;
    private float startupTimer = 0f;
    private float stationaryTimer = 0f;
    private float currentSpeed = 0f;

    void Start()
    {
        isChasing = true;
        Time.timeScale = 1f;

        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.interpolation = RigidbodyInterpolation.Interpolate;

        FindInitialWaypoint();

        if (engineClip != null)
        {
            engineSource = gameObject.AddComponent<AudioSource>();
            engineSource.clip = engineClip;
            engineSource.loop = true;
            engineSource.spatialBlend = 1f;
            engineSource.volume = 0.5f;
            engineSource.Play();
        }

        if (sirenClip != null)
        {
            sirenSource = gameObject.AddComponent<AudioSource>();
            sirenSource.clip = sirenClip;
            sirenSource.loop = true;
            sirenSource.spatialBlend = 1f;
            sirenSource.volume = 0f;
            sirenSource.Play();
        }

        SetSirenState(true);

        if (bustedUI != null) bustedUI.SetActive(false);
        if (bustingCountdownText != null) bustingCountdownText.gameObject.SetActive(false);
    }

    void FindInitialWaypoint()
    {
        GameObject network = GameObject.Find("Global_Navigation_Network");
        if (network != null)
        {
            Waypoint[] allWaypoints = network.GetComponentsInChildren<Waypoint>();
            float closestDist = float.MaxValue;

            foreach (Waypoint wp in allWaypoints)
            {
                float dist = Vector3.Distance(transform.position, wp.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    targetWaypoint = wp;
                }
            }
        }
        else
        {
            Debug.LogWarning("GridPoliceController: Could not find Global_Navigation_Network! Cannot initialize grid pathfinding.");
        }
    }

    void FixedUpdate()
    {
        if (startupTimer < 2.0f)
        {
            startupTimer += Time.fixedDeltaTime;
        }

        if (isChasing)
        {
            HandleGreedyNavigation();
            CheckBustedTimer();
            SetSirenState(true);
        }
    }

    void HandleGreedyNavigation()
    {
        if (playerTarget == null) return;

        // Fall back to the player position until the route is ready.
        Vector3 destination = playerTarget.position;

        if (targetWaypoint != null)
        {
            destination = targetWaypoint.transform.position;
            float distToWaypoint = Vector3.Distance(transform.position, destination);

            if (distToWaypoint < 5.0f || (distToWaypoint < 8.0f && VerifyLostPosition(destination)))
            {
                EvaluateNextWaypoint();
                if (targetWaypoint != null)
                {
                    destination = targetWaypoint.transform.position;
                }
            }
        }

        MoveTowards(destination, chaseSpeed);
    }

    bool VerifyLostPosition(Vector3 destination)
    {
        Vector3 toDest = destination - transform.position;
        toDest.y = 0;
        return Vector3.Dot(transform.forward, toDest.normalized) < 0f;
    }

    void EvaluateNextWaypoint()
    {
        if (targetWaypoint == null || playerTarget == null) return;

        List<Waypoint> neighbors = new List<Waypoint>();
        if (targetWaypoint.nextWaypoint != null) neighbors.Add(targetWaypoint.nextWaypoint);
        if (targetWaypoint.previousWaypoint != null) neighbors.Add(targetWaypoint.previousWaypoint);

        // Pick up nearby route nodes to keep intersections connected.
        Collider[] physicsNeighbors = Physics.OverlapSphere(targetWaypoint.transform.position, 15f);
        foreach (Collider c in physicsNeighbors)
        {
            Waypoint wp = c.GetComponent<Waypoint>();
            if (wp != null && wp != targetWaypoint && !neighbors.Contains(wp))
            {
                neighbors.Add(wp);
            }
        }

        if (neighbors.Count == 0) return;

        Waypoint bestNeighbor = null;
        float shortestDistanceToPlayer = float.MaxValue;

        Vector3 pPos = playerTarget.position;

        foreach (Waypoint wp in neighbors)
        {
            Vector3 wPos = wp.transform.position;
            float dx = wPos.x - pPos.x;
            float dz = wPos.z - pPos.z;
            float distToPlayer = Mathf.Sqrt((dx * dx) + (dz * dz));

            if (distToPlayer < shortestDistanceToPlayer)
            {
                shortestDistanceToPlayer = distToPlayer;
                bestNeighbor = wp;
            }
        }

        targetWaypoint = bestNeighbor;
    }


    void CheckBustedTimer()
    {
        if (playerTarget == null) return;

        float dist = Vector3.Distance(transform.position, playerTarget.position);
        float playerVel = 0f;

        Rigidbody playerRb = playerTarget.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerVel = playerRb.linearVelocity.magnitude;
        }

        if (dist < captureRange && playerVel < 1.0f)
        {
            stationaryTimer += Time.fixedDeltaTime;

            if (bustingCountdownText != null)
            {
                bustingCountdownText.gameObject.SetActive(true);
                bustingCountdownText.text = $"BUSTING... {(bustedThreshold - stationaryTimer):F1}s";
            }

            if (stationaryTimer >= bustedThreshold)
            {
                isChasing = false;
                if (bustingCountdownText != null) bustingCountdownText.gameObject.SetActive(false);
                if (bustedUI != null) bustedUI.SetActive(true);

                Time.timeScale = 0f;
                Debug.Log("BUSTED! Police caught the SuperminiCar via stationary timer.");
                if (GameOverManager.Instance == null)
                {
                    var gm = new GameObject("GameOverManager");
                    gm.AddComponent<GameOverManager>();
                }
                GameOverManager.Instance.ShowGameOver();
            }
        }
        else
        {
            stationaryTimer = 0f;
            if (bustingCountdownText != null && bustingCountdownText.gameObject.activeSelf)
            {
                bustingCountdownText.gameObject.SetActive(false);
            }
        }
    }

    void MoveTowards(Vector3 targetPos, float targetSpeed)
    {
        if (rb == null) return;

        Vector3 directionOrig = (targetPos - transform.position);
        Vector3 directionFlat = new Vector3(directionOrig.x, 0f, directionOrig.z);
        if (directionFlat.sqrMagnitude > 0.0001f) directionFlat.Normalize();

        if (directionFlat != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(directionFlat);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

        Vector3 targetVel = transform.forward * currentSpeed;
        targetVel.y = rb.linearVelocity.y;
        rb.linearVelocity = targetVel;

        if (engineSource != null)
        {
            engineSource.pitch = 1f + (currentSpeed / 30f);
        }
    }

    void SetSirenState(bool active)
    {
        if (sirenLightsContainer != null && sirenLightsContainer.activeSelf != active)
        {
            sirenLightsContainer.SetActive(active);
        }

        if (sirenSource != null && sirenSource.clip != null)
        {
            float targetVol = active ? 0.7f : 0f;
            sirenSource.volume = Mathf.MoveTowards(sirenSource.volume, targetVol, Time.deltaTime * 2f);
        }
    }
}
