using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Lightweight mission framework for FatalChase.
/// Define mission waypoints (world positions), and the manager
/// tracks when the player reaches each one in sequence.
/// </summary>
public class MissionManager : MonoBehaviour
{
    [Header("Player Reference")]
    [Tooltip("The player car transform. Auto-found if empty.")]
    public Transform player;

    [Header("Mission Waypoints")]
    [Tooltip("World positions the player must reach, in order.")]
    public List<MissionWaypoint> waypoints = new List<MissionWaypoint>();

    [Header("Settings")]
    public float arrivalRadius = 10f;

    private int currentWaypointIndex = 0;
    private bool missionComplete = false;

    [System.Serializable]
    public class MissionWaypoint
    {
        public string label = "Checkpoint";
        public Vector3 position;
        public float customRadius = -1f;
    }

    void Start()
    {
        if (player == null)
        {
            SuperminiCarController ctrl = FindAnyObjectByType<SuperminiCarController>();
            if (ctrl != null)
            {
                player = ctrl.transform;
                Debug.Log("[MissionManager] Auto-linked to " + player.name);
            }
            else
            {
                GameObject car = GameObject.Find("SuperminiCar");
                if (car != null) player = car.transform;
            }
        }

        if (player == null)
        {
            Debug.LogError("[MissionManager] No player found!");
            return;
        }

        if (waypoints.Count > 0)
        {
            Debug.Log($"[MissionManager] Mission started! {waypoints.Count} waypoints. First: {waypoints[0].label}");
        }
        else
        {
            Debug.Log("[MissionManager] No waypoints defined. Assign them in the Inspector.");
        }
    }

    void Update()
    {
        if (missionComplete || player == null || waypoints.Count == 0) return;
        if (currentWaypointIndex >= waypoints.Count) return;

        MissionWaypoint current = waypoints[currentWaypointIndex];
        float radius = (current.customRadius > 0) ? current.customRadius : arrivalRadius;
        float distance = Vector3.Distance(player.position, current.position);

        Debug.DrawLine(player.position, current.position, Color.yellow);

        if (distance <= radius)
        {
            Debug.Log($"[MissionManager] Reached waypoint [{currentWaypointIndex}]: {current.label}");
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Count)
            {
                missionComplete = true;
                Debug.Log("[MissionManager] === MISSION COMPLETE ===");
                OnMissionComplete();
            }
            else
            {
                Debug.Log($"[MissionManager] Next: [{currentWaypointIndex}] {waypoints[currentWaypointIndex].label}");
            }
        }
    }

    void OnMissionComplete()
    {
    }

    void OnDrawGizmos()
    {
        for (int i = 0; i < waypoints.Count; i++)
        {
            MissionWaypoint wp = waypoints[i];
            float radius = (wp.customRadius > 0) ? wp.customRadius : arrivalRadius;

            if (i < currentWaypointIndex)
                Gizmos.color = Color.green;
            else if (i == currentWaypointIndex)
                Gizmos.color = Color.yellow;
            else
                Gizmos.color = new Color(1, 1, 1, 0.3f);

            Gizmos.DrawWireSphere(wp.position, radius);

            if (i < waypoints.Count - 1)
            {
                Gizmos.color = new Color(1, 1, 0, 0.5f);
                Gizmos.DrawLine(wp.position, waypoints[i + 1].position);
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(wp.position + Vector3.up * 3f, $"[{i}] {wp.label}");
#endif
        }
    }
}
