using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Survival win condition: keep the Supermini alive until the countdown reaches zero.
/// </summary>
public class SurvivalWinTimer : MonoBehaviour
{
    [Header("Timer")]
    public float countdownSeconds = 120f;

    SuperminiCarController _player;
    Text _timerText;
    Text _objectiveText;
    Image _objectiveCheck;
    float _timeRemaining;
    bool _finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureTimerExists()
    {
        if (FindAnyObjectByType<SurvivalWinTimer>() != null)
        {
            return;
        }

        GameObject timerGo = new GameObject("SurvivalWinTimer");
        timerGo.AddComponent<SurvivalWinTimer>();
    }

    void Start()
    {
        _timeRemaining = countdownSeconds;
        CreateTimerUI();
        StartCoroutine(FindPlayerWhenReady());
        RefreshTimerText();
        RefreshObjectiveUI();
    }

    IEnumerator FindPlayerWhenReady()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (_player == null && elapsed < timeout)
        {
            _player = FindAnyObjectByType<SuperminiCarController>();
            if (_player != null)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    void Update()
    {
        if (_finished)
        {
            return;
        }

        if (_player == null || Time.timeScale <= 0f)
        {
            return;
        }

        if (_player.isWrecked || _player.currentHealth <= 0f)
        {
            return;
        }

        _timeRemaining = Mathf.Max(0f, _timeRemaining - Time.unscaledDeltaTime);
        RefreshTimerText();
        RefreshObjectiveUI();

        if (_timeRemaining > 0f)
        {
            return;
        }

        _finished = true;
        TriggerWin();
    }

    void CreateTimerUI()
    {
        GameObject canvasGo = new GameObject("SurvivalTimerCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject timerGo = new GameObject("SurvivalTimerText");
        timerGo.transform.SetParent(canvasGo.transform, false);
        _timerText = timerGo.AddComponent<Text>();
        _timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _timerText.fontSize = 28;
        _timerText.fontStyle = FontStyle.Bold;
        _timerText.alignment = TextAnchor.UpperRight;
        _timerText.color = Color.white;

        RectTransform timerRect = _timerText.rectTransform;
        timerRect.anchorMin = new Vector2(1f, 1f);
        timerRect.anchorMax = new Vector2(1f, 1f);
        timerRect.pivot = new Vector2(1f, 1f);
        timerRect.anchoredPosition = new Vector2(-22f, -20f);
        timerRect.sizeDelta = new Vector2(260f, 48f);

        GameObject objectivePanelGo = new GameObject("SurvivalObjectivePanel");
        objectivePanelGo.transform.SetParent(canvasGo.transform, false);
        Image objectivePanel = objectivePanelGo.AddComponent<Image>();
        objectivePanel.color = new Color(0f, 0f, 0f, 0.42f);
        RectTransform objectivePanelRect = objectivePanel.GetComponent<RectTransform>();
        objectivePanelRect.anchorMin = new Vector2(1f, 1f);
        objectivePanelRect.anchorMax = new Vector2(1f, 1f);
        objectivePanelRect.pivot = new Vector2(1f, 1f);
        objectivePanelRect.anchoredPosition = new Vector2(-22f, -74f);
        objectivePanelRect.sizeDelta = new Vector2(260f, 58f);

        GameObject objectiveLabelGo = new GameObject("SurvivalObjectiveLabel");
        objectiveLabelGo.transform.SetParent(objectivePanelGo.transform, false);
        Text objectiveLabel = objectiveLabelGo.AddComponent<Text>();
        objectiveLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        objectiveLabel.fontSize = 11;
        objectiveLabel.alignment = TextAnchor.UpperLeft;
        objectiveLabel.color = new Color(0.7f, 0.78f, 0.86f, 1f);
        objectiveLabel.text = "OBJECTIVE";
        RectTransform objectiveLabelRect = objectiveLabel.rectTransform;
        objectiveLabelRect.anchorMin = new Vector2(0f, 1f);
        objectiveLabelRect.anchorMax = new Vector2(1f, 1f);
        objectiveLabelRect.pivot = new Vector2(0f, 1f);
        objectiveLabelRect.anchoredPosition = new Vector2(12f, -8f);
        objectiveLabelRect.sizeDelta = new Vector2(-24f, 16f);

        GameObject objectiveCheckGo = new GameObject("SurvivalObjectiveCheck");
        objectiveCheckGo.transform.SetParent(objectivePanelGo.transform, false);
        _objectiveCheck = objectiveCheckGo.AddComponent<Image>();
        _objectiveCheck.color = new Color(1f, 1f, 1f, 0.16f);
        RectTransform objectiveCheckRect = _objectiveCheck.rectTransform;
        objectiveCheckRect.anchorMin = new Vector2(0f, 0.5f);
        objectiveCheckRect.anchorMax = new Vector2(0f, 0.5f);
        objectiveCheckRect.pivot = new Vector2(0f, 0.5f);
        objectiveCheckRect.anchoredPosition = new Vector2(12f, -7f);
        objectiveCheckRect.sizeDelta = new Vector2(14f, 14f);

        GameObject objectiveTextGo = new GameObject("SurvivalObjectiveText");
        objectiveTextGo.transform.SetParent(objectivePanelGo.transform, false);
        _objectiveText = objectiveTextGo.AddComponent<Text>();
        _objectiveText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _objectiveText.fontSize = 16;
        _objectiveText.alignment = TextAnchor.MiddleLeft;
        _objectiveText.color = Color.white;
        RectTransform objectiveTextRect = _objectiveText.rectTransform;
        objectiveTextRect.anchorMin = new Vector2(0f, 0f);
        objectiveTextRect.anchorMax = new Vector2(1f, 1f);
        objectiveTextRect.offsetMin = new Vector2(34f, 8f);
        objectiveTextRect.offsetMax = new Vector2(-12f, -18f);
    }

    void RefreshTimerText()
    {
        if (_timerText == null)
        {
            return;
        }

        int totalMilliseconds = Mathf.CeilToInt(_timeRemaining * 1000f);
        if (_timeRemaining <= 0f)
        {
            totalMilliseconds = 0;
        }

        int minutes = totalMilliseconds / 60000;
        int seconds = (totalMilliseconds / 1000) % 60;
        int milliseconds = totalMilliseconds % 1000;

        _timerText.text = string.Format("{0:00}:{1:00}:{2:000}", minutes, seconds, milliseconds);

        if (_timeRemaining <= 30f)
        {
            _timerText.color = new Color(0.35f, 1f, 0.45f, 1f);
        }
        else
        {
            _timerText.color = Color.white;
        }
    }

    void RefreshObjectiveUI()
    {
        if (_objectiveText == null || _objectiveCheck == null)
        {
            return;
        }

        _objectiveText.text = "Survive for 2 minutes";
        _objectiveText.color = Color.white;
        _objectiveCheck.color = new Color(1f, 1f, 1f, 0.16f);
    }

    void TriggerWin()
    {
        RefreshObjectiveUI();

        if (GameOverManager.Instance == null)
        {
            GameObject manager = new GameObject("GameOverManager");
            manager.AddComponent<GameOverManager>();
        }

        GameOverManager.Instance.ShowWin();
    }
}
