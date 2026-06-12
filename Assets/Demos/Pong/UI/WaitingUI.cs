using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WaitingUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject WaitingPanel;
    public TMP_Text StatusText;
    public Button ReturnToMenuButton;

    private void Awake()
    {
        if (WaitingPanel == null)
        {
            Transform found = transform.Find("WaitingPanel");
            if (found != null) WaitingPanel = found.gameObject;
        }

        if (WaitingPanel == null)
        {
            GameObject globalFound = GameObject.Find("WaitingPanel");
            if (globalFound != null) WaitingPanel = globalFound.gameObject;
        }

        if (StatusText == null && WaitingPanel != null)
        {
            Transform statusTransform = WaitingPanel.transform.Find("StatusText");
            if (statusTransform != null)
            {
                StatusText = statusTransform.GetComponent<TMP_Text>();
            }
        }

        if (WaitingPanel == null)
        {
            Debug.LogError("[WaitingUI] CRITICAL: No object named 'WaitingPanel' found in this scene!");
        }
        else
        {
            Debug.Log("[WaitingUI] Successfully initialized and linked to: " + WaitingPanel.name);
        }

        SetupReturnToMenuButton();
    }

    private void OnEnable()
    {
        if (WaitingPanel != null) WaitingPanel.SetActive(false);
    }

    private void Update()
    {
        if (GameNetworkManager.Instance == null || PongNetworkSession.Instance == null) return;

        bool isHosting = PongNetworkSession.Instance.IsHosting;
        bool isClienting = PongNetworkSession.Instance.IsClienting;

        bool isHostWaiting = isHosting && !PongNetworkSession.Instance.IsMatchActive;
        bool isClientDisconnected = isClienting && !PongNetworkSession.Instance.IsConnectedToHost;
        bool isClientWaitingForStart = isClienting && PongNetworkSession.Instance.IsConnectedToHost &&
                                       !PongNetworkSession.Instance.IsMatchActive;

        bool shouldFreezeGame = isHostWaiting || isClientDisconnected;
        bool shouldDisplayWaitingPanel = shouldFreezeGame || isClientWaitingForStart;

        if (shouldDisplayWaitingPanel)
        {
            if (WaitingPanel != null)
            {
                if (!WaitingPanel.activeSelf)
                {
                    Debug.Log("[WaitingUI] ACTIVATE: Connection lost or waiting for player.");
                    WaitingPanel.SetActive(true);
                }

                if (StatusText != null)
                {
                    if (isHostWaiting)
                    {
                        StatusText.text = "En attente d'un deuxième joueur......";
                    }
                    else if (isClientWaitingForStart)
                    {
                        StatusText.text = "Connecté au host. En attente du démarrage...";
                    }
                    else
                    {
                        StatusText.text = "Connexion au host perdue...";
                    }
                }
            }

            Time.timeScale = shouldFreezeGame ? 0f : 1f;
        }
        else
        {
            if (WaitingPanel != null && WaitingPanel.activeSelf)
            {
                Debug.Log("[WaitingUI] DEACTIVATE: Match is active.");
                WaitingPanel.SetActive(false);
                Time.timeScale = 1;
            }
        }
    }

    public void OnReturnToMenu()
    {
        Time.timeScale = 1f;

        if (WaitingPanel != null)
        {
            WaitingPanel.SetActive(false);
        }

        PongNetworkSession.Instance?.StopSession(true);
    }

    void SetupReturnToMenuButton()
    {
        if (WaitingPanel == null)
        {
            return;
        }

        if (ReturnToMenuButton == null)
        {
            Transform existing = WaitingPanel.transform.Find("ButtonReturnToMenu");
            if (existing != null)
            {
                ReturnToMenuButton = existing.GetComponent<Button>();
            }
        }

        if (ReturnToMenuButton == null)
        {
            ReturnToMenuButton = CreateReturnToMenuButton();
        }

        if (ReturnToMenuButton == null)
        {
            return;
        }

        ReturnToMenuButton.onClick.RemoveListener(OnReturnToMenu);
        ReturnToMenuButton.onClick.AddListener(OnReturnToMenu);
    }

    Button CreateReturnToMenuButton()
    {
        var buttonObject = new GameObject(
            "ButtonReturnToMenu",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        buttonObject.transform.SetParent(WaitingPanel.transform, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -80f);
        rect.sizeDelta = new Vector2(280f, 40f);

        var image = buttonObject.GetComponent<Image>();
        image.color = Color.white;
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

        var textObject = new GameObject("Text (TMP)", typeof(RectTransform));
        textObject.transform.SetParent(buttonObject.transform, false);

        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = "Retour menu principal";
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        if (StatusText != null)
        {
            label.font = StatusText.font;
        }

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }
}
