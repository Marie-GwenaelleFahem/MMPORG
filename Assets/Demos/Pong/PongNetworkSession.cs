using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

// Simplified orchestrator for Pong networking, delegates actual networking work to PongServerManager and PongClientManager.
public class PongNetworkSession : MonoBehaviour
{
    public static PongNetworkSession Instance { get; private set; }

    [Header("Managers")]
    public PongServerManager ServerManager;
    public PongClientManager ClientManager;

    [Header("UI")]
    public GameObject GameGameplayUI;

    public bool IsMatchActive => (ServerManager != null && ServerManager.IsMatchActive) ||
                                (ClientManager != null && ClientManager.IsMatchActive);

    // Returns true if we are in a network session (either as host or client).
    public bool IsNetworkSession => (ServerManager != null && ServerManager.gameObject.activeSelf) ||
                                    (ClientManager != null && ClientManager.gameObject.activeSelf);

    // Returns true if we are not currently in an active host or client session.
    public bool IsInMenu => !IsNetworkSession;

    public bool CanMovePaddle(PongPlayer player)
    {
        if (ServerManager == null || ClientManager == null) return false;

        // If both are inactive
        if (!ServerManager.gameObject.activeSelf && !ClientManager.gameObject.activeSelf) return true;

        if (!IsMatchActive) return false;

        // Only the Host can move the Left paddle locally
        if (ServerManager.gameObject.activeSelf) return player == PongPlayer.PlayerLeft;

        return false;
    }

    private UDPService udp;
    private List<PongClientManager.ServerInfo> discoveredServers = new List<PongClientManager.ServerInfo>();
    public List<PongClientManager.ServerInfo> DiscoveredServers => discoveredServers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "Pong") return;
        if (Instance != null) return;

        GameObject go = new GameObject("PongNetworkSession");
        go.AddComponent<PongNetworkSession>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        udp = GetComponent<UDPService>();
        if (udp == null) udp = gameObject.AddComponent<UDPService>();

        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureManagers();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Pong")
        {
            CacheRefsInManagers();
        }
    }

    private void EnsureManagers()
    {
        if (ServerManager == null) ServerManager = GetComponentInChildren<PongServerManager>(true);
        if (ClientManager == null) ClientManager = GetComponentInChildren<PongClientManager>(true);

        if (ServerManager == null)
        {
            GameObject serverGo = new GameObject("ServerManager");
            serverGo.transform.SetParent(transform);
            ServerManager = serverGo.AddComponent<PongServerManager>();
            serverGo.SetActive(false);
        }

        if (ClientManager == null)
        {
            GameObject clientGo = new GameObject("ClientManager");
            clientGo.transform.SetParent(transform);
            ClientManager = clientGo.AddComponent<PongClientManager>();
            clientGo.SetActive(false);
        }

        CacheRefsInManagers();
    }

    private void CacheRefsInManagers()
    {
        PongPaddle left = FindPaddle(PongPlayer.PlayerLeft);
        PongPaddle right = FindPaddle(PongPlayer.PlayerRight);
        PongBall ball = GameObject.FindAnyObjectByType<PongBall>(FindObjectsInactive.Include);

        if (ServerManager != null) { ServerManager.PaddleLeft = left; ServerManager.PaddleRight = right; ServerManager.Ball = ball; }
        if (ClientManager != null) { ClientManager.PaddleLeft = left; ClientManager.PaddleRight = right; ClientManager.Ball = ball; }
    }

    private PongPaddle FindPaddle(PongPlayer player)
    {
        PongPaddle[] paddles = GameObject.FindObjectsByType<PongPaddle>(FindObjectsInactive.Include);
        foreach (PongPaddle paddle in paddles) { if (paddle.Player == player) return paddle; }
        return null;
    }

    public void StartHost()
    {
        StopSession(false);
        EnsureManagers();

        // Release the discovery binding so the ServerManager can take over the port
        if (udp != null) udp.CloseUDP();

        ServerManager.gameObject.SetActive(true);
        ClientManager.gameObject.SetActive(false);
        ServerManager.StartServer();
        ApplyGameplayUI(true);
    }

    public void StartClient(string ip)
    {
        StopSession(false);
        EnsureManagers();

        // Release the discovery binding so the ClientManager can take over the port
        if (udp != null) udp.CloseUDP();

        ServerManager.gameObject.SetActive(false);
        ClientManager.gameObject.SetActive(true);
        ClientManager.StartClient(ip);
        ApplyGameplayUI(true);
    }

    public void SetRemoteIP(string ip)
    {
        if (ClientManager != null) ClientManager.ServerIP = ip;
    }

    public void StopSession(bool showMenu = true)
    {
        if (ServerManager != null) { ServerManager.StopServer(); ServerManager.gameObject.SetActive(false); }
        if (ClientManager != null) { ClientManager.StopClient(); ClientManager.gameObject.SetActive(false); }

        if (udp != null) udp.CloseUDP();

        ApplyGameplayUI(false);

        if (showMenu)
        {
            MenuController menu = FindAnyObjectByType<MenuController>();
            if (menu != null) menu.ShowMainPanel();
        }
    }

    private void ApplyGameplayUI(bool active)
    {
        if (GameGameplayUI != null) GameGameplayUI.SetActive(active);
    }

    public void RequestReplay()
    {
        if (ServerManager != null && ServerManager.gameObject.activeSelf) ServerManager.ResetMatch(true);
        else if (ClientManager != null && ClientManager.gameObject.activeSelf) ClientManager.RequestReplay();
    }

    private void OnHostMigrationTriggered()
    {
        Debug.Log("[NetworkSession] Migrating to Host...");
        StartHost();
    }

    private void Update()
    {
        // Background discovery when not in an active session
        if (IsInMenu)
        {
            if (!udp.IsBound)
            {
                // Discovery uses the fixed port 25000
                if (!udp.Bind(25000, OnDiscoveryMessage)) { udp.Bind(0, OnDiscoveryMessage); }
                ;
            }
            // Cleanup old servers
            discoveredServers.RemoveAll(s => Time.time - s.LastSeen > 5.0f);
        }
    }

    private void OnDiscoveryMessage(string message, IPEndPoint source)
    {
        if (message.StartsWith("B|", StringComparison.Ordinal))
        {
            string[] parts = message.Split('|');
            Debug.Log($"Parts {string.Join(", ", parts)}");
            if (parts.Length >= 2)
            {
                string name = parts[1];
                string ip = source.Address.ToString();
                int index = discoveredServers.FindIndex(s => s.IP == ip);
                if (index >= 0)
                {
                    var info = discoveredServers[index];
                    info.LastSeen = Time.time;
                    discoveredServers[index] = info;
                }
                else
                {
                    discoveredServers.Add(new PongClientManager.ServerInfo { Name = name, IP = ip, LastSeen = Time.time });
                    Debug.Log($"[Discovery] Found server: {name} at {ip}");
                }
            }
        }
    }
}
