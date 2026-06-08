using UnityEngine;
using TMPro;

/// <summary>
/// This script manages the "Waiting" overlay that appears when a host is
/// waiting for a client to connect. It also pauses the game logic.
/// </summary>
public class WaitingUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject WaitingPanel; // The panel to show/hide
    public TMP_Text StatusText;     // The text showing connection status

    private void Awake()
    {
        // 1. If not assigned, try to find a child named "WaitingPanel"
        if (WaitingPanel == null)
        {
            Transform found = transform.Find("WaitingPanel");
            if (found != null) WaitingPanel = found.gameObject;
        }

        // 2. If still not found, search the whole scene
        if (WaitingPanel == null)
        {
            GameObject globalFound = GameObject.Find("WaitingPanel");
            if (globalFound != null) WaitingPanel = globalFound.gameObject;
        }

        // 3. Link the status text automatically
        if (StatusText == null && WaitingPanel != null)
        {
            StatusText = WaitingPanel.GetComponentInChildren<TMP_Text>(true);
        }

        if (WaitingPanel == null)
        {
            Debug.LogError("[WaitingUI] CRITICAL: No object named 'WaitingPanel' found in this scene! Please create one in the UI.");
        }
        else
        {
            Debug.Log("[WaitingUI] Successfully initialized and linked to: " + WaitingPanel.name);
        }
    }

    private void OnEnable()
    {
        // Hide on start just in case, logic will turn it on in Update
        if (WaitingPanel != null) WaitingPanel.SetActive(false);
    }

    private void Update()
    {
        // Safety check for the singletons
        if (GameNetworkManager.Instance == null || PongNetworkSession.Instance == null) return;

        // Determine if we are "Waiting"
        // 1. We are the Host but no client is connected
        bool isHostWaiting = GameNetworkManager.Instance.IsHost && !GameNetworkManager.Instance.IsClientConnected;

        // 2. We are the Client but we haven't successfully synced with the host yet
        // (This handles the case where the host crashes or leaves during the match)
        bool isClientWaiting = !GameNetworkManager.Instance.IsHost && !GameNetworkManager.Instance.IsClientConnected && !PongNetworkSession.Instance.IsInMenu;

        bool shouldShow = isHostWaiting || isClientWaiting;

        if (shouldShow)
        {
            // Show the waiting panel
            if (WaitingPanel != null)
            {
                if (!WaitingPanel.activeSelf)
                {
                    Debug.Log("[WaitingUI] ACTIVATE: Connection lost or waiting for player.");
                    WaitingPanel.SetActive(true);
                }

                if (StatusText != null)
                    StatusText.text = isHostWaiting ? "En attente d'un deuxième joueur......" : "Connection au host perdue...";
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
                Time.timeScale = 1; // Resume game
            }
        }
    }
}
