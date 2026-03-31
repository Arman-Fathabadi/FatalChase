#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class SuperminiCarBuilder
{
    const string FBX_PATH = "Assets/SuperminiCarBuilt-In/SuperminiCar Build-In by WITSGaming/SuperminiCarBuilt-In/SuperminiCar.fbx";

    [MenuItem("Tools/FatalChase/Create SuperminiCar")]
    public static void CreateSuperminiCar()
    {
        // 1. Load the FBX prefab
        var fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_PATH);
        if (fbxPrefab == null)
        {
            Debug.LogError($"[SuperminiCarBuilder] Could not load FBX at: {FBX_PATH}");
            return;
        }

        // 2. Create a root physics GameObject
        var car = new GameObject("SuperminiCar");
        Undo.RegisterCreatedObjectUndo(car, "Create SuperminiCar");
        car.transform.position = Vector3.up * 0.8f;

        // 3. Instantiate the FBX model as a child — DO NOT MODIFY IT AT ALL
        var model = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab, car.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // CRITICAL: Unpack the prefab so we can freely reparent its children (wheel meshes)
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // 4. Strip ONLY colliders from FBX (to prevent physics fighting)
        int removedColliders = 0;
        foreach (var col in model.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(col);
            removedColliders++;
        }
        Debug.Log($"[SuperminiCarBuilder] Stripped {removedColliders} colliders from FBX model");

        // 5. Calculate the overall bounds of the car model
        Bounds carBounds = CalculateBounds(model);
        Debug.Log($"[SuperminiCarBuilder] Model bounds center:{carBounds.center} size:{carBounds.size}");

        // 6. Calculate wheel positions from the model bounds.
        // Use bounds.center.x so wheel track is centered even if the mesh pivot isn't centered.
        float halfWidth = carBounds.size.x * 0.39f;
        float wheelBaseXOffset = -0.14f; // negative = shift wheels left
        float centerX = carBounds.center.x + wheelBaseXOffset;
        float frontZ = carBounds.center.z + carBounds.size.z * 0.30f;
        float rearZ = carBounds.center.z - carBounds.size.z * 0.33f;
        float wheelRadius = Mathf.Clamp(carBounds.size.y * 0.22f, 0.25f, 0.40f);
        float wheelY = carBounds.min.y + wheelRadius * 1.02f;

        Vector3 flLocal = model.transform.TransformPoint(new Vector3(centerX - halfWidth, wheelY, frontZ)) - car.transform.position;
        Vector3 frLocal = model.transform.TransformPoint(new Vector3(centerX + halfWidth, wheelY, frontZ)) - car.transform.position;
        Vector3 rlLocal = model.transform.TransformPoint(new Vector3(centerX - halfWidth, wheelY, rearZ))  - car.transform.position;
        Vector3 rrLocal = model.transform.TransformPoint(new Vector3(centerX + halfWidth, wheelY, rearZ))  - car.transform.position;

        Debug.Log($"[SuperminiCarBuilder] Wheel positions (local): FL:{flLocal} FR:{frLocal} RL:{rlLocal} RR:{rrLocal} (xOffset:{wheelBaseXOffset:F3})");

        // 7. Add Rigidbody
        var rb = car.AddComponent<Rigidbody>();
        rb.mass = 1400f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 1.5f;

        // 8. Add body BoxCollider — raised to ensure no ground dragging
        var bodyCol = car.AddComponent<BoxCollider>();
        Vector3 localCenter = model.transform.TransformPoint(carBounds.center) - car.transform.position;
        bodyCol.center = localCenter + Vector3.up * 0.3f;
        bodyCol.size = new Vector3(carBounds.size.x * 0.75f, carBounds.size.y * 0.35f, carBounds.size.z * 0.80f);

        // 9. Center of mass
        var com = new GameObject("CenterOfMass");
        com.transform.SetParent(car.transform, false);
        com.transform.localPosition = new Vector3(0f, localCenter.y - carBounds.size.y * 0.15f, localCenter.z);

        // 10. Create WheelColliders

        var fl = CreateWheel(car.transform, "WC_FL", flLocal, wheelRadius);
        var fr = CreateWheel(car.transform, "WC_FR", frLocal, wheelRadius);
        var rl = CreateWheel(car.transform, "WC_RL", rlLocal, wheelRadius);
        var rr = CreateWheel(car.transform, "WC_RR", rrLocal, wheelRadius);

        // 11. Resolve visible wheel meshes from the FBX model
        var renderMeshes = new List<Transform>();
        CollectRenderableTransforms(model.transform, renderMeshes);
        var usedMeshes = new HashSet<Transform>();

        Transform flM = FindMeshByTokens(renderMeshes, usedMeshes, "tire_fl", "trire_fl", "tyre_fl", "wheel_fl");
        Transform frM = FindMeshByTokens(renderMeshes, usedMeshes, "tire_fr", "tyre_fr", "wheel_fr");
        Transform rlM = FindMeshByTokens(renderMeshes, usedMeshes, "tire_rl", "tyre_rl", "wheel_rl");
        Transform rrM = FindMeshByTokens(renderMeshes, usedMeshes, "tire_rr", "tyre_rr", "wheel_rr");

        Vector3 flWorld = car.transform.TransformPoint(flLocal);
        Vector3 frWorld = car.transform.TransformPoint(frLocal);
        Vector3 rlWorld = car.transform.TransformPoint(rlLocal);
        Vector3 rrWorld = car.transform.TransformPoint(rrLocal);

        if (flM == null) flM = FindClosestWheelLikeMesh(renderMeshes, usedMeshes, flWorld);
        if (frM == null) frM = FindClosestWheelLikeMesh(renderMeshes, usedMeshes, frWorld);
        if (rlM == null) rlM = FindClosestWheelLikeMesh(renderMeshes, usedMeshes, rlWorld);
        if (rrM == null) rrM = FindClosestWheelLikeMesh(renderMeshes, usedMeshes, rrWorld);

        Debug.Log($"[SuperminiCarBuilder] Visual wheels: FL='{(flM ? flM.name : "null")}', FR='{(frM ? frM.name : "null")}', RL='{(rlM ? rlM.name : "null")}', RR='{(rrM ? rrM.name : "null")}'");
        if (flM == null || frM == null || rlM == null || rrM == null)
        {
            Debug.LogWarning("[SuperminiCarBuilder] One or more visible wheel meshes were not found. Wheel visuals may stay static.");
        }

        // 12. Create wheel visual proxies and attach actual wheel meshes to them.
        // This gives us clean pivots for spin/steer while preserving mesh orientation.
        var flProxy = CreateWheelVisualProxy(car.transform, "Wheel_FL", flLocal, flM);
        var frProxy = CreateWheelVisualProxy(car.transform, "Wheel_FR", frLocal, frM);
        var rlProxy = CreateWheelVisualProxy(car.transform, "Wheel_RL", rlLocal, rlM);
        var rrProxy = CreateWheelVisualProxy(car.transform, "Wheel_RR", rrLocal, rrM);

        // 13. Attach SuperminiCarController
        var ctrl = car.AddComponent<SuperminiCarController>();
        ctrl.FL = fl; ctrl.FR = fr; ctrl.RL = rl; ctrl.RR = rr;
        ctrl.FLMesh = flProxy; ctrl.FRMesh = frProxy; ctrl.RLMesh = rlProxy; ctrl.RRMesh = rrProxy;
        ctrl.centerOfMass = com.transform;
        
        // Auto-assign brake lights from the FBX
        ctrl.brakeLights = FindBrakeLightRenderers(renderMeshes);

        // Auto-assign engine audio
        ctrl.engineSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audios/engine.mp3");

        // Supermini tuning
        ctrl.maxEngineTorque = 2500f;  // Massive torque for extreme RWD wheelspin
        ctrl.reverseTorque = 1500f;
        ctrl.maxSpeedKmh = 220f;
        ctrl.maxReverseSpeedKmh = 40f;
        ctrl.maxBrakeTorque = 4500f;
        ctrl.handbrakeTorque = 6500f;
        ctrl.maxSteerAngle = 35f;
        ctrl.minMass = 1400f;
        ctrl.downforce = 15f;   // Very low downforce = much easier to break traction
        ctrl.antiRoll = 4000f;

        // Loosen rear tire grip so they spin more under power
        LooseRearGrip(rl);
        LooseRearGrip(rr);

        // 14. Ground plane if needed
        if (Object.FindAnyObjectByType<Terrain>() == null &&
            GameObject.Find("Ground") == null &&
            GameObject.Find("Plane") == null)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(50f, 1f, 50f);
            Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
        }

        // 15. Setup camera to follow the car
        // Find ANY camera in the scene (not just Camera.main, which requires the "MainCamera" tag)
        Camera mainCam = Camera.main;
        if (mainCam == null)
            mainCam = Object.FindAnyObjectByType<Camera>();
        if (mainCam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            mainCam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            Undo.RegisterCreatedObjectUndo(camGo, "Create Camera");
        }
        var camFollow = mainCam.GetComponent<CameraFollow>();
        if (camFollow == null)
            camFollow = mainCam.gameObject.AddComponent<CameraFollow>();
        camFollow.carTarget = car.transform;
        camFollow.moveOffset = new Vector3(0f, 2.5f, -8f);
        camFollow.moveSmoothness = 5f;
        camFollow.rotationSmoothness = 5f;
        Debug.Log($"[SuperminiCarBuilder] CameraFollow attached to '{mainCam.name}' targeting SuperminiCar");

        // 16. Procedural Tire Smoke & Skid Trail Setup
        var smokeRL = CreateProceduralSmoke(car.transform, "Smoke_RL", rlLocal);
        var smokeRR = CreateProceduralSmoke(car.transform, "Smoke_RR", rrLocal);
        ctrl.smokeParticles = new ParticleSystem[] { smokeRL, smokeRR };

        var trailRL = CreateProceduralSkidTrail(car.transform, "SkidTrail_RL", rlLocal);
        var trailRR = CreateProceduralSkidTrail(car.transform, "SkidTrail_RR", rrLocal);
        ctrl.skidTrails = new TrailRenderer[] { trailRL, trailRR };

        Selection.activeGameObject = car;
        EditorGUIUtility.PingObject(car);
        Debug.Log($"[SuperminiCarBuilder] SuperminiCar created! WheelRadius:{wheelRadius:F3} Box:{bodyCol.center}/{bodyCol.size}");
    }

    static void CollectRenderableTransforms(Transform t, List<Transform> results)
    {
        if (t.GetComponent<Renderer>() != null)
            results.Add(t);

        for (int i = 0; i < t.childCount; i++)
            CollectRenderableTransforms(t.GetChild(i), results);
    }

    static Transform FindMeshByTokens(List<Transform> meshes, HashSet<Transform> used, params string[] tokens)
    {
        foreach (string token in tokens)
        {
            string tokenLower = token.ToLowerInvariant();
            foreach (var mesh in meshes)
            {
                if (used.Contains(mesh)) continue;
                if (!mesh.name.ToLowerInvariant().Contains(tokenLower)) continue;
                used.Add(mesh);
                return mesh;
            }
        }

        return null;
    }

    static Transform FindClosestWheelLikeMesh(List<Transform> meshes, HashSet<Transform> used, Vector3 worldPos)
    {
        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (var mesh in meshes)
        {
            if (used.Contains(mesh)) continue;
            if (!IsWheelLikeName(mesh.name)) continue;

            float dist = Vector3.Distance(mesh.position, worldPos);
            if (dist < closestDist)
            {
                closest = mesh;
                closestDist = dist;
            }
        }

        if (closest != null) used.Add(closest);
        return closest;
    }

    static bool IsWheelLikeName(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("wheel") || n.Contains("tire") || n.Contains("tyre") || n.Contains("rim") || n.Contains("trire");
    }

    static Renderer[] FindBrakeLightRenderers(List<Transform> meshes)
    {
        List<Renderer> found = new List<Renderer>();
        foreach (var mesh in meshes)
        {
            if (mesh == null) continue;
            string n = mesh.name.ToLowerInvariant();
            
            // Look for common brake/tail light naming conventions
            if (n.Contains("tail") || n.Contains("brake") || n.Contains("stop") || (n.Contains("rear") && (n.Contains("light") || n.Contains("lamp"))))
            {
                var r = mesh.GetComponent<Renderer>();
                if (r != null)
                {
                    found.Add(r);
                    Debug.Log($"[SuperminiCarBuilder] Auto-discovered brake light: {mesh.name}");
                }
            }
        }
        return found.ToArray();
    }

    static Bounds CalculateBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        var localCenter = go.transform.InverseTransformPoint(bounds.center);
        return new Bounds(localCenter, bounds.size);
    }

    static WheelCollider CreateWheel(Transform parent, string name, Vector3 pos, float radius)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Wheel");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;

        var wc = go.AddComponent<WheelCollider>();
        wc.radius = radius;
        wc.suspensionDistance = 0.12f;
        wc.mass = 20f;

        var sp = wc.suspensionSpring;
        sp.spring = 35000f; sp.damper = 4500f; sp.targetPosition = 0.5f;
        wc.suspensionSpring = sp;

        var fwd = wc.forwardFriction;
        fwd.extremumSlip = 0.35f; fwd.extremumValue = 1.10f;
        fwd.asymptoteSlip = 0.90f; fwd.asymptoteValue = 0.75f;
        wc.forwardFriction = fwd;

        var side = wc.sidewaysFriction;
        side.extremumSlip = 0.25f; side.extremumValue = 1.15f;
        side.asymptoteSlip = 0.75f; side.asymptoteValue = 0.85f;
        wc.sidewaysFriction = side;

        return wc;
    }

    static Transform CreateWheelVisualProxy(Transform parent, string name, Vector3 pos, Transform wheelMesh)
    {
        var proxy = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(proxy, "WheelVisualProxy");
        proxy.transform.SetParent(parent, false);
        proxy.transform.localPosition = pos;

        if (wheelMesh != null)
        {
            wheelMesh.SetParent(proxy.transform, true);
            wheelMesh.localPosition = Vector3.zero;
        }

        return proxy.transform;
    }

    // Reduces grip on rear wheels so they spin more under torque (RWD feel)
    static void LooseRearGrip(WheelCollider wc)
    {
        if (!wc) return;

        var fwd = wc.forwardFriction;
        fwd.extremumSlip = 0.55f;  // Very loose — slips early
        fwd.extremumValue = 0.70f; // Much less peak grip
        fwd.asymptoteSlip = 1.5f;  // Very wide slip range
        fwd.asymptoteValue = 0.45f;
        wc.forwardFriction = fwd;

        var side = wc.sidewaysFriction;
        side.extremumSlip = 0.35f;
        side.extremumValue = 0.95f; // Slightly less lateral grip
        side.asymptoteSlip = 0.85f;
        side.asymptoteValue = 0.70f;
        wc.sidewaysFriction = side;
    }

    static ParticleSystem CreateProceduralSmoke(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Smoke");
        go.transform.SetParent(parent, false);
        
        // Position slightly at the bottom of the tire
        go.transform.localPosition = pos - new Vector3(0, 0.25f, 0); 

        var ps = go.AddComponent<ParticleSystem>();
        
        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = 1.0f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startColor = new Color(0.8f, 0.8f, 0.8f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; // leave smoke trails behind
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 500;
        
        var emission = ps.emission;
        emission.enabled = false;
        emission.rateOverTime = 60f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.gray, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        col.color = grad;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3f));

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        
        return ps;
    }

    static TrailRenderer CreateProceduralSkidTrail(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Skid Trail");
        go.transform.SetParent(parent, false);
        
        // Position slightly at the bottom of the tire
        go.transform.localPosition = pos - new Vector3(0, 0.38f, 0); 

        var tr = go.AddComponent<TrailRenderer>();
        tr.emitting = false;
        tr.time = 3f;
        tr.minVertexDistance = 0.1f;
        tr.alignment = LineAlignment.TransformZ;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        
        tr.startWidth = 0.25f;
        tr.endWidth = 0.25f;

        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/skidmark.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(0f, 0f, 0f, 0.6f);
        }
        
        tr.sharedMaterial = mat;
        
        // Color / fade
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.black, 0.0f), new GradientColorKey(Color.black, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0.0f), new GradientAlphaKey(0.6f, 0.8f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        tr.colorGradient = grad;

        return tr;
    }
}
#endif
