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

        bool isHostWaiting = GameNetworkManager.Instance.IsHost && !GameNetworkManager.Instance.IsClientConnected;

        bool isClientInSession = !GameNetworkManager.Instance.IsHost && !PongNetworkSession.Instance.IsInMenu;
        bool isClientDisconnected = isClientInSession && !GameNetworkManager.Instance.IsClientConnected;
        bool isClientWaitingForStart = isClientInSession && GameNetworkManager.Instance.IsClientConnected &&
                                       !PongNetworkSession.Instance.IsMatchActive;

        bool shouldDisplayWaitingPanel = isHostWaiting || isClientDisconnected || isClientWaitingForStart;

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
                        StatusText.text = "Connecte au host. En attente du demarrage...";
                    }
                    else
                    {
                        StatusText.text = "Connection au host perdue...";
                    }
                }
            }

            // Freeze the game while waiting
            Time.timeScale = 0;
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
