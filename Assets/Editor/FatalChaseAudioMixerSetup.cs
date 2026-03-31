using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FatalChaseAudioMixerSetup
{
    const string MixerAssetPath = "Assets/Resources/Audio/FatalChaseAudioMixer.mixer";
    const string ResourcesFolder = "Assets/Resources";
    const string AudioResourcesFolder = "Assets/Resources/Audio";
    const string MusicGroupName = "Music";
    const string SfxGroupName = "SFX";

    static readonly string[] UnsupportedMasterEffectNames = { "Limiter", "SimpleLimiter", "Simple Limiter", "HardLimiter", "Hard Limiter" };
    static readonly string[] HighPassNames = { "Highpass", "High Pass" };
    static readonly string[] LowPassNames = { "Lowpass", "Low Pass" };
    static readonly string[] FrequencyParameterNames = { "Cutoff", "Cutoff freq", "CutoffFreq", "Frequency", "Freq" };

    static readonly Assembly EditorAssembly = typeof(Editor).Assembly;
    static readonly Type MixerControllerType = EditorAssembly.GetType("UnityEditor.Audio.AudioMixerController");
    static readonly Type GroupControllerType = EditorAssembly.GetType("UnityEditor.Audio.AudioMixerGroupController");
    static readonly Type SnapshotControllerType = EditorAssembly.GetType("UnityEditor.Audio.AudioMixerSnapshotController");
    static readonly Type EffectControllerType = EditorAssembly.GetType("UnityEditor.Audio.AudioMixerEffectController");
    static readonly Type EffectPluginType = EditorAssembly.GetType("UnityEditor.Audio.AudioMixerEffectPlugin");

    static readonly MethodInfo CreateMixerControllerAtPathMethod = MixerControllerType?.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.Static);
    static readonly MethodInfo GetAllAudioGroupsSlowMethod = MixerControllerType?.GetMethod("GetAllAudioGroupsSlow", BindingFlags.Public | BindingFlags.Instance);
    static readonly MethodInfo CreateNewGroupMethod = MixerControllerType?.GetMethod("CreateNewGroup", BindingFlags.Public | BindingFlags.Instance);
    static readonly MethodInfo AddChildToParentMethod = MixerControllerType?.GetMethod("AddChildToParent", BindingFlags.Public | BindingFlags.Instance);
    static readonly MethodInfo RemoveEffectMethod = MixerControllerType?.GetMethod("RemoveEffect", BindingFlags.Public | BindingFlags.Instance);
    static readonly MethodInfo AddNewSubAssetMethod = MixerControllerType?.GetMethod("AddNewSubAsset", BindingFlags.NonPublic | BindingFlags.Instance);

    static readonly PropertyInfo MasterGroupProperty = MixerControllerType?.GetProperty("masterGroup", BindingFlags.Public | BindingFlags.Instance);
    static readonly PropertyInfo StartSnapshotProperty = MixerControllerType?.GetProperty("startSnapshot", BindingFlags.Public | BindingFlags.Instance);
    static readonly PropertyInfo SnapshotsProperty = MixerControllerType?.GetProperty("snapshots", BindingFlags.Public | BindingFlags.Instance);

    static readonly PropertyInfo GroupEffectsProperty = GroupControllerType?.GetProperty("effects", BindingFlags.Public | BindingFlags.Instance);
    static readonly MethodInfo InsertEffectMethod = GroupControllerType?.GetMethod("InsertEffect", BindingFlags.Public | BindingFlags.Instance);
    static readonly MethodInfo SetValueForVolumeMethod = GroupControllerType?.GetMethod("SetValueForVolume", BindingFlags.Public | BindingFlags.Instance);

    static readonly PropertyInfo EffectNameProperty = EffectControllerType?.GetProperty("effectName", BindingFlags.Public | BindingFlags.Instance);
    static readonly MethodInfo SetValueForParameterMethod = EffectControllerType?.GetMethod("SetValueForParameter", BindingFlags.Public | BindingFlags.Instance);

    static readonly FieldInfo PluginControllerField = EffectPluginType?.GetField("m_Controller", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly FieldInfo PluginEffectField = EffectPluginType?.GetField("m_Effect", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly MethodInfo PluginHasParameterMethod = EffectPluginType?.GetMethod("HasParameter", BindingFlags.Public | BindingFlags.Instance);
    static readonly MethodInfo PluginIsEditableMethod = EffectPluginType?.GetMethod("IsPluginEditableAndEnabled", BindingFlags.Public | BindingFlags.Instance);

    static FatalChaseAudioMixerSetup()
    {
        EditorApplication.delayCall += EnsureDefaultMixer;
    }

    [MenuItem("FatalChase/Audio/Rebuild Default Mixer")]
    public static void RebuildDefaultMixer()
    {
        EnsureDefaultMixer(true);
    }

    static void EnsureDefaultMixer()
    {
        EnsureDefaultMixer(false);
    }

    static void EnsureDefaultMixer(bool forceRebuild)
    {
        if (EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
        {
            return;
        }

        if (MixerControllerType == null ||
            GroupControllerType == null ||
            SnapshotControllerType == null ||
            EffectControllerType == null)
        {
            Debug.LogWarning("[FatalChaseAudioMixerSetup] Unity editor audio mixer APIs were not available.");
            return;
        }

        EnsureFolderExists(ResourcesFolder);
        EnsureFolderExists(AudioResourcesFolder);

        object controller = LoadOrCreateMixerController(forceRebuild);
        if (controller == null)
        {
            return;
        }

        Object masterGroup = MasterGroupProperty?.GetValue(controller) as Object;
        object snapshot = ResolveDefaultSnapshot(controller);
        if (masterGroup == null || snapshot == null)
        {
            Debug.LogWarning("[FatalChaseAudioMixerSetup] Could not resolve the master group or default snapshot.");
            return;
        }

        Object musicGroup = EnsureGroup(controller, masterGroup, MusicGroupName);
        Object sfxGroup = EnsureGroup(controller, masterGroup, SfxGroupName);

        if (musicGroup == null || sfxGroup == null)
        {
            Debug.LogWarning("[FatalChaseAudioMixerSetup] Could not create the Music / SFX groups.");
            return;
        }

        SetGroupVolume(controller, snapshot, musicGroup, -6f);
        SetGroupVolume(controller, snapshot, sfxGroup, -3f);

        // Clear unsupported limiter entries so the mixer can load cleanly.
        RemoveEffects(controller, masterGroup, UnsupportedMasterEffectNames);

        Object highPassEffect = EnsureEffect(controller, musicGroup, HighPassNames);
        if (highPassEffect != null)
        {
            TrySetEffectParameter(controller, snapshot, highPassEffect, FrequencyParameterNames, GameAudioRouting.MusicHighPassCutoffHz);
        }

        Object lowPassEffect = EnsureEffect(controller, sfxGroup, LowPassNames);
        if (lowPassEffect != null)
        {
            TrySetEffectParameter(controller, snapshot, lowPassEffect, FrequencyParameterNames, GameAudioRouting.SfxLowPassCutoffHz);
        }

        EditorUtility.SetDirty((Object)controller);
        AssetDatabase.SaveAssets();
    }

    static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    static object LoadOrCreateMixerController(bool forceRebuild)
    {
        if (forceRebuild)
        {
            Object existingAsset = AssetDatabase.LoadAssetAtPath(MixerAssetPath, typeof(AudioMixer));
            if (existingAsset != null)
            {
                AssetDatabase.DeleteAsset(MixerAssetPath);
            }
        }

        object existingController = AssetDatabase.LoadAssetAtPath(MixerAssetPath, MixerControllerType);
        if (existingController != null)
        {
            return existingController;
        }

        return CreateMixerControllerAtPathMethod?.Invoke(null, new object[] { MixerAssetPath });
    }

    static object ResolveDefaultSnapshot(object controller)
    {
        object startSnapshot = StartSnapshotProperty?.GetValue(controller);
        if (startSnapshot != null)
        {
            return startSnapshot;
        }

        Array snapshots = SnapshotsProperty?.GetValue(controller) as Array;
        return snapshots != null && snapshots.Length > 0 ? snapshots.GetValue(0) : null;
    }

    static Object EnsureGroup(object controller, Object parentGroup, string groupName)
    {
        Object existingGroup = FindGroupByName(controller, groupName);
        if (existingGroup != null)
        {
            return existingGroup;
        }

        object createdGroup = CreateNewGroupMethod?.Invoke(controller, new object[] { groupName, false });
        if (createdGroup == null)
        {
            return null;
        }

        AddChildToParentMethod?.Invoke(controller, new object[] { createdGroup, parentGroup });
        return createdGroup as Object;
    }

    static Object FindGroupByName(object controller, string groupName)
    {
        IEnumerable groups = GetAllAudioGroupsSlowMethod?.Invoke(controller, null) as IEnumerable;
        if (groups == null)
        {
            return null;
        }

        foreach (object group in groups)
        {
            if (group is Object unityObject && unityObject.name == groupName)
            {
                return unityObject;
            }
        }

        return null;
    }

    static void SetGroupVolume(object controller, object snapshot, Object group, float volumeDb)
    {
        if (group == null || snapshot == null || SetValueForVolumeMethod == null)
        {
            return;
        }

        SetValueForVolumeMethod.Invoke(group, new[] { controller, snapshot, (object)volumeDb });
    }

    static Object EnsureEffect(object controller, Object group, string[] candidateNames)
    {
        Object existing = FindEffect(group, candidateNames);
        if (existing != null)
        {
            return existing;
        }

        if (candidateNames == null || candidateNames.Length == 0 || group == null)
        {
            return null;
        }

        foreach (string candidateName in candidateNames)
        {
            if (string.IsNullOrWhiteSpace(candidateName))
            {
                continue;
            }

            object effect = Activator.CreateInstance(EffectControllerType, candidateName);
            if (effect == null)
            {
                continue;
            }

            AddNewSubAssetMethod?.Invoke(controller, new object[] { effect, false });
            int insertIndex = GetEffects(group).Length;
            InsertEffectMethod?.Invoke(group, new object[] { effect, insertIndex });

            if (IsEffectUsable(controller, effect))
            {
                return effect as Object;
            }

            RemoveEffectMethod?.Invoke(controller, new[] { effect, (object)group });
            Object.DestroyImmediate(effect as Object, true);
        }

        return null;
    }

    static void RemoveEffects(object controller, Object group, string[] candidateNames)
    {
        if (controller == null || group == null || candidateNames == null || candidateNames.Length == 0)
        {
            return;
        }

        string[] normalizedCandidates = candidateNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizeName)
            .ToArray();

        foreach (object effect in GetEffects(group))
        {
            if (!(effect is Object effectObject))
            {
                continue;
            }

            string effectName = EffectNameProperty?.GetValue(effect) as string;
            if (!normalizedCandidates.Contains(NormalizeName(effectName)))
            {
                continue;
            }

            RemoveEffectMethod?.Invoke(controller, new[] { effect, (object)group });
            Object.DestroyImmediate(effectObject, true);
        }
    }

    static Object FindEffect(Object group, string[] candidateNames)
    {
        if (group == null || candidateNames == null || candidateNames.Length == 0)
        {
            return null;
        }

        string[] normalizedCandidates = candidateNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizeName)
            .ToArray();

        foreach (object effect in GetEffects(group))
        {
            if (effect == null)
            {
                continue;
            }

            string effectName = EffectNameProperty?.GetValue(effect) as string;
            if (normalizedCandidates.Contains(NormalizeName(effectName)))
            {
                return effect as Object;
            }
        }

        return null;
    }

    static object[] GetEffects(Object group)
    {
        Array effects = GroupEffectsProperty?.GetValue(group) as Array;
        if (effects == null || effects.Length == 0)
        {
            return Array.Empty<object>();
        }

        object[] result = new object[effects.Length];
        for (int i = 0; i < effects.Length; i++)
        {
            result[i] = effects.GetValue(i);
        }

        return result;
    }

    static bool IsEffectUsable(object controller, object effect)
    {
        if (effect == null || EffectPluginType == null || PluginControllerField == null || PluginEffectField == null || PluginIsEditableMethod == null)
        {
            return true;
        }

        try
        {
            object plugin = Activator.CreateInstance(EffectPluginType);
            PluginControllerField.SetValue(plugin, controller);
            PluginEffectField.SetValue(plugin, effect);
            return (bool)PluginIsEditableMethod.Invoke(plugin, null);
        }
        catch
        {
            return true;
        }
    }

    static bool TrySetEffectParameter(object controller, object snapshot, Object effect, string[] parameterCandidates, float value)
    {
        if (effect == null || parameterCandidates == null || parameterCandidates.Length == 0 || SetValueForParameterMethod == null)
        {
            return false;
        }

        object plugin = null;
        if (EffectPluginType != null && PluginControllerField != null && PluginEffectField != null)
        {
            try
            {
                plugin = Activator.CreateInstance(EffectPluginType);
                PluginControllerField.SetValue(plugin, controller);
                PluginEffectField.SetValue(plugin, effect);
            }
            catch
            {
                plugin = null;
            }
        }

        foreach (string parameterName in parameterCandidates)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                continue;
            }

            if (plugin != null && PluginHasParameterMethod != null)
            {
                bool hasParameter = false;
                try
                {
                    hasParameter = (bool)PluginHasParameterMethod.Invoke(plugin, new object[] { parameterName });
                }
                catch
                {
                    hasParameter = false;
                }

                if (!hasParameter)
                {
                    continue;
                }
            }

            try
            {
                SetValueForParameterMethod.Invoke(effect, new[] { controller, snapshot, (object)parameterName, (object)value });
                return true;
            }
            catch
            {
                // Keep trying parameter aliases.
            }
        }

        return false;
    }

    static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Replace(" ", string.Empty).Trim().ToLowerInvariant();
    }
}
