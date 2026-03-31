using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public static class GameAudioRouting
{
    public const string MixerResourcePath = "Audio/FatalChaseAudioMixer";
    public const string MasterGroupName = "Master";
    public const string MusicGroupName = "Music";
    public const string SfxGroupName = "SFX";
    public const float MusicHighPassCutoffHz = 150f;
    public const float SfxLowPassCutoffHz = 10000f;
    public const float MusicFallbackDb = -6f;
    public const float SfxFallbackDb = -3f;
    const int MaxOneShotPoolSize = 8;

    static AudioMixer _mixer;
    static AudioMixerGroup _masterGroup;
    static AudioMixerGroup _musicGroup;
    static AudioMixerGroup _sfxGroup;
    static bool _resolved;
    static GameObject _oneShotPoolRoot;
    static readonly List<AudioSource> _oneShotPool = new List<AudioSource>(MaxOneShotPoolSize);
    static int _oneShotReuseIndex;

    static void EnsureResolved()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;
        _mixer = Resources.Load<AudioMixer>(MixerResourcePath);
        if (_mixer == null)
        {
            return;
        }

        _masterGroup = FindFirstMatchingGroup(MasterGroupName);
        _musicGroup = FindFirstMatchingGroup(MusicGroupName);
        _sfxGroup = FindFirstMatchingGroup(SfxGroupName);
    }

    static AudioMixerGroup FindFirstMatchingGroup(string groupName)
    {
        if (_mixer == null || string.IsNullOrWhiteSpace(groupName))
        {
            return null;
        }

        AudioMixerGroup[] matches = _mixer.FindMatchingGroups(groupName);
        if (matches == null || matches.Length == 0)
        {
            return null;
        }

        foreach (AudioMixerGroup match in matches)
        {
            if (match != null && match.name == groupName)
            {
                return match;
            }
        }

        return matches[0];
    }

    public static AudioMixer GetDefaultMixer()
    {
        EnsureResolved();
        return _mixer;
    }

    public static AudioMixerGroup ResolveMusicGroup(AudioMixerGroup overrideGroup = null)
    {
        if (overrideGroup != null)
        {
            return overrideGroup;
        }

        EnsureResolved();
        return _musicGroup;
    }

    public static AudioMixerGroup ResolveSfxGroup(AudioMixerGroup overrideGroup = null)
    {
        if (overrideGroup != null)
        {
            return overrideGroup;
        }

        EnsureResolved();
        return _sfxGroup;
    }

    public static float GetMusicGainMultiplier(AudioMixerGroup overrideGroup = null)
    {
        return ResolveMusicGroup(overrideGroup) != null ? 1f : DbToLinear(MusicFallbackDb);
    }

    public static float GetSfxGainMultiplier(AudioMixerGroup overrideGroup = null)
    {
        return ResolveSfxGroup(overrideGroup) != null ? 1f : DbToLinear(SfxFallbackDb);
    }

    public static void ConfigureMusicSource(AudioSource source, AudioMixerGroup overrideGroup = null, int priority = 0)
    {
        if (source == null)
        {
            return;
        }

        source.priority = Mathf.Clamp(priority, 0, 256);
        AudioMixerGroup group = ResolveMusicGroup(overrideGroup);
        source.outputAudioMixerGroup = group;

        if (group == null)
        {
            AudioHighPassFilter filter = GetOrAddComponent<AudioHighPassFilter>(source.gameObject);
            filter.cutoffFrequency = MusicHighPassCutoffHz;
            filter.highpassResonanceQ = 1f;
            filter.enabled = true;
        }
        else
        {
            SetComponentEnabled<AudioHighPassFilter>(source.gameObject, false);
        }
    }

    public static void ConfigureSfxSource(AudioSource source, AudioMixerGroup overrideGroup = null, int priority = 160)
    {
        if (source == null)
        {
            return;
        }

        source.priority = Mathf.Clamp(priority, 0, 256);
        if (source.spatialBlend > 0.001f)
        {
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        AudioMixerGroup group = ResolveSfxGroup(overrideGroup);
        source.outputAudioMixerGroup = group;

        if (group == null)
        {
            AudioLowPassFilter filter = GetOrAddComponent<AudioLowPassFilter>(source.gameObject);
            filter.cutoffFrequency = SfxLowPassCutoffHz;
            filter.lowpassResonanceQ = 1f;
            filter.enabled = true;
        }
        else
        {
            SetComponentEnabled<AudioLowPassFilter>(source.gameObject, false);
        }
    }

    public static void PlaySfxClipAtPoint(AudioClip clip, Vector3 position, float volume = 1f, int priority = 180, float spatialBlend = 1f)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource source = GetPooledOneShotSource();
        if (source == null)
        {
            return;
        }

        source.transform.position = position;
        source.Stop();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * GetSfxGainMultiplier());
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.playOnAwake = false;
        source.loop = false;
        source.dopplerLevel = 0f;
        source.minDistance = 4f;
        source.maxDistance = 60f;
        source.time = 0f;

        ConfigureSfxSource(source, null, priority);
        source.Play();
    }

    static AudioSource GetPooledOneShotSource()
    {
        EnsureOneShotPool();

        for (int i = _oneShotPool.Count - 1; i >= 0; i--)
        {
            if (_oneShotPool[i] == null)
            {
                _oneShotPool.RemoveAt(i);
            }
        }

        for (int i = 0; i < _oneShotPool.Count; i++)
        {
            AudioSource pooledSource = _oneShotPool[i];
            if (pooledSource != null && !pooledSource.isPlaying)
            {
                return pooledSource;
            }
        }

        if (_oneShotPool.Count < MaxOneShotPoolSize)
        {
            AudioSource newSource = CreatePooledOneShotSource(_oneShotPool.Count);
            if (newSource != null)
            {
                _oneShotPool.Add(newSource);
            }

            return newSource;
        }

        if (_oneShotPool.Count == 0)
        {
            return null;
        }

        _oneShotReuseIndex %= _oneShotPool.Count;
        AudioSource reusedSource = _oneShotPool[_oneShotReuseIndex];
        _oneShotReuseIndex = (_oneShotReuseIndex + 1) % _oneShotPool.Count;
        return reusedSource;
    }

    static void EnsureOneShotPool()
    {
        if (_oneShotPoolRoot != null)
        {
            return;
        }

        _oneShotPoolRoot = GameObject.Find("GameAudioOneShotPool");
        if (_oneShotPoolRoot == null)
        {
            _oneShotPoolRoot = new GameObject("GameAudioOneShotPool");
            Object.DontDestroyOnLoad(_oneShotPoolRoot);
        }
    }

    static AudioSource CreatePooledOneShotSource(int index)
    {
        EnsureOneShotPool();
        if (_oneShotPoolRoot == null)
        {
            return null;
        }

        GameObject sourceObject = new GameObject($"TempSfxOneShot_{index}");
        sourceObject.transform.SetParent(_oneShotPoolRoot.transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }

    static T GetOrAddComponent<T>(GameObject gameObject) where T : Behaviour
    {
        T existing = gameObject.GetComponent<T>();
        if (existing != null)
        {
            return existing;
        }

        return gameObject.AddComponent<T>();
    }

    static void SetComponentEnabled<T>(GameObject gameObject, bool enabled) where T : Behaviour
    {
        T component = gameObject.GetComponent<T>();
        if (component != null)
        {
            component.enabled = enabled;
        }
    }

    static float DbToLinear(float db)
    {
        return Mathf.Pow(10f, db / 20f);
    }
}
