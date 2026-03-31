using System.Collections.Generic;
using UnityEngine;

public static class RuntimeSharedResources
{
    static readonly Dictionary<int, Material> TintedSpriteMaterials = new Dictionary<int, Material>();

    static PhysicsMaterial _superminiAntiSticky;
    static PhysicsMaterial _policeAntiSticky;

    public static PhysicsMaterial GetSuperminiAntiStickyMaterial()
    {
        if (_superminiAntiSticky == null)
        {
            _superminiAntiSticky = CreatePhysicsMaterial(
                "SuperminiAntiSticky",
                0.35f,
                0.35f,
                0.05f);
        }

        return _superminiAntiSticky;
    }

    public static PhysicsMaterial GetPoliceAntiStickyMaterial()
    {
        if (_policeAntiSticky == null)
        {
            _policeAntiSticky = CreatePhysicsMaterial(
                "PoliceAntiSticky",
                0.4f,
                0.4f,
                0.1f);
        }

        return _policeAntiSticky;
    }

    public static Material GetTintedSpriteMaterial(Color color)
    {
        int key = GetColorKey(color);
        if (TintedSpriteMaterials.TryGetValue(key, out Material cachedMaterial) && cachedMaterial != null)
        {
            return cachedMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.name = $"RuntimeSprite_{key:X8}";
        material.color = color;
        TintedSpriteMaterials[key] = material;
        return material;
    }

    static PhysicsMaterial CreatePhysicsMaterial(string materialName, float dynamicFriction, float staticFriction, float bounciness)
    {
        PhysicsMaterial material = new PhysicsMaterial(materialName);
        material.dynamicFriction = dynamicFriction;
        material.staticFriction = staticFriction;
        material.bounciness = bounciness;
        material.frictionCombine = PhysicsMaterialCombine.Minimum;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;
        return material;
    }

    static int GetColorKey(Color color)
    {
        Color32 color32 = color;
        return (color32.r << 24) | (color32.g << 16) | (color32.b << 8) | color32.a;
    }
}
