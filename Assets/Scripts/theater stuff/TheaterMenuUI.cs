using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TheaterMenuUI : MonoBehaviour
{
    public GameObject mainMenuCanvas;

    void Start()
    {
        BuildTheaterMenu();
    }

    void BuildTheaterMenu()
    {
        // Background
        GameObject background = CreatePanel(
            "TheaterBackground",
            transform,
            new Color(0.05f, 0.05f, 0.05f, 1f)
        );

        // =========================
        // TITLE
        // =========================

        GameObject title = CreateText(
            "TitleText",
            background.transform,
            "THEATER MODE",
            60
        );

        RectTransform titleRect = title.GetComponent<RectTransform>();

        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);

        titleRect.sizeDelta = new Vector2(900, 100);
        titleRect.anchoredPosition = new Vector2(0, -50);


        // =========================
        // RECORDED MATCHES TEXT
        // =========================

        GameObject recordedText = CreateText(
            "RecordedMatchesText",
            background.transform,
            "RECORDED MATCHES",
            30
        );

        RectTransform recordedRect =
            recordedText.GetComponent<RectTransform>();

        recordedRect.anchorMin = new Vector2(0.5f, 1f);
        recordedRect.anchorMax = new Vector2(0.5f, 1f);
        recordedRect.pivot = new Vector2(0.5f, 1f);

        recordedRect.sizeDelta = new Vector2(900, 60);
        recordedRect.anchoredPosition = new Vector2(0, -170);


        // =========================
        // MATCH LIST PANEL
        // =========================

        GameObject replayPanel = CreatePanel(
            "ReplayListPanel",
            background.transform,
            new Color(0.12f, 0.12f, 0.12f, 1f)
        );

        RectTransform panelRect =
            replayPanel.GetComponent<RectTransform>();

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);

        panelRect.sizeDelta = new Vector2(1000, 480);
        panelRect.anchoredPosition = new Vector2(0, 30);


        // Automatically stack buttons
        VerticalLayoutGroup layout =
            replayPanel.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.spacing = 25;

        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;


        // =========================
        // TEST MATCHES
        // =========================

        CreateMatchButton(
            replayPanel.transform,
            "Match 1 - Test Map"
        );

        CreateMatchButton(
            replayPanel.transform,
            "Match 2 - Test Map"
        );

        CreateMatchButton(
            replayPanel.transform,
            "Match 3 - Test Map"
        );


        // =========================
        // WATCH REPLAY
        // =========================

        GameObject watchButton = CreateButton(
            "WatchReplayButton",
            background.transform,
            "WATCH REPLAY"
        );

        RectTransform watchRect =
            watchButton.GetComponent<RectTransform>();

        watchRect.anchorMin = new Vector2(0.5f, 0f);
        watchRect.anchorMax = new Vector2(0.5f, 0f);
        watchRect.pivot = new Vector2(0.5f, 0f);

        watchRect.sizeDelta = new Vector2(400, 80);
        watchRect.anchoredPosition = new Vector2(0, 150);


        // =========================
        // BACK
        // =========================

        GameObject backButton = CreateButton(
            "BackButton",
            background.transform,
            "BACK"
        );

        RectTransform backRect =
            backButton.GetComponent<RectTransform>();

        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 0f);
        backRect.pivot = new Vector2(0f, 0f);

        backRect.sizeDelta = new Vector2(250, 70);
        backRect.anchoredPosition = new Vector2(50, 50);

        backButton.GetComponent<Button>()
            .onClick.AddListener(GoBack);
    }


    // =========================
    // CREATE MATCH BUTTON
    // =========================

    void CreateMatchButton(
        Transform parent,
        string matchName)
    {
        GameObject button =
            CreateButton(
                matchName,
                parent,
                matchName
            );

        LayoutElement layout =
            button.AddComponent<LayoutElement>();

        layout.preferredHeight = 100;
    }


    // =========================
    // CREATE PANEL
    // =========================

    GameObject CreatePanel(
        string name,
        Transform parent,
        Color color)
    {
        GameObject panel = new GameObject(name);

        panel.transform.SetParent(parent, false);

        RectTransform rect =
            panel.AddComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.AddComponent<Image>();

        image.color = color;

        return panel;
    }


    // =========================
    // CREATE TEXT
    // =========================

    GameObject CreateText(
        string name,
        Transform parent,
        string text,
        float fontSize)
    {
        GameObject textObject =
            new GameObject(name);

        textObject.transform.SetParent(
            parent,
            false
        );

        RectTransform rect =
            textObject.AddComponent<RectTransform>();

        TextMeshProUGUI tmp =
            textObject.AddComponent<TextMeshProUGUI>();

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;

        tmp.alignment =
            TextAlignmentOptions.Center;

        return textObject;
    }


    // =========================
    // CREATE BUTTON
    // =========================

    GameObject CreateButton(
        string name,
        Transform parent,
        string buttonText)
    {
        GameObject buttonObject =
            new GameObject(name);

        buttonObject.transform.SetParent(
            parent,
            false
        );

        RectTransform rect =
            buttonObject.AddComponent<RectTransform>();

        Image image =
            buttonObject.AddComponent<Image>();

        image.color =
            new Color(
                0.25f,
                0.25f,
                0.25f,
                1f
            );

        Button button =
            buttonObject.AddComponent<Button>();


        // Button text
        GameObject textObject =
            CreateText(
                "Text",
                buttonObject.transform,
                buttonText,
                28
            );

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;

        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return buttonObject;
    }


    // =========================
    // BACK BUTTON
    // =========================

    public void GoBack()
    {
        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.SetActive(true);
        }

        gameObject.SetActive(false);
    }
}