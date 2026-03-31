#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class FBXInspector
{
    [MenuItem("Tools/FatalChase/Inspect SuperminiCar FBX")]
    public static void InspectSuperminiCar()
    {
        // Load the FBX as a GameObject
        string fbxPath = "Assets/SuperminiCarBuilt-In/SuperminiCar Build-In by WITSGaming/SuperminiCarBuilt-In/SuperminiCar.fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

        if (prefab == null)
        {
            Debug.LogError($"[FBXInspector] Could not load FBX at: {fbxPath}");
            return;
        }

        Debug.Log($"[FBXInspector] ===== SuperminiCar FBX Hierarchy =====");
        Debug.Log($"[FBXInspector] Root: '{prefab.name}' children:{prefab.transform.childCount}");

        PrintHierarchy(prefab.transform, 0);

        Debug.Log($"[FBXInspector] ===== END =====");
    }

    static void PrintHierarchy(Transform t, int depth)
    {
        string indent = new string(' ', depth * 2);
        var meshFilter = t.GetComponent<MeshFilter>();
        var meshRenderer = t.GetComponent<MeshRenderer>();
        var skinnedMesh = t.GetComponent<SkinnedMeshRenderer>();

        string meshInfo = "";
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            var bounds = meshFilter.sharedMesh.bounds;
            meshInfo = $" [MeshFilter: verts={meshFilter.sharedMesh.vertexCount}, bounds_size={bounds.size}, bounds_center={bounds.center}]";
        }
        if (skinnedMesh != null)
        {
            meshInfo = $" [SkinnedMesh: verts={skinnedMesh.sharedMesh.vertexCount}]";
        }

        Debug.Log($"[FBXInspector] {indent}'{t.name}' localPos:{t.localPosition} localRot:{t.localEulerAngles} localScale:{t.localScale}{meshInfo}");

        for (int i = 0; i < t.childCount; i++)
        {
            PrintHierarchy(t.GetChild(i), depth + 1);
        }
    }
}
#endif
