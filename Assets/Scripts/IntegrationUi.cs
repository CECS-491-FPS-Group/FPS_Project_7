using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Small shared presentation helpers for the adapted controls/theater menus.
public static class IntegrationUi
{
    public static GameObject Canvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        go.GetComponent<Canvas>().sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        if (EventSystem.current == null)
        {
            var events = new GameObject("Local Menu Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(go.transform, false);
        }
        return go;
    }
    public static RectTransform Panel(Transform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(.045f, .06f, .085f, .98f);
        return rect;
    }
    public static TMP_Text Text(Transform parent, string value, Vector2 position, Vector2 size, float font = 18)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var label = go.GetComponent<TMP_Text>(); label.transform.SetParent(parent, false);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
        label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size;
        label.font = TMP_Settings.defaultFontAsset; label.text = value; label.fontSize = font;
        label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; label.richText = false;
        return label;
    }
    public static Button Button(Transform parent, string value, Vector2 position, UnityAction action, Vector2? size = null)
    {
        RectTransform rect = Panel(parent, value, size ?? new Vector2(360, 40)); rect.anchoredPosition = position;
        rect.GetComponent<Image>().color = new Color(.16f, .22f, .3f);
        var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
        Text(rect, value, Vector2.zero, rect.sizeDelta - new Vector2(8, 2));
        return button;
    }
}
