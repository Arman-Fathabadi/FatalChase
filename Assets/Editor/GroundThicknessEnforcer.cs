using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to ensure all ground/road objects have thick BoxColliders (≥ 2m Y)
/// so vehicles can never tunnel through at high speed.
/// Menu: FatalChase → Enforce Ground Thickness
/// </summary>
public class GroundThicknessEnforcer : Editor
{
    [MenuItem("FatalChase/Enforce Ground Thickness")]
    static void EnforceGroundThickness()
    {
        int fixed_count = 0;
        int checked_count = 0;
        float minThickness = 5f;

        // Find all MeshRenderers in the scene
        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);

        foreach (MeshRenderer mr in renderers)
        {
            string lowerName = mr.gameObject.name.ToLower();

            // Only process ground/road/asphalt/highway/plane objects
            if (!lowerName.Contains("ground") &&
                !lowerName.Contains("road") &&
                !lowerName.Contains("asphalt") &&
                !lowerName.Contains("highway") &&
                !lowerName.Contains("plane") &&
                !lowerName.Contains("floor"))
            {
                continue;
            }

            checked_count++;

            // Check for existing BoxCollider
            BoxCollider box = mr.GetComponent<BoxCollider>();
            if (box != null)
            {
                // Check Y thickness
                float worldY = box.size.y * mr.transform.lossyScale.y;
                if (worldY < minThickness)
                {
                    Undo.RecordObject(box, "Enforce Ground Thickness");
                    Vector3 size = box.size;
                    size.y = minThickness / mr.transform.lossyScale.y;
                    box.size = size;

                    // Shift center down so the top surface stays at the same position
                    Vector3 center = box.center;
                    center.y = -(size.y / 2f) + 0.05f;
                    box.center = center;

                    fixed_count++;
                    Debug.Log($"[GroundThickness] Thickened '{mr.name}' BoxCollider Y to {minThickness}m");
                }
            }
            else
            {
                // Check MeshCollider — if it's just a thin plane, add a BoxCollider
                MeshCollider meshCol = mr.GetComponent<MeshCollider>();
                Bounds bounds = mr.bounds;

                if (bounds.size.y < 0.5f) // Basically flat
                {
                    // Add a thick BoxCollider
                    Undo.RecordObject(mr.gameObject, "Add Ground BoxCollider");
                    BoxCollider newBox = Undo.AddComponent<BoxCollider>(mr.gameObject);

                    // Use the mesh bounds for X/Z, force 2m Y
                    Vector3 localSize = new Vector3(
                        bounds.size.x / mr.transform.lossyScale.x,
                        minThickness / mr.transform.lossyScale.y,
                        bounds.size.z / mr.transform.lossyScale.z
                    );
                    newBox.size = localSize;
                    newBox.center = new Vector3(0, -(localSize.y / 2f) + 0.05f, 0);

                    // If there was a MeshCollider, disable it
                    if (meshCol != null)
                    {
                        Undo.RecordObject(meshCol, "Disable MeshCollider");
                        meshCol.enabled = false;
                    }

                    fixed_count++;
                    Debug.Log($"[GroundThickness] Added BoxCollider to '{mr.name}' (was flat)");
                }
            }
        }

        Debug.Log($"[GroundThickness] === Done. Checked {checked_count} ground objects, fixed {fixed_count} ===");
    }
}
