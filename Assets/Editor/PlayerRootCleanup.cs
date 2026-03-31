using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to clean up the Player_Root after the Robot/Transformation pivot.
/// Removes missing MonoBehaviour components and ensures the SuperminiCar is the sole active player.
/// Menu: FatalChase → Clean Player Root
/// </summary>
public class PlayerRootCleanup : Editor
{
    [MenuItem("FatalChase/Clean Player Root")]
    static void CleanPlayerRoot()
    {
        // 1. Find or validate Player_Root
        GameObject playerRoot = GameObject.Find("Player_Root");
        if (playerRoot == null)
        {
            Debug.LogWarning("[Cleanup] No 'Player_Root' found in scene.");
            return;
        }

        int removed = 0;

        // 2. Remove all missing/null MonoBehaviour components from entire hierarchy
        foreach (Transform child in playerRoot.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            removed++;
        }
        // Also clean the root itself
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(playerRoot);

        Debug.Log($"[Cleanup] Scanned {removed} objects for missing scripts.");

        // 3. Find and remove the Robert model child
        Transform robert = playerRoot.transform.Find("Robert");
        if (robert == null)
        {
            // Try broader search
            foreach (Transform child in playerRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("Robert") || child.name.Contains("robert"))
                {
                    robert = child;
                    break;
                }
            }
        }
        if (robert != null)
        {
            Undo.DestroyObjectImmediate(robert.gameObject);
            Debug.Log("[Cleanup] Deleted Robert model from Player_Root.");
        }
        else
        {
            Debug.Log("[Cleanup] No Robert model found (already removed).");
        }

        // 4. Ensure SuperminiCar is active
        Transform car = playerRoot.transform.Find("SuperminiCar");
        if (car == null)
        {
            // Search by component
            SuperminiCarController ctrl = playerRoot.GetComponentInChildren<SuperminiCarController>(true);
            if (ctrl != null) car = ctrl.transform;
        }
        if (car != null)
        {
            Undo.RecordObject(car.gameObject, "Enable SuperminiCar");
            car.gameObject.SetActive(true);
            Debug.Log("[Cleanup] SuperminiCar enabled as sole player model.");

            // Ensure its controller is enabled
            SuperminiCarController controller = car.GetComponent<SuperminiCarController>();
            if (controller != null)
            {
                Undo.RecordObject(controller, "Enable Controller");
                controller.enabled = true;
            }
        }
        else
        {
            Debug.LogWarning("[Cleanup] Could not find SuperminiCar in Player_Root!");
        }

        // 5. Configure Player_Root Rigidbody
        Rigidbody rootRb = playerRoot.GetComponent<Rigidbody>();
        if (rootRb != null)
        {
            Undo.RecordObject(rootRb, "Configure Root Rigidbody");
            rootRb.isKinematic = false;
            rootRb.useGravity = true;
            Debug.Log("[Cleanup] Player_Root Rigidbody: isKinematic=false, useGravity=true.");
        }

        // 6. Update police controller targets
        PoliceController[] policeCars = Object.FindObjectsByType<PoliceController>(FindObjectsInactive.Include);
        foreach (PoliceController police in policeCars)
        {
            if (car != null)
            {
                Undo.RecordObject(police, "Update Police Target");
                police.playerTarget = car;
                Debug.Log("[Cleanup] PoliceController '" + police.name + "' now targets SuperminiCar.");
            }
        }

        Debug.Log("[Cleanup] === Player Root Cleanup Complete ===");
    }
}
