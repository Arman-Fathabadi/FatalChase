using UnityEngine;
using UnityEditor;

public class WaypointCityGenerator : Editor
{
    [MenuItem("Waypoint/Generate City Patrol Loop")]
    public static void GeneratePatrolLoop()
    {
        // 1. Origin Setup: Create Police_Patrol_Route
        GameObject routeObj = new GameObject("Police_Patrol_Route");
        routeObj.transform.position = Vector3.zero;

        // Try to find the city parts to get a bounding area, otherwise fall back to a default size
        Bounds cityBounds = new Bounds(Vector3.zero, new Vector3(200f, 0f, 200f));
        bool foundCity = false;

        GameObject mainCity = GameObject.Find("city_part_main");
        if (mainCity != null)
        {
            Renderer[] renderers = mainCity.GetComponentsInChildren<Renderer>();
            foreach(var r in renderers)
            {
                if (!foundCity)
                {
                    cityBounds = r.bounds;
                    foundCity = true;
                }
                else
                {
                    cityBounds.Encapsulate(r.bounds);
                }
            }
        }

        GameObject highwayCity = GameObject.Find("city_part_highway");
        if (highwayCity != null)
        {
            Renderer[] renderers = highwayCity.GetComponentsInChildren<Renderer>();
            foreach(var r in renderers)
            {
                if (!foundCity)
                {
                    cityBounds = r.bounds;
                    foundCity = true;
                }
                else
                {
                    cityBounds.Encapsulate(r.bounds);
                }
            }
        }

        // 2. Generate Nodes in a large loop around the city bounds
        int numWaypoints = 24; // 24 points to make a nice loop
        Waypoint[] routeWaypoints = new Waypoint[numWaypoints];

        Vector3 center = cityBounds.center;
        float radiusX = (cityBounds.extents.x * 0.8f); // 80% of the city size
        float radiusZ = (cityBounds.extents.z * 0.8f);

        // Fallback if the bounds failed to find anything substantial
        if (radiusX < 10f) radiusX = 100f;
        if (radiusZ < 10f) radiusZ = 100f;

        for (int i = 0; i < numWaypoints; i++)
        {
            float angle = i * Mathf.PI * 2f / numWaypoints;
            float x = center.x + Mathf.Cos(angle) * radiusX;
            float z = center.z + Mathf.Sin(angle) * radiusZ;

            // Height is 0.5 units above ground (assuming ground is roughly 0 or center.y - extents.y)
            float y = 0.5f; 
            GameObject groundObj = GameObject.Find("Ground");
            if (groundObj != null)
            {
                y = groundObj.transform.position.y + 0.5f;
            }

            Vector3 pos = new Vector3(x, y, z);

            GameObject wpObj = new GameObject("Waypoint " + i);
            wpObj.transform.position = pos;
            wpObj.transform.SetParent(routeObj.transform);
            
            Waypoint wp = wpObj.AddComponent<Waypoint>();
            routeWaypoints[i] = wp;
        }

        // 3. Automation of Node Linking & Orientation
        for (int i = 0; i < numWaypoints; i++)
        {
            Waypoint current = routeWaypoints[i];
            Waypoint prev = routeWaypoints[(i - 1 + numWaypoints) % numWaypoints];
            Waypoint next = routeWaypoints[(i + 1) % numWaypoints];

            current.previousWaypoint = prev;
            current.nextWaypoint = next;

            // Orient the waypoint to look at the next one
            current.transform.LookAt(next.transform.position);
        }

        // 4. Focus the new tool and route
        Selection.activeGameObject = routeObj;
        Undo.RegisterCreatedObjectUndo(routeObj, "Create City Patrol Loop");

        Debug.Log("Created a closed Police_Patrol_Route loop with " + numWaypoints + " waypoints at Y=" + routeWaypoints[0].transform.position.y);
    }
}
