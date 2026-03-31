using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance;

    enum OverlayState
    {
        None,
        GameOver,
        Win
    }

    [Header("Fade Settings")]
    public float fadeDuration = 0.6f;
    public AudioClip powerOffClip;
    public Color winTextColor = new Color(0.2f, 1f, 0.35f, 1f);
    public Color gameOverTextColor = new Color(1f, 0.2f, 0.2f, 1f);
    public string resetHintMessage = "PRESS R TO RESET";

    Canvas _canvas;
    Image _fadeImage;
    Text _titleText;
    Text _hintText;
    bool _isOverlayActive = false;
    float _initialAudioVolume = 1f;
    OverlayState _overlayState = OverlayState.None;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateOverlay();
    }

    void CreateOverlay()
    {
        GameObject canvasGo = new GameObject("GameOverCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1000;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject fadeGo = new GameObject("FadeImage");
        fadeGo.transform.SetParent(canvasGo.transform, false);
        _fadeImage = fadeGo.AddComponent<Image>();
        _fadeImage.color = new Color(0f, 0f, 0f, 0f);
        RectTransform rt = _fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        _fadeImage.raycastTarget = true;
        _fadeImage.gameObject.SetActive(false);

        GameObject hintGo = new GameObject("ResetHint");
        hintGo.transform.SetParent(canvasGo.transform, false);
        _hintText = hintGo.AddComponent<Text>();
        _hintText.text = resetHintMessage;
        _hintText.alignment = TextAnchor.LowerCenter;
        _hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _hintText.fontSize = 20;
        RectTransform ht = _hintText.rectTransform;
        ht.anchorMin = new Vector2(0f, 0f); ht.anchorMax = new Vector2(1f, 0f); ht.pivot = new Vector2(0.5f, 0f);
        ht.anchoredPosition = new Vector2(0f, 24f); ht.sizeDelta = new Vector2(0f, 40f);
        _hintText.color = new Color(1f, 1f, 1f, 0.85f);
        _hintText.gameObject.SetActive(false);

        GameObject titleGo = new GameObject("ResultTitle");
        titleGo.transform.SetParent(canvasGo.transform, false);
        _titleText = titleGo.AddComponent<Text>();
        _titleText.text = string.Empty;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _titleText.fontSize = 72;
        _titleText.fontStyle = FontStyle.Bold;
        RectTransform titleRt = _titleText.rectTransform;
        titleRt.anchorMin = Vector2.zero;
        titleRt.anchorMax = Vector2.one;
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        _titleText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_isOverlayActive) return;

        // Allow reset via keyboard while paused
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetGame();
        }
    }

    public void ShowGameOver()
    {
        ShowOverlay("BUSTED!", gameOverTextColor, OverlayState.GameOver);
    }

    public void ShowWin()
    {
        ShowOverlay("YOU WON", winTextColor, OverlayState.Win);
    }

    void ShowOverlay(string title, Color titleColor, OverlayState state)
    {
        if (_isOverlayActive) return;
        _isOverlayActive = true;
        _overlayState = state;
        _initialAudioVolume = AudioListener.volume;

        BackgroundMusicController backgroundMusic = FindAnyObjectByType<BackgroundMusicController>();
        if (backgroundMusic != null)
        {
            backgroundMusic.HandleGameEnd();
        }

        _fadeImage.gameObject.SetActive(true);
        _hintText.gameObject.SetActive(true);
        _hintText.text = resetHintMessage;
        _titleText.text = title;
        _titleText.color = titleColor;
        _titleText.gameObject.SetActive(true);
        StartCoroutine(FadeInUnscaled());
    }

    public void ResetGame()
    {
        if (!_isOverlayActive) return;
        
        // Unpause before reloading to avoid getting stuck in 0 timeScale
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AudioListener.volume = _initialAudioVolume;

        // Full Scene Reload for clean game state
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        
        // Reset our local state
        _hintText.gameObject.SetActive(false);
        _titleText.gameObject.SetActive(false);
        _fadeImage.gameObject.SetActive(false);
        _isOverlayActive = false;
        _overlayState = OverlayState.None;
    }

    IEnumerator FadeInUnscaled()
    {
        // play optional power-off clip
        if (powerOffClip != null && Camera.main != null)
        {
            GameAudioRouting.PlaySfxClipAtPoint(powerOffClip, Camera.main.transform.position, 1f, 96, 0f);
        }

        float t = 0f;
        float startVol = AudioListener.volume;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            if (_fadeImage != null) _fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.82f, a));
            AudioListener.volume = Mathf.Lerp(startVol, 0f, a);
            yield return null;
        }
        if (_fadeImage != null) _fadeImage.color = new Color(0f, 0f, 0f, 0.82f);
        AudioListener.volume = 0f;

        // Safety: Global pause for all sounds
        AudioListener.pause = true;
        Time.timeScale = 0f;
    }

    IEnumerator FadeOutUnscaled()
    {
        float t = 0f;
        float startVol = AudioListener.volume;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / fadeDuration);
            if (_fadeImage != null) _fadeImage.color = new Color(0f, 0f, 0f, a);
            AudioListener.volume = Mathf.Lerp(startVol, _initialAudioVolume, 1f - a);
            yield return null;
        }
        if (_fadeImage != null) _fadeImage.color = new Color(0f, 0f, 0f, 0f);
        AudioListener.volume = _initialAudioVolume;

        // Unpause the game
        Time.timeScale = 1f;

        // Tell player to heal / restore
        SuperminiCarController player = FindAnyObjectByType<SuperminiCarController>();
        if (player != null)
        {
            player.HealToFull();
        }

        // Hide hint and fade
        _hintText.gameObject.SetActive(false);
        _titleText.gameObject.SetActive(false);
        _fadeImage.gameObject.SetActive(false);
        _isOverlayActive = false;
        _overlayState = OverlayState.None;
    }
}
