using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class GlobalWaypointGenerator : Editor
{
    [MenuItem("Waypoint/Generate Global Grid Network")]
    public static void GenerateGrid()
    {
        GameObject oldNetwork = GameObject.Find("Global_Navigation_Network");
        if (oldNetwork != null)
        {
            Undo.DestroyObjectImmediate(oldNetwork);
        }

        GameObject oldRoute = GameObject.Find("Police_Patrol_Route");
        if (oldRoute != null)
        {
            Undo.DestroyObjectImmediate(oldRoute);
        }

        PoliceController[] policeCars = Object.FindObjectsByType<PoliceController>(FindObjectsInactive.Include);
        foreach (var police in policeCars)
        {
            Undo.RecordObject(police.gameObject, "Deactivate Police");
            police.gameObject.SetActive(false);
        }

        GameObject bustedUI = GameObject.Find("BustedCanvas");
        if (bustedUI != null)
        {
            Undo.DestroyObjectImmediate(bustedUI);
        }
        GameObject bustingUI = GameObject.Find("BustingCanvas");
        if (bustingUI != null)
        {
            Undo.DestroyObjectImmediate(bustingUI);
        }

        GameObject cityRoot = GameObject.Find("demo_city_night");
        if (cityRoot == null)
        {
            cityRoot = GameObject.Find("demo_city_by_versatile_studio"); 
        }

        if (cityRoot == null)
        {
            Debug.LogError("Could not find the city root object. Looked for 'demo_city_night'.");
            return;
        }

        Renderer[] renderers = cityRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogError("No renderers found in city to calculate bounds.");
            return;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        Debug.Log($"City Bounds Calculated: Min {bounds.min}, Max {bounds.max}");

        GameObject networkObj = new GameObject("Global_Navigation_Network");
        Undo.RegisterCreatedObjectUndo(networkObj, "Create Global Navigation Network");
        
        float gridSpacing = 10f;
        float raycastHeight = 100f;
        
        List<Waypoint> routeWaypoints = new List<Waypoint>();

        int waypointCount = 0;
        for (float x = bounds.min.x; x <= bounds.max.x; x += gridSpacing)
        {
            for (float z = bounds.min.z; z <= bounds.max.z; z += gridSpacing)
            {
                Vector3 origin = new Vector3(x, raycastHeight, z);
                
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastHeight * 2))
                {
                    bool validHit = hit.collider.gameObject.name.ToLower().Contains("ground") || 
                                    hit.collider.gameObject.name.ToLower().Contains("road") ||
                                    hit.collider.gameObject.name.ToLower().Contains("highway") ||
                                    hit.collider.gameObject.name.ToLower().Contains("asphalt");

                    if (validHit)
                    {
                        GameObject wpObj = new GameObject($"Waypoint_{waypointCount}");
                        wpObj.transform.position = new Vector3(x, hit.point.y + 0.5f, z);
                        wpObj.transform.SetParent(networkObj.transform, true);
                        
                        Waypoint wp = wpObj.AddComponent<Waypoint>();
                        wp.waypointWidth = 4f;
                        
                        routeWaypoints.Add(wp);
                        Undo.RegisterCreatedObjectUndo(wpObj, "Create Grid Waypoint");
                        waypointCount++;
                    }
                }
            }
        }

        Debug.Log($"Created {waypointCount} valid waypoints. Linking network...");

        foreach (Waypoint wp in routeWaypoints)
        {
            float closestDist = float.MaxValue;
            Waypoint closestNeighbor = null;

            foreach (Waypoint potentialNext in routeWaypoints)
            {
                if (wp == potentialNext) continue;

                Vector3 dir = (potentialNext.transform.position - wp.transform.position);
                
                if (dir.x > 1f || dir.z > 1f)
                {
                    float dist = dir.magnitude;
                    if (dist < gridSpacing * 1.5f && dist < closestDist)
                    {
                        closestDist = dist;
                        closestNeighbor = potentialNext;
                    }
                }
            }

            if (closestNeighbor != null)
            {
                wp.nextWaypoint = closestNeighbor;
                closestNeighbor.previousWaypoint = wp;
            }
        }

        Debug.Log("Global Waypoint Grid Network created successfully!");
    }
}
