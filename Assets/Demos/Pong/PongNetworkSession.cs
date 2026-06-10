using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PongNetworkSession : MonoBehaviour
{
    public static PongNetworkSession Instance { get; private set; }

    [Header("Managers")]
    public PongServerManager ServerManager;
    public PongClientManager ClientManager;

    [Header("UI")]
    public GameObject GameGameplayUI;

    readonly PongRoundFlow roundFlow = new PongRoundFlow();

    UDPService udp;
    readonly List<PongClientManager.ServerInfo> discoveredServers = new List<PongClientManager.ServerInfo>();

    public List<PongClientManager.ServerInfo> DiscoveredServers => discoveredServers;

    public bool IsMatchActive => (ServerManager != null && ServerManager.IsMatchActive) ||
                                 (ClientManager != null && ClientManager.IsMatchActive);

    public bool IsNetworkSession => (ServerManager != null && ServerManager.gameObject.activeSelf) ||
                                    (ClientManager != null && ClientManager.gameObject.activeSelf);

    public bool IsInMenu => !IsNetworkSession;
    public bool IsCountdownActive => roundFlow.IsCountdownActive;
    public bool IsGameplayUnlocked => roundFlow.IsGameplayUnlocked(IsMatchActive);
    public string CountdownText => roundFlow.CountdownText;
    public PongPlayer LocalSide => roundFlow.LocalSide;
    public float LocalSpeedShare => roundFlow.LocalSpeedShare;
    public int LocalSidePlayerCount => roundFlow.LocalSidePlayerCount;

    public bool CanMovePaddle(PongPlayer player)
    {
        if (!IsNetworkSession)
        {
            return true;
        }

        return false;
    }

    public float GetPaddleSpeedMultiplier(PongPlayer player)
    {
        if (!IsNetworkSession)
        {
            return 1f;
        }

        return 0f;
    }

    public bool TryConsumeSideAnnouncement(out string primary, out string secondary)
    {
        return roundFlow.TryConsumeAnnouncement(out primary, out secondary);
    }

    public void SetSideAssignment(PongPlayer side, float share, int count, bool showAnnouncement)
    {
        roundFlow.SetSideAssignment(side, share, count, showAnnouncement);
    }

    public void BeginRoundCountdown()
    {
        if (!IsNetworkSession)
        {
            return;
        }

        roundFlow.BeginCountdown();
        ApplyBallSimulation();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnableRunInBackground()
    {
        Application.runInBackground = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "Pong")
        {
            return;
        }

        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("PongNetworkSession");
        go.AddComponent<PongNetworkSession>();
    }

    void Awake()
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

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Pong")
        {
            CacheRefsInManagers();
        }
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name != "Pong")
        {
            return;
        }

        bool wasCountdown = roundFlow.IsCountdownActive;
        roundFlow.UpdateCountdown();
        if (wasCountdown && !roundFlow.IsCountdownActive)
        {
            ApplyBallSimulation();
            if (ServerManager != null && ServerManager.gameObject.activeSelf)
            {
                ServerManager.OnCountdownFinished();
            }
        }

        if (IsInMenu)
        {
            if (!udp.IsBound)
            {
                if (!udp.Bind(25000, OnDiscoveryMessage))
                {
                    udp.Bind(0, OnDiscoveryMessage);
                }
            }

            discoveredServers.RemoveAll(s => Time.unscaledTime - s.LastSeen > 5f);
        }
    }

    void EnsureManagers()
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

    void CacheRefsInManagers()
    {
        PongPaddle left = FindPaddle(PongPlayer.PlayerLeft);
        PongPaddle right = FindPaddle(PongPlayer.PlayerRight);
        PongBall ball = GameObject.FindAnyObjectByType<PongBall>(FindObjectsInactive.Include);

        if (ServerManager != null)
        {
            ServerManager.PaddleLeft = left;
            ServerManager.PaddleRight = right;
            ServerManager.Ball = ball;
        }

        if (ClientManager != null)
        {
            ClientManager.PaddleLeft = left;
            ClientManager.PaddleRight = right;
            ClientManager.Ball = ball;
        }
    }

    PongPaddle FindPaddle(PongPlayer player)
    {
        PongPaddle[] paddles = GameObject.FindObjectsByType<PongPaddle>(FindObjectsInactive.Include);
        foreach (PongPaddle paddle in paddles)
        {
            if (paddle.Player == player)
            {
                return paddle;
            }
        }

        return null;
    }

    public void SetJoinSide(PongPlayer side)
    {
        if (ClientManager != null)
        {
            ClientManager.SetJoinSide(side);
        }
    }

    public void StartHost()
    {
        StopSession(false);
        EnsureManagers();
        roundFlow.Reset();

        if (udp != null)
        {
            udp.CloseUDP();
        }

        ServerManager.gameObject.SetActive(true);
        ClientManager.gameObject.SetActive(false);
        ServerManager.StartServer();
        SetSideAssignment(PongPlayer.PlayerLeft, 1f, 1, true);
        ApplyGameplayUI(true);
    }

    public void StartClient(string ip)
    {
        StopSession(false);
        EnsureManagers();
        roundFlow.Reset();

        if (udp != null)
        {
            udp.CloseUDP();
        }

        ServerManager.gameObject.SetActive(false);
        ClientManager.gameObject.SetActive(true);
        ClientManager.StartClient(ip);
        ApplyGameplayUI(true);
    }

    public void StopSession(bool showMenu = true)
    {
        roundFlow.Reset();

        if (ServerManager != null)
        {
            ServerManager.StopServer();
            ServerManager.gameObject.SetActive(false);
        }

        if (ClientManager != null)
        {
            ClientManager.StopClient();
            ClientManager.gameObject.SetActive(false);
        }

        if (udp != null)
        {
            udp.CloseUDP();
        }

        if (GameNetworkManager.Instance != null)
        {
            GameNetworkManager.Instance.ClearState();
        }

        ApplyGameplayUI(false);
        ApplyBallSimulation();

        if (showMenu)
        {
            MenuController menu = FindAnyObjectByType<MenuController>();
            menu?.ShowMainPanel();
        }
    }

    void ApplyGameplayUI(bool active)
    {
        if (GameGameplayUI != null)
        {
            GameGameplayUI.SetActive(active);
        }
    }

    public void RequestReplay()
    {
        if (ServerManager != null && ServerManager.gameObject.activeSelf)
        {
            ServerManager.ResetMatch(true);
        }
        else if (ClientManager != null && ClientManager.gameObject.activeSelf)
        {
            ClientManager.RequestReplay();
        }
    }

    void OnHostMigrationTriggered()
    {
        Debug.Log("[NetworkSession] Migrating to Host...");
        StartHost();
    }

    void ApplyBallSimulation()
    {
        PongBall ball = GameObject.FindAnyObjectByType<PongBall>(FindObjectsInactive.Include);
        if (ball == null)
        {
            return;
        }

        if (!IsNetworkSession)
        {
            ball.SetSimulate(true);
            return;
        }

        bool hostSimulate = ServerManager != null &&
                            ServerManager.gameObject.activeSelf &&
                            IsGameplayUnlocked &&
                            IsMatchActive;
        ball.SetSimulate(hostSimulate);
    }

    void OnDiscoveryMessage(string message, IPEndPoint source)
    {
        if (!message.StartsWith("B|", StringComparison.Ordinal))
        {
            return;
        }

        string[] parts = message.Split('|');
        if (parts.Length < 2)
        {
            return;
        }

        string name = parts[1];
        string ip = source.Address.ToString();
        float now = Time.unscaledTime;

        for (int i = 0; i < discoveredServers.Count; i++)
        {
            if (discoveredServers[i].IP == ip)
            {
                discoveredServers[i] = new PongClientManager.ServerInfo { Name = name, IP = ip, LastSeen = now };
                return;
            }
        }

        discoveredServers.Add(new PongClientManager.ServerInfo { Name = name, IP = ip, LastSeen = now });
        Debug.Log("[Discovery] Found server: " + name + " at " + ip);
    }
}
