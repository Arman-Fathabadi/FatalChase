using System.Collections.Generic;
using UnityEngine;

public class VehicleDamageVisuals : MonoBehaviour
{
    class DamageTarget
    {
        public Transform transform;
        public Vector3 baseLocalPosition;
        public Quaternion baseLocalRotation;
        public Vector3 baseLocalScale;
        public Vector3 dentOffsetDirection;
        public Vector3 dentRotationDirection;
        public Vector3 dentScaleDirection;
        public float weight;
    }

    public float smokeStartHealthFraction = 0.65f;
    public float heavySmokeHealthFraction = 0.25f;
    public float maxDentOffset = 0.08f;
    public float maxDentRotation = 7f;
    public float maxScaleReduction = 0.05f;
    public int maxTargets = 10;

    readonly List<DamageTarget> damageTargets = new List<DamageTarget>();
    ParticleSystem damageSmoke;

    public void Initialize()
    {
        if (damageTargets.Count == 0)
        {
            CacheDamageTargets();
        }

        if (damageSmoke == null)
        {
            CreateDamageSmoke();
        }
    }

    public void SetHealthNormalized(float healthNormalized)
    {
        Initialize();

        float damage01 = 1f - Mathf.Clamp01(healthNormalized);
        foreach (DamageTarget target in damageTargets)
        {
            if (target.transform == null) continue;

            float amount = damage01 * target.weight;
            target.transform.localPosition = target.baseLocalPosition + target.dentOffsetDirection * (maxDentOffset * amount);
            target.transform.localRotation = target.baseLocalRotation * Quaternion.Euler(target.dentRotationDirection * (maxDentRotation * amount));

            Vector3 scaleReduction = Vector3.Scale(target.dentScaleDirection, Vector3.one * (maxScaleReduction * amount));
            Vector3 newScale = target.baseLocalScale - scaleReduction;
            target.transform.localScale = new Vector3(
                Mathf.Max(0.78f, newScale.x),
                Mathf.Max(0.78f, newScale.y),
                Mathf.Max(0.78f, newScale.z));
        }

        UpdateSmoke(healthNormalized);
    }

    void CacheDamageTargets()
    {
        HashSet<Transform> used = new HashSet<Transform>();
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            if (renderer.GetComponent<ParticleSystemRenderer>() != null) continue;
            if (renderer.GetComponent<TrailRenderer>() != null) continue;

            Transform target = renderer.transform;
            if (!CanUseTarget(target, used)) continue;

            used.Add(target);
            damageTargets.Add(CreateDamageTarget(target));
            if (damageTargets.Count >= maxTargets)
            {
                break;
            }
        }

        if (damageTargets.Count > 0)
        {
            return;
        }

        foreach (Transform child in transform)
        {
            if (child == null) continue;
            if (child.GetComponentInChildren<Renderer>(true) == null) continue;
            if (!CanUseTarget(child, used)) continue;

            used.Add(child);
            damageTargets.Add(CreateDamageTarget(child));
            if (damageTargets.Count >= maxTargets)
            {
                break;
            }
        }
    }

    DamageTarget CreateDamageTarget(Transform target)
    {
        float hashX = HashToSigned01(target.name, 0);
        float hashY = HashToSigned01(target.name, 1);
        float hashZ = HashToSigned01(target.name, 2);

        DamageTarget damageTarget = new DamageTarget();
        damageTarget.transform = target;
        damageTarget.baseLocalPosition = target.localPosition;
        damageTarget.baseLocalRotation = target.localRotation;
        damageTarget.baseLocalScale = target.localScale;
        damageTarget.dentOffsetDirection = new Vector3(hashX, Mathf.Abs(hashY) * 0.2f, hashZ).normalized;
        damageTarget.dentRotationDirection = new Vector3(hashZ, hashX, hashY).normalized;
        damageTarget.dentScaleDirection = new Vector3(Mathf.Abs(hashX), Mathf.Abs(hashY) * 0.5f, Mathf.Abs(hashZ));
        damageTarget.weight = Mathf.Lerp(0.35f, 1f, Mathf.Abs(HashToSigned01(target.name, 3)));
        return damageTarget;
    }

    void CreateDamageSmoke()
    {
        Bounds bounds = CalculateVehicleBounds();
        GameObject smokeGo = new GameObject("DamageSmoke");
        smokeGo.transform.SetParent(transform, false);

        Vector3 smokeWorldPos = bounds.center + transform.up * (bounds.extents.y * 0.6f) + transform.forward * (bounds.extents.z * 0.15f);
        smokeGo.transform.position = smokeWorldPos;

        damageSmoke = smokeGo.AddComponent<ParticleSystem>();
        damageSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = damageSmoke.main;
        main.playOnAwake = false;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = 1.2f;
        main.startSpeed = 0.25f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startColor = new Color(0.08f, 0.08f, 0.08f, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 160;

        var emission = damageSmoke.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        var shape = damageSmoke.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        var colorOverLifetime = damageSmoke.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.05f, 0.05f, 0.05f), 0f), new GradientColorKey(new Color(0.18f, 0.18f, 0.18f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.2f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        Material sharedMaterial = RuntimeSharedResources.GetTintedSpriteMaterial(Color.white);
        if (sharedMaterial != null)
        {
            ParticleSystemRenderer renderer = damageSmoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sharedMaterial;
        }
    }

    void UpdateSmoke(float healthNormalized)
    {
        if (damageSmoke == null) return;

        float damage01 = 1f - Mathf.Clamp01(healthNormalized);
        float smokeStartDamage = 1f - smokeStartHealthFraction;
        float heavySmokeDamage = 1f - heavySmokeHealthFraction;
        var emission = damageSmoke.emission;

        if (damage01 <= smokeStartDamage)
        {
            emission.enabled = false;
            emission.rateOverTime = 0f;
            if (damageSmoke.isPlaying)
            {
                damageSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            return;
        }

        float smokeStrength = Mathf.InverseLerp(smokeStartDamage, heavySmokeDamage, damage01);
        emission.enabled = true;
        emission.rateOverTime = Mathf.Lerp(2f, 11f, smokeStrength);
        if (!damageSmoke.isPlaying)
        {
            damageSmoke.Play();
        }
    }

    bool CanUseTarget(Transform target, HashSet<Transform> used)
    {
        if (target == null || target == transform || used.Contains(target))
        {
            return false;
        }

        string lowerName = target.name.ToLowerInvariant();
        return !lowerName.Contains("wheel") &&
               !lowerName.Contains("tire") &&
               !lowerName.Contains("tyre") &&
               !lowerName.Contains("rim") &&
               !lowerName.Contains("glass") &&
               !lowerName.Contains("window") &&
               !lowerName.Contains("light") &&
               !lowerName.Contains("smoke") &&
               !lowerName.Contains("trail") &&
               !lowerName.Contains("siren") &&
               !lowerName.Contains("exhaust");
    }

    Bounds CalculateVehicleBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            if (renderer.GetComponent<ParticleSystemRenderer>() != null) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    static float HashToSigned01(string value, int salt)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + value.GetHashCode();
            hash = hash * 31 + salt;
            float normalized = (hash & 0x7fffffff) / (float)int.MaxValue;
            return Mathf.Lerp(-1f, 1f, normalized);
        }
    }
}
