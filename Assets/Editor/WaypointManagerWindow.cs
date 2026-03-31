using UnityEngine;
using UnityEditor;

public class WaypointManagerWindow : EditorWindow
{
    public Transform WaypointOrigin;
    private SerializedObject obj;

    [MenuItem("Waypoint/Waypoints Editor Tools")]
    public static void ShowWindow()
    {
        // Get existing open window or if none, make a new one:
        WaypointManagerWindow window = GetWindow<WaypointManagerWindow>("Waypoint Editor");
        window.Show();
    }

    private void OnEnable()
    {
        obj = new SerializedObject(this);
    }

    private void OnGUI()
    {
        GUILayout.Label("Waypoint Manager", EditorStyles.boldLabel);
        GUILayout.Label("Use this tool to generate and link police waypoints.");
        EditorGUILayout.Space();

        obj.Update();
        
        // Display the WaypointOrigin variable in the editor window
        EditorGUILayout.PropertyField(obj.FindProperty("WaypointOrigin"));

        if (WaypointOrigin == null)
        {
            // Dynamic Warning if nothing is assigned
            EditorGUILayout.HelpBox("Please assign a waypoint origin transform.", MessageType.Warning);
        }
        else
        {
            // Only show buttons if the origin is hooked up
            EditorGUILayout.BeginVertical("box");
            CreateButtons();
            EditorGUILayout.EndVertical();
        }

        obj.ApplyModifiedProperties();
    }

    private void CreateButtons()
    {
        if (GUILayout.Button("Create Waypoint"))
        {
            CreateWaypoint();
        }
    }

    private void CreateWaypoint()
    {
        // 1. Dynamic Naming and Parentage
        int newIndex = WaypointOrigin.childCount;
        GameObject waypointObj = new GameObject("Waypoint " + newIndex);
        waypointObj.transform.SetParent(WaypointOrigin, false);

        // Add the core logic script
        Waypoint newWaypoint = waypointObj.AddComponent<Waypoint>();

        // 2. Automatic Node Linking & Initial Alignment
        if (WaypointOrigin.childCount > 1)
        {
            // Get the previous waypoint (it's the second-to-last child now because we just added the new one)
            Waypoint previousWaypoint = WaypointOrigin.GetChild(WaypointOrigin.childCount - 2).GetComponent<Waypoint>();
            
            if (previousWaypoint != null)
            {
                // Two-way connection
                newWaypoint.previousWaypoint = previousWaypoint;
                previousWaypoint.nextWaypoint = newWaypoint;

                // Initial Alignment (spawn exactly where the last one was)
                waypointObj.transform.position = previousWaypoint.transform.position;
                waypointObj.transform.rotation = previousWaypoint.transform.rotation;
                waypointObj.transform.forward = previousWaypoint.transform.forward;
            }
        }

        // Register action so the user can hit Ctrl+Z to undo placing a waypoint
        Undo.RegisterCreatedObjectUndo(waypointObj, "Create Waypoint");

        // 3. Editor Focus
        Selection.activeGameObject = waypointObj;
    }
}
