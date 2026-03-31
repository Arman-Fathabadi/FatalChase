using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class BackgroundMusicController : MonoBehaviour
{
    [Header("Music")]
    public AudioClip musicClip;
    public string fallbackClipName = "game-music";
    [Range(0f, 1f)] public float musicVolume = 0.42f;
    public bool loopMusic = true;
    public bool playOnStart = true;
    public bool stopOnGameEnd = true;
    public float fadeOutDuration = 0.6f;
    public float startupRetryDuration = 2f;
    public float retryInterval = 0.25f;
    public AudioMixerGroup musicMixerGroup;
    public bool routeThroughDefaultMusicMixer = false;
    public string audioSourceChildName = "BackgroundMusicSource";

    AudioSource _audioSource;
    Coroutine _fadeCoroutine;
    Coroutine _startupCoroutine;
    bool _gameEnded;
    float _enabledUnscaledTime;
    float _lastClipResolveAttemptTime = -10f;

    static readonly Dictionary<string, AudioClip> CachedFallbackClips = new Dictionary<string, AudioClip>();

    void Awake()
    {
        EnsureAudioSource();
        ResolveMusicClip();
        ApplyAudioSettings();
    }

    void OnEnable()
    {
        _gameEnded = false;
        _enabledUnscaledTime = Time.unscaledTime;
        if (playOnStart && Application.isPlaying)
        {
            StartPlaybackBootstrap();
        }
    }

    void Start()
    {
        if (playOnStart)
        {
            StartPlaybackBootstrap();
        }
    }

    void Update()
    {
        if (!playOnStart || _gameEnded || _audioSource == null || musicClip == null || _startupCoroutine != null)
        {
            return;
        }

        if (!_audioSource.isPlaying && !_audioSource.mute && Time.unscaledTime - _enabledUnscaledTime <= startupRetryDuration + 0.5f)
        {
            TryStartPlayback();
        }
    }

    void OnDisable()
    {
        if (_startupCoroutine != null)
        {
            StopCoroutine(_startupCoroutine);
            _startupCoroutine = null;
        }
    }

    void OnDestroy()
    {
        if (_startupCoroutine != null)
        {
            StopCoroutine(_startupCoroutine);
            _startupCoroutine = null;
        }
    }

    void OnValidate()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }

        ResolveMusicClip();
        if (_audioSource != null)
        {
            ApplyAudioSettings();
        }
    }

    void EnsureAudioSource()
    {
        if (_audioSource != null)
        {
            _audioSource.enabled = true;
            return;
        }

        // Keep music on a dedicated source so scene camera setup changes do not break playback.
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && !string.IsNullOrWhiteSpace(audioSourceChildName))
        {
            Transform child = transform.Find(audioSourceChildName);
            if (child != null)
            {
                _audioSource = child.GetComponent<AudioSource>();
            }
        }

        if (_audioSource == null)
        {
            GameObject sourceObject = new GameObject(string.IsNullOrWhiteSpace(audioSourceChildName) ? "BackgroundMusicSource" : audioSourceChildName);
            sourceObject.transform.SetParent(transform, false);
            sourceObject.transform.localPosition = Vector3.zero;
            _audioSource = sourceObject.AddComponent<AudioSource>();
        }

        _audioSource.enabled = true;
    }

    void ResolveMusicClip()
    {
        if (musicClip != null)
        {
            CacheResolvedClip();
            return;
        }

        if (_audioSource != null && _audioSource.clip != null)
        {
            if (string.IsNullOrWhiteSpace(fallbackClipName) || _audioSource.clip.name == fallbackClipName)
            {
                musicClip = _audioSource.clip;
                CacheResolvedClip();
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(fallbackClipName))
        {
            return;
        }

        if (CachedFallbackClips.TryGetValue(fallbackClipName, out AudioClip cachedClip) && cachedClip != null)
        {
            musicClip = cachedClip;
            return;
        }

        if (Application.isPlaying)
        {
            float retryDelay = Mathf.Max(0.15f, retryInterval);
            if (Time.unscaledTime - _lastClipResolveAttemptTime < retryDelay)
            {
                return;
            }
        }

        _lastClipResolveAttemptTime = Application.isPlaying ? Time.unscaledTime : -10f;
        AudioClip[] loadedClips = Resources.FindObjectsOfTypeAll<AudioClip>();
        foreach (AudioClip clip in loadedClips)
        {
            if (clip != null && clip.name == fallbackClipName)
            {
                musicClip = clip;
                CacheResolvedClip();
                break;
            }
        }
    }

    void ApplyAudioSettings()
    {
        if (_audioSource == null)
        {
            return;
        }

        _audioSource.enabled = true;
        _audioSource.playOnAwake = false;
        _audioSource.loop = loopMusic;
        _audioSource.spatialBlend = 0f;
        _audioSource.dopplerLevel = 0f;
        _audioSource.ignoreListenerPause = false;
        _audioSource.volume = Mathf.Clamp01(musicVolume);
        _audioSource.clip = musicClip;
        _audioSource.priority = 0;

        if (musicClip != null && musicClip.loadState == AudioDataLoadState.Unloaded)
        {
            musicClip.LoadAudioData();
        }

        if (musicMixerGroup != null)
        {
            GameAudioRouting.ConfigureMusicSource(_audioSource, musicMixerGroup, 0);
        }
        else if (routeThroughDefaultMusicMixer)
        {
            GameAudioRouting.ConfigureMusicSource(_audioSource, null, 0);
        }
        else
        {
            _audioSource.outputAudioMixerGroup = null;
            DisableSourceFilter<AudioHighPassFilter>();
        }
    }

    void StartPlaybackBootstrap()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (_startupCoroutine != null)
        {
            StopCoroutine(_startupCoroutine);
        }

        _startupCoroutine = StartCoroutine(EnsurePlaybackStarts());
    }

    IEnumerator EnsurePlaybackStarts()
    {
        float elapsed = 0f;
        while (!_gameEnded && elapsed <= startupRetryDuration)
        {
            if (TryStartPlayback())
            {
                _startupCoroutine = null;
                yield break;
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, retryInterval));
            elapsed += Mathf.Max(0.05f, retryInterval);
        }

        _startupCoroutine = null;
    }

    bool TryStartPlayback()
    {
        EnsureAudioSource();
        ResolveMusicClip();
        ApplyAudioSettings();

        if (_audioSource == null || musicClip == null)
        {
            return false;
        }

        if (musicClip.loadState == AudioDataLoadState.Unloaded)
        {
            musicClip.LoadAudioData();
        }

        if (musicClip.loadState == AudioDataLoadState.Loading)
        {
            return false;
        }

        if (AudioListener.pause)
        {
            AudioListener.pause = false;
        }

        if (AudioListener.volume <= 0.01f)
        {
            AudioListener.volume = 1f;
        }

        if (!_audioSource.isPlaying)
        {
            _audioSource.Stop();
            _audioSource.time = 0f;
            _audioSource.Play();
            return true;
        }

        return _audioSource.isPlaying;
    }

    void CacheResolvedClip()
    {
        if (musicClip == null || string.IsNullOrWhiteSpace(musicClip.name))
        {
            return;
        }

        CachedFallbackClips[musicClip.name] = musicClip;
        if (!string.IsNullOrWhiteSpace(fallbackClipName))
        {
            CachedFallbackClips[fallbackClipName] = musicClip;
        }
    }

    public void PlayMusic()
    {
        _gameEnded = false;

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (!TryStartPlayback())
        {
            StartPlaybackBootstrap();
        }
    }

    public void HandleGameEnd()
    {
        _gameEnded = true;

        if (_startupCoroutine != null)
        {
            StopCoroutine(_startupCoroutine);
            _startupCoroutine = null;
        }

        if (!stopOnGameEnd || _audioSource == null || !_audioSource.isPlaying)
        {
            return;
        }

        if (fadeOutDuration <= 0f)
        {
            _audioSource.Stop();
            _audioSource.volume = musicVolume * GameAudioRouting.GetMusicGainMultiplier(musicMixerGroup);
            return;
        }

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }

        _fadeCoroutine = StartCoroutine(FadeOutAndStop());
    }

    IEnumerator FadeOutAndStop()
    {
        float startVolume = _audioSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            _audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        _audioSource.Stop();
        _audioSource.volume = Mathf.Clamp01(musicVolume);
        _fadeCoroutine = null;
    }

    void DisableSourceFilter<T>() where T : Behaviour
    {
        if (_audioSource == null)
        {
            return;
        }

        T filter = _audioSource.GetComponent<T>();
        if (filter != null)
        {
            filter.enabled = false;
        }
    }
}
