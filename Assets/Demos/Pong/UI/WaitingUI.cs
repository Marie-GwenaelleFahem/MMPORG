using UnityEngine;
using TMPro;

public class WaitingUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject WaitingPanel;
    public TMP_Text StatusText;

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
            StatusText = WaitingPanel.GetComponentInChildren<TMP_Text>(true);
        }

        if (WaitingPanel == null)
        {
            Debug.LogError("[WaitingUI] CRITICAL: No object named 'WaitingPanel' found in this scene!");
        }
        else
        {
            Debug.Log("[WaitingUI] Successfully initialized and linked to: " + WaitingPanel.name);
        }
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

        bool isCountingDown = PongNetworkSession.Instance.IsCountdownActive;
        string countdownText = PongNetworkSession.Instance.CountdownText;

        bool shouldFreezeGame = (isHostWaiting || isClientDisconnected) && !isCountingDown;
        bool shouldDisplayWaitingPanel = shouldFreezeGame || isClientWaitingForStart || isCountingDown;

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
                    if (isCountingDown)
                    {
                        StatusText.text = countdownText == "GO!" ? "C'EST PARTI !" : $"Le jeu commence dans {countdownText}";
                    }
                    else if (isHostWaiting)
                    {
                        StatusText.text = "En attente d'un deuxième joueur......";
                    }
                    else if (isClientWaitingForStart)
                    {
                        StatusText.text = "Connecte au host. En attente du demarrage...";
                    }
                    else
                    {
                        StatusText.text = "Connection au host perdue...";
                    }
                }
            }

            Time.timeScale = shouldFreezeGame ? 0f : 1f;
        }
        else
        {
            // Hide the waiting panel when everything is fine
            if (WaitingPanel != null && WaitingPanel.activeSelf)
            {
                Debug.Log("[WaitingUI] DEACTIVATE: Match is active.");
                WaitingPanel.SetActive(false);
                // Resume the game
                Time.timeScale = 1;
            }
        }
    }
}
