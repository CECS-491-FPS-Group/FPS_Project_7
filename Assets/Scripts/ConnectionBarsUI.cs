using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ConnectionBarsUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Shortest bar to tallest bar")]
    [SerializeField] private Image[] bars = new Image[4];

    [Header("Hover tooltip")]
    [SerializeField] private GameObject tooltipRoot;
    [SerializeField] private TMP_Text tooltipText;

    [Header("Connection thresholds")]
    [SerializeField, Min(0)] private int excellentMaxMs = 60;
    [SerializeField, Min(0)] private int goodMaxMs = 100;
    [SerializeField, Min(0)] private int fairMaxMs = 160;

    [Header("Colors")]
    [SerializeField] private Color excellentColor = new Color(0.15f, 0.85f, 0.30f, 1f);
    [SerializeField] private Color goodColor = new Color(0.65f, 0.85f, 0.20f, 1f);
    [SerializeField] private Color fairColor = new Color(1f, 0.65f, 0.10f, 1f);
    [SerializeField] private Color poorColor = new Color(0.95f, 0.20f, 0.15f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.35f, 0.39f, 0.45f, 0.65f);

    private int _latencyMs = -1;

    private void Awake()
    {
        HideTooltip();
        RefreshBars();
    }

    private void OnValidate()
    {
        goodMaxMs = Mathf.Max(goodMaxMs, excellentMaxMs);
        fairMaxMs = Mathf.Max(fairMaxMs, goodMaxMs);
    }

    public void SetLatencyMs(int latencyMs)
    {
        _latencyMs = latencyMs < 0 ? -1 : latencyMs;
        RefreshBars();
        RefreshTooltipText();
    }

    public void SetUnavailable()
    {
        SetLatencyMs(-1);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        RefreshTooltipText();

        if (tooltipRoot != null)
            tooltipRoot.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    private void RefreshBars()
    {
        if (bars == null)
            return;

        int litBarCount = 0;
        Color activeColor = inactiveColor;

        if (_latencyMs >= 0 && _latencyMs <= excellentMaxMs)
        {
            litBarCount = 4;
            activeColor = excellentColor;
        }
        else if (_latencyMs >= 0 && _latencyMs <= goodMaxMs)
        {
            litBarCount = 3;
            activeColor = goodColor;
        }
        else if (_latencyMs >= 0 && _latencyMs <= fairMaxMs)
        {
            litBarCount = 2;
            activeColor = fairColor;
        }
        else if (_latencyMs >= 0)
        {
            litBarCount = 1;
            activeColor = poorColor;
        }

        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] != null)
                bars[i].color = i < litBarCount ? activeColor : inactiveColor;
        }
    }

    private void RefreshTooltipText()
    {
        if (tooltipText == null)
            return;

        tooltipText.text = _latencyMs >= 0
            ? $"{_latencyMs}ms"
            : "--ms";
    }

    private void HideTooltip()
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
    }
}
