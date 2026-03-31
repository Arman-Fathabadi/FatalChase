using UnityEngine;

public class Waypoint : MonoBehaviour
{
    [Header("Connections")]
    public Waypoint previousWaypoint;
    public Waypoint nextWaypoint;

    [Header("Settings")]
    [Range(0f, 5f)]
    public float waypointWidth = 5f;

    /// <summary>
    /// Calculates a random target position within the waypoint's width.
    /// This prevents police cars from all driving in a single-file line.
    /// </summary>
    public Vector3 GetPosition()
    {
        // minBound represents the right side of the waypoint lane
        Vector3 minBound = transform.position + transform.right * (waypointWidth / 2f);
        
        // maxBound represents the left side of the waypoint lane
        Vector3 maxBound = transform.position - transform.right * (waypointWidth / 2f);

        // Return a random position interpolated between the two bounds
        return Vector3.Lerp(minBound, maxBound, Random.Range(0f, 1f));
    }
}
