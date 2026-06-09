using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class ClientPanelUI : MonoBehaviour
{
    [Header("UI Layout")]
    public Transform GameListContainer; // Where the game buttons will be spawned
    public GameObject GameEntryPrefab;  // A button prefab with a text component
    public TMP_Text EmptyListMessage;   // A text object that says "No games found"

    private float _lastRefreshTime;

    private void OnEnable()
    {
        RefreshGameList();
    }

    private void Update()
    {
        // Auto-refresh the list every 1 second while the panel is open
        if (Time.time - _lastRefreshTime > 1.0f)
        {
            RefreshGameList();
            _lastRefreshTime = Time.time;
        }
    }

    // This listen to UDP broadcasts.
    public void RefreshGameList()
    {
        // 1. Clear existing list
        foreach (Transform child in GameListContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. Get real servers found by the network session
        List<PongClientManager.ServerInfo> foundServers = new List<PongClientManager.ServerInfo>();
        if (PongNetworkSession.Instance != null)
        {
            foundServers = PongNetworkSession.Instance.DiscoveredServers;
        }

        // 3. Check if we found anything
        if (foundServers.Count == 0)
        {
            if (EmptyListMessage != null)
                EmptyListMessage.gameObject.SetActive(true);
        }
        else
        {
            if (EmptyListMessage != null)
                EmptyListMessage.gameObject.SetActive(false);

            foreach (var server in foundServers)
            {
                CreateGameEntry(server.Name, server.IP);
            }
        }
    }

    private void CreateGameEntry(string serverName, string ip)
    {
        GameObject entry = Instantiate(GameEntryPrefab, GameListContainer);

        // Setup the button text using TextMeshPro
        TMP_Text btnText = entry.GetComponentInChildren<TMP_Text>();
        if (btnText != null)
            btnText.text = $"{serverName} ({ip})";

        // Setup the button click action
        Button btn = entry.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => OnGameSelected(ip));
        }
    }

    private void OnGameSelected(string ip)
    {
        Debug.Log($"Joining game at {ip}...");
        GameNetworkManager.Instance.JoinGame(ip);
    }
}
