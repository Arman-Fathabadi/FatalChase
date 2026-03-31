using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class WaypointSimpleLoopGenerator : Editor
{
    // Keywords that indicate drivable road surface
    static readonly string[] ROAD_KEYWORDS = { "highway", "road", "main", "asphalt", "street", "intersection" };

    [MenuItem("Waypoint/Generate Simple Patrol Loop")]
    public static void GenerateSimpleLoop()
    {
        // ========================================
        // 1. CLEANUP 
        // ========================================
        GameObject oldNetwork = GameObject.Find("Global_Navigation_Network");
        if (oldNetwork != null) Undo.DestroyObjectImmediate(oldNetwork);

        GameObject oldRoute = GameObject.Find("Police_Patrol_Route");
        if (oldRoute != null) Undo.DestroyObjectImmediate(oldRoute);

        PoliceController[] policeCars = Object.FindObjectsByType<PoliceController>(FindObjectsInactive.Include);
        foreach (var p in policeCars) Undo.DestroyObjectImmediate(p.gameObject);
        GridPoliceController[] gridPoliceCars = Object.FindObjectsByType<GridPoliceController>(FindObjectsInactive.Include);
        foreach (var p in gridPoliceCars) Undo.DestroyObjectImmediate(p.gameObject);

        GameObject bustedUI = GameObject.Find("BustedCanvas");
        if (bustedUI != null) Undo.DestroyObjectImmediate(bustedUI);
        GameObject bustingUI = GameObject.Find("BustingCanvas");
        if (bustingUI != null) Undo.DestroyObjectImmediate(bustingUI);

        // ========================================
        // 2. CALCULATE BOUNDS from STATIC_MODELS
        // ========================================
        GameObject staticModels = GameObject.Find("STATIC_MODELS");
        Bounds mapBounds;

        if (staticModels != null)
        {
            Renderer[] renderers = staticModels.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Debug.LogError("No renderers in STATIC_MODELS!"); return; }
            mapBounds = renderers[0].bounds;
            foreach (Renderer r in renderers) mapBounds.Encapsulate(r.bounds);
        }
        else
        {
            // Fallback: use all renderers in the scene
            Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            if (all.Length == 0) { Debug.LogError("No renderers found!"); return; }
            mapBounds = all[0].bounds;
            foreach (Renderer r in all) mapBounds.Encapsulate(r.bounds);
        }

        Debug.Log($"Map Bounds: Min {mapBounds.min}, Max {mapBounds.max}, Size {mapBounds.size}");

        // ========================================
        // 3. PRE-CACHE all road Renderer bounds for verification
        // ========================================
        Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        List<Bounds> roadBounds = new List<Bounds>();
        foreach (Renderer r in allRenderers)
        {
            if (IsRoadSurface(r.gameObject.name))
            {
                roadBounds.Add(r.bounds);
            }
        }
        Debug.Log($"Found {roadBounds.Count} road/highway Renderer meshes for verification.");

        // ========================================
        // 4. GRID SAMPLING - Raycast every 15m
        // ========================================
        float gridSpacing = 15f;
        List<Vector3> roadPoints = new List<Vector3>();

        for (float x = mapBounds.min.x; x <= mapBounds.max.x; x += gridSpacing)
        {
            for (float z = mapBounds.min.z; z <= mapBounds.max.z; z += gridSpacing)
            {
                Vector3 rayOrigin = new Vector3(x, 100f, z);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 200f))
                {
                    string hitName = hit.collider.gameObject.name.ToLower();
                    
                    // Accept if the collider IS a road surface directly
                    bool directHit = IsRoadSurface(hitName);
                    
                    // OR if the collider is "Ground" and the hit point is within a road Renderer's bounds
                    bool groundVerified = false;
                    if (!directHit && hitName.Contains("ground"))
                    {
                        Vector3 checkPoint = hit.point;
                        foreach (Bounds rb in roadBounds)
                        {
                            // Expand bounds slightly on Y to catch flat meshes
                            Bounds expanded = rb;
                            expanded.Expand(new Vector3(2f, 10f, 2f));
                            if (expanded.Contains(checkPoint))
                            {
                                groundVerified = true;
                                break;
                            }
                        }
                    }

                    if (directHit || groundVerified)
                    {
                        roadPoints.Add(new Vector3(hit.point.x, hit.point.y + 0.5f, hit.point.z));
                    }
                }
            }
        }

        Debug.Log($"Grid sampling found {roadPoints.Count} valid road points.");

        if (roadPoints.Count < 4)
        {
            Debug.LogError("Not enough road points found! Try a smaller grid spacing or check mesh names.");
            return;
        }

        // ========================================
        // 4. EXTRACT THE LONGEST CIRCUIT (10-12 points)
        // ========================================
        int targetLoopSize = Mathf.Min(12, roadPoints.Count);
        List<Vector3> loopPoints = ExtractLargestLoop(roadPoints, targetLoopSize);

        Debug.Log($"Extracted {loopPoints.Count}-point patrol circuit.");

        // ========================================
        // 5. CREATE WAYPOINT OBJECTS
        // ========================================
        GameObject routeObj = new GameObject("Police_Patrol_Route");
        Undo.RegisterCreatedObjectUndo(routeObj, "Create Patrol Route");

        int numWaypoints = loopPoints.Count;
        Waypoint[] waypoints = new Waypoint[numWaypoints];

        for (int i = 0; i < numWaypoints; i++)
        {
            GameObject wpObj = new GameObject($"Waypoint {i}");
            wpObj.transform.position = loopPoints[i];
            wpObj.transform.SetParent(routeObj.transform, true);

            Waypoint wp = wpObj.AddComponent<Waypoint>();
            wp.waypointWidth = 4f;
            waypoints[i] = wp;

            Undo.RegisterCreatedObjectUndo(wpObj, "Create Waypoint");
        }

        // ========================================
        // 6. LINK into a perfect circle
        // ========================================
        for (int i = 0; i < numWaypoints; i++)
        {
            Waypoint current = waypoints[i];
            Waypoint prev = (i == 0) ? waypoints[numWaypoints - 1] : waypoints[i - 1];
            Waypoint next = (i == numWaypoints - 1) ? waypoints[0] : waypoints[i + 1];

            current.previousWaypoint = prev;
            current.nextWaypoint = next;

            current.transform.LookAt(new Vector3(
                next.transform.position.x, 
                current.transform.position.y, 
                next.transform.position.z));
        }

        Debug.Log($"Created a {numWaypoints}-point city-wide patrol loop.");

        // ========================================
        // 7. TELEPORT SuperminiCar to Waypoint 0
        // ========================================
        GameObject playerCar = GameObject.Find("SuperminiCar");
        if (playerCar == null)
        {
            SuperminiCarController ctrl = Object.FindAnyObjectByType<SuperminiCarController>();
            if (ctrl != null) playerCar = ctrl.gameObject;
        }

        if (playerCar != null && numWaypoints >= 2)
        {
            Vector3 wp0Pos = waypoints[0].transform.position;
            Vector3 wp1Pos = waypoints[1].transform.position;

            playerCar.transform.position = wp0Pos + Vector3.up * 1.0f;

            Vector3 lookDir = (wp1Pos - wp0Pos);
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                playerCar.transform.rotation = Quaternion.LookRotation(lookDir);

            Rigidbody playerRb = playerCar.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
            }
            Debug.Log("SuperminiCar teleported to Waypoint 0.");
        }

        // ========================================
        // 8. ALIGN Camera
        // ========================================
        CameraFollow camFollow = Object.FindAnyObjectByType<CameraFollow>();
        if (camFollow != null && playerCar != null)
        {
            camFollow.carTarget = playerCar.transform;
            Vector3 chaseCamPos = playerCar.transform.TransformPoint(camFollow.moveOffset);
            camFollow.transform.position = chaseCamPos;
            camFollow.transform.LookAt(playerCar.transform.position);
        }
        else
        {
            Camera mainCam = Camera.main;
            if (mainCam != null && playerCar != null)
            {
                mainCam.transform.position = playerCar.transform.position + (-playerCar.transform.forward * 5f) + (Vector3.up * 3f);
                mainCam.transform.LookAt(playerCar.transform.position);
            }
        }

        // ========================================
        // 9. AUTO-SPAWN the Police Car
        // ========================================
        PoliceControllerSetup.SpawnPoliceCar();
    }

    /// <summary>
    /// Check if a GameObject name matches any road keyword.
    /// </summary>
    static bool IsRoadSurface(string gameObjectName)
    {
        string lower = gameObjectName.ToLower();
        foreach (string keyword in ROAD_KEYWORDS)
        {
            if (lower.Contains(keyword)) return true;
        }
        return false;
    }

    /// <summary>
    /// Extract N points that form the widest possible loop through the city.
    /// Uses a greedy "convex hull then fill" approach to maximize coverage.
    /// </summary>
    static List<Vector3> ExtractLargestLoop(List<Vector3> allPoints, int targetCount)
    {
        // Step 1: Pick the farthest-spread points using a greedy algorithm.
        // Start from the point with the minimum X, then always pick the point
        // farthest from all already-selected points. This maximizes coverage.

        List<Vector3> selected = new List<Vector3>();
        List<Vector3> remaining = new List<Vector3>(allPoints);

        // Start with the point that has the smallest X (leftmost road point)
        int startIdx = 0;
        for (int i = 1; i < remaining.Count; i++)
        {
            if (remaining[i].x < remaining[startIdx].x) startIdx = i;
        }
        selected.Add(remaining[startIdx]);
        remaining.RemoveAt(startIdx);

        // Greedily add the point farthest from the current selection
        while (selected.Count < targetCount && remaining.Count > 0)
        {
            float bestMinDist = -1f;
            int bestIdx = 0;

            for (int i = 0; i < remaining.Count; i++)
            {
                // Find the minimum distance from this candidate to any already-selected point
                float minDistToSelected = float.MaxValue;
                foreach (Vector3 s in selected)
                {
                    float d = Vector3.Distance(remaining[i], s);
                    if (d < minDistToSelected) minDistToSelected = d;
                }

                // We want the candidate whose minimum distance is the LARGEST (= farthest from everything)
                if (minDistToSelected > bestMinDist)
                {
                    bestMinDist = minDistToSelected;
                    bestIdx = i;
                }
            }

            selected.Add(remaining[bestIdx]);
            remaining.RemoveAt(bestIdx);
        }

        // Step 2: Sort the selected points into a loop order using angular sorting
        // around their centroid, so they form a clean non-crossing circuit.
        Vector3 centroid = Vector3.zero;
        foreach (Vector3 p in selected) centroid += p;
        centroid /= selected.Count;

        selected.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2(a.z - centroid.z, a.x - centroid.x);
            float angleB = Mathf.Atan2(b.z - centroid.z, b.x - centroid.x);
            return angleA.CompareTo(angleB);
        });

        return selected;
    }
}
