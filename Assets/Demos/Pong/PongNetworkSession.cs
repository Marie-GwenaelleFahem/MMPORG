using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-200)]
public class PongNetworkSession : MonoBehaviour
{
    enum NetMode
    {
        Offline,
        Host,
        Client
    }

    class RemotePlayer
    {
        public IPEndPoint Endpoint;
        public PongPlayer Side;
        public float LastInput;
        public float LastPacketAt;
    }

    const int DefaultPort = 25000;
    const float SendInterval = 0.02f;
    const float ClientTimeout = 3f;
    const float DiscoveryBeaconInterval = 1f;
    const float DiscoveryStaleTime = 3f;
    const float CountdownStepDuration = 1f;
    const float CountdownGoDuration = 0.7f;

    static PongNetworkSession _instance;

    public static PongNetworkSession Instance => _instance;
    public bool IsNetworkSession => mode != NetMode.Offline;
    public bool IsInMenu => mode == NetMode.Offline;
    public bool IsMatchActive => matchActive;
    public bool IsCountdownActive => countdownActive;
    public string CountdownText
    {
        get
        {
            if (!countdownActive)
            {
                return "";
            }

            return countdownShowingGo ? "GO!" : countdownNumber.ToString(CultureInfo.InvariantCulture);
        }
    }
    public PongPlayer LocalSide => localSide;
    public float LocalSpeedShare => localSpeedShare;
    public int LocalSidePlayerCount => localSidePlayerCount;
    public List<PongClientManager.ServerInfo> DiscoveredServers => discoveredServers;

    UDPService udp;

    PongPaddle paddleLeft;
    PongPaddle paddleRight;
    PongBall ball;

    readonly Dictionary<string, RemotePlayer> remotePlayers = new Dictionary<string, RemotePlayer>();
    readonly List<PongClientManager.ServerInfo> discoveredServers = new List<PongClientManager.ServerInfo>();

    NetMode mode = NetMode.Offline;
    string remoteIp = "127.0.0.1";
    int port = DefaultPort;
    float lastInputSentAt;
    float lastStateSentAt;
    float lastClientPacketAt;
    float lastBeaconSentAt;
    bool matchActive;
    bool countdownActive;
    bool countdownShowingGo;
    int countdownNumber = 3;
    float countdownNextStepAt;
    string statusMessage = "";
    int statesReceived;
    int joinPacketsSent;
    bool hostResponded;

    PongPlayer joinSide = PongPlayer.PlayerRight;
    PongPlayer localSide = PongPlayer.PlayerLeft;
    float localSpeedShare = 1f;
    int localSidePlayerCount = 1;

    string pendingAnnouncementPrimary = "";
    string pendingAnnouncementSecondary = "";
    bool hasPendingAnnouncement;

    public bool CanMovePaddle(PongPlayer player)
    {
        if (mode == NetMode.Offline)
        {
            return true;
        }

        return false;
    }

    public float GetPaddleSpeedMultiplier(PongPlayer player)
    {
        if (mode == NetMode.Offline)
        {
            return 1f;
        }

        if (player != localSide)
        {
            return 0f;
        }

        return localSpeedShare;
    }

    public bool TryConsumeSideAnnouncement(out string primary, out string secondary)
    {
        if (!hasPendingAnnouncement)
        {
            primary = "";
            secondary = "";
            return false;
        }

        primary = pendingAnnouncementPrimary;
        secondary = pendingAnnouncementSecondary;
        hasPendingAnnouncement = false;
        return true;
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

        if (_instance != null)
        {
            return;
        }

        GameObject go = new GameObject("PongNetworkSession");
        _instance = go.AddComponent<PongNetworkSession>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        udp = gameObject.AddComponent<UDPService>();
        SceneManager.sceneLoaded += OnSceneLoaded;
        CacheSceneRefs();
        ConfigureForMode(NetMode.Offline);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this)
        {
            _instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (scene.name != "Pong")
        {
            return;
        }

        CacheSceneRefs();
        ApplyGameplayForCurrentMode();
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name != "Pong")
        {
            return;
        }

        if (paddleLeft == null || paddleRight == null || ball == null)
        {
            CacheSceneRefs();
        }

        UpdateCountdown();

        if (mode == NetMode.Offline)
        {
            EnsureDiscoveryListener();
            PruneStaleDiscoveredServers();
        }
        else if (mode == NetMode.Host)
        {
            SendDiscoveryBeacon();
            RemoveStaleRemotePlayers();
            UpdateHostMatchState();
        }
        else if (mode == NetMode.Client)
        {
            if (!udp.IsBound)
            {
                return;
            }

            if (matchActive && Time.time - lastClientPacketAt > ClientTimeout)
            {
                matchActive = false;
                ApplyGameplayForCurrentMode();
                statusMessage = "Deconnecte du host.";
            }

            SendClientInput();
        }
    }

    void LateUpdate()
    {
        if (SceneManager.GetActiveScene().name != "Pong")
        {
            return;
        }

        if (paddleLeft == null || paddleRight == null || ball == null)
        {
            CacheSceneRefs();
        }

        if (mode != NetMode.Host || !matchActive || !HasSceneRefs())
        {
            return;
        }

        ApplyAggregatedPaddleMovement(PongPlayer.PlayerLeft, paddleLeft);
        ApplyAggregatedPaddleMovement(PongPlayer.PlayerRight, paddleRight);
        BroadcastState();
    }

    void ApplyAggregatedPaddleMovement(PongPlayer side, PongPaddle paddle)
    {
        int count = GetPlayerCount(side);
        if (count <= 0 || paddle == null)
        {
            return;
        }

        float axis = GetAggregatedAxis(side);
        float shareSpeed = paddle.Speed / count;
        Vector3 pos = paddle.transform.position;
        pos.y = Mathf.Clamp(
            pos.y + axis * shareSpeed * Time.deltaTime,
            paddle.MinY,
            paddle.MaxY);
        paddle.transform.position = pos;
    }

    float GetAggregatedAxis(PongPlayer side)
    {
        float axis = 0f;

        if (mode == NetMode.Host && side == PongPlayer.PlayerLeft)
        {
            axis += ReadHostLeftInput();
        }

        foreach (RemotePlayer player in remotePlayers.Values)
        {
            if (player.Side == side)
            {
                axis += player.LastInput;
            }
        }

        return axis;
    }

    static float ReadHostLeftInput()
    {
        float axis = 0f;
        if (Keyboard.current == null)
        {
            return axis;
        }

        if (Keyboard.current.wKey.isPressed)
        {
            axis += 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            axis -= 1f;
        }

        return axis;
    }

    int GetPlayerCount(PongPlayer side)
    {
        int count = 0;

        if (mode == NetMode.Host && side == PongPlayer.PlayerLeft)
        {
            count++;
        }

        foreach (RemotePlayer player in remotePlayers.Values)
        {
            if (player.Side == side)
            {
                count++;
            }
        }

        return count;
    }

    void UpdateHostMatchState()
    {
        bool opponentConnected = udp.IsBound && remotePlayers.Count > 0 && HasAnyActiveRemotePlayer();

        if (opponentConnected == matchActive)
        {
            return;
        }

        matchActive = opponentConnected;
        if (matchActive)
        {
            statusMessage = "Adversaire(s) connecte(s). C'est parti !";
            ResetMatch(sendToAll: true);
        }
        else
        {
            statusMessage = "En attente de joueurs...";
            ApplyGameplayForCurrentMode();
        }
    }

    bool HasAnyActiveRemotePlayer()
    {
        foreach (RemotePlayer player in remotePlayers.Values)
        {
            if (Time.time - player.LastPacketAt <= ClientTimeout)
            {
                return true;
            }
        }

        return false;
    }

    void RemoveStaleRemotePlayers()
    {
        var removedSides = new HashSet<PongPlayer>();
        var keysToRemove = new List<string>();

        foreach (KeyValuePair<string, RemotePlayer> entry in remotePlayers)
        {
            if (Time.time - entry.Value.LastPacketAt > ClientTimeout)
            {
                removedSides.Add(entry.Value.Side);
                keysToRemove.Add(entry.Key);
            }
        }

        if (keysToRemove.Count == 0)
        {
            return;
        }

        foreach (string key in keysToRemove)
        {
            remotePlayers.Remove(key);
        }

        foreach (PongPlayer side in removedSides)
        {
            RefreshSideAssignments(side);
        }

        if (remotePlayers.Count == 0)
        {
            matchActive = false;
            statusMessage = "En attente de joueurs...";
            ApplyGameplayForCurrentMode();
        }
    }

    bool HasSceneRefs()
    {
        return paddleLeft != null && paddleRight != null && ball != null;
    }

    void OnDisable()
    {
        ShutdownNetwork();
    }

    public void SetJoinSide(PongPlayer side)
    {
        joinSide = side;
    }

    public void StartHost()
    {
        ShutdownNetwork();
        remotePlayers.Clear();
        discoveredServers.Clear();
        lastClientPacketAt = 0f;

        bool ok = udp.Bind(port, OnHostMessage);
        if (!ok)
        {
            statusMessage = "Echec Start Host UDP port " + port + ": "
                + (string.IsNullOrEmpty(udp.LastError) ? "port deja utilise ?" : udp.LastError);
            ConfigureForMode(NetMode.Offline);
            return;
        }

        localSide = PongPlayer.PlayerLeft;
        matchActive = false;
        statusMessage = "Host pret. Plusieurs joueurs peuvent rejoindre un cote.";
        ConfigureForMode(NetMode.Host);
        RefreshSideAssignments(PongPlayer.PlayerLeft);
    }

    public void StartClient(string ip)
    {
        remoteIp = ip != null ? ip.Trim() : "";
        StartClientInternal();
    }

    void StartClientInternal()
    {
        remoteIp = remoteIp != null ? remoteIp.Trim() : "";

        if (!IPAddress.TryParse(remoteIp, out _))
        {
            statusMessage = "IP invalide: " + remoteIp;
            return;
        }

        if (remoteIp == "127.0.0.1")
        {
            statusMessage = "127.0.0.1 = local seulement. Sur 2 PC, mets l'IP LAN du host.";
        }

        ShutdownNetwork();
        remotePlayers.Clear();
        lastClientPacketAt = Time.time;
        joinPacketsSent = 0;
        hostResponded = false;
        statesReceived = 0;
        localSide = joinSide;

        bool ok = udp.Bind(0, OnClientMessage);
        if (!ok)
        {
            statusMessage = "Echec bind UDP client: " + udp.LastError;
            ConfigureForMode(NetMode.Offline);
            return;
        }

        SendJoinPacket();
        statusMessage = "Client UDP vers " + remoteIp + ":" + port + " (" + SideLabel(joinSide) + ")";
        matchActive = false;
        ConfigureForMode(NetMode.Client);
    }

    void SendJoinPacket()
    {
        string sideToken = joinSide == PongPlayer.PlayerLeft ? "L" : "R";
        udp.Send("J|" + sideToken + "\n", remoteIp, port);
        joinPacketsSent++;
        lastInputSentAt = Time.time;
    }

    public void RequestReplay()
    {
        if (mode == NetMode.Host)
        {
            ResetMatch(sendToAll: true);
            return;
        }

        if (mode == NetMode.Client && udp.IsBound)
        {
            udp.Send("R\n", remoteIp, port);
        }
    }

    void ResetMatch(bool sendToAll)
    {
        if (!HasSceneRefs())
        {
            CacheSceneRefs();
            if (!HasSceneRefs())
            {
                return;
            }
        }

        ball.ResetBall();
        ResetPaddlePositions();

        foreach (RemotePlayer player in remotePlayers.Values)
        {
            player.LastInput = 0f;
        }

        matchActive = mode == NetMode.Offline
            || (mode == NetMode.Host && remotePlayers.Count > 0)
            || (mode == NetMode.Client && udp.IsBound);

        ApplyGameplayForCurrentMode();

        if (mode == NetMode.Host)
        {
            RefreshSideAssignments(PongPlayer.PlayerLeft);
            RefreshSideAssignments(PongPlayer.PlayerRight);
        }

        if (sendToAll && mode == NetMode.Host)
        {
            BroadcastToAllRemotes("R\n");
            BroadcastState(force: true);
        }

        if (matchActive)
        {
            NotifyLocalSideAssignment();
            BeginRoundCountdown();
        }
    }

    void BeginRoundCountdown()
    {
        if (!IsNetworkSession)
        {
            return;
        }

        countdownActive = true;
        countdownShowingGo = false;
        countdownNumber = 3;
        countdownNextStepAt = Time.time + CountdownStepDuration;
        ApplyBallSimulation();
    }

    void UpdateCountdown()
    {
        if (!countdownActive || Time.time < countdownNextStepAt)
        {
            return;
        }

        if (countdownShowingGo)
        {
            countdownActive = false;
            countdownShowingGo = false;
            ApplyBallSimulation();
            return;
        }

        countdownNumber--;
        if (countdownNumber <= 0)
        {
            countdownShowingGo = true;
            countdownNextStepAt = Time.time + CountdownGoDuration;
        }
        else
        {
            countdownNextStepAt = Time.time + CountdownStepDuration;
        }
    }

    void ApplyBallSimulation()
    {
        if (!HasSceneRefs())
        {
            return;
        }

        switch (mode)
        {
            case NetMode.Offline:
                ball.SetSimulate(true);
                break;
            case NetMode.Host:
                ball.SetSimulate(matchActive && !countdownActive);
                break;
            case NetMode.Client:
                ball.SetSimulate(false);
                break;
        }
    }

    void ResetPaddlePositions()
    {
        Vector3 leftPos = paddleLeft.transform.position;
        leftPos.y = 0f;
        paddleLeft.transform.position = leftPos;

        Vector3 rightPos = paddleRight.transform.position;
        rightPos.y = 0f;
        paddleRight.transform.position = rightPos;
    }

    static string[] GetLocalIPv4Addresses()
    {
        var preferred = new List<string>();
        var others = new List<string>();

        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (UnicastIPAddressInformation address in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                string ip = address.Address.ToString();
                if (ip.StartsWith("169.254."))
                {
                    continue;
                }

                if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172."))
                {
                    preferred.Add(ip);
                }
                else
                {
                    others.Add(ip);
                }
            }
        }

        if (preferred.Count > 0)
        {
            return preferred.ToArray();
        }

        if (others.Count > 0)
        {
            return others.ToArray();
        }

        return new[] { "IP introuvable" };
    }

    public void StopSession(bool showMenu = false)
    {
        countdownActive = false;
        countdownShowingGo = false;
        ShutdownNetwork();
        matchActive = false;
        remotePlayers.Clear();
        discoveredServers.Clear();
        ConfigureForMode(NetMode.Offline);

        if (showMenu)
        {
            MenuController menu = UnityEngine.Object.FindAnyObjectByType<MenuController>();
            menu?.ShowMainPanel();
        }
    }

    void ShutdownNetwork()
    {
        if (udp != null && udp.IsBound)
        {
            udp.Close();
        }
    }

    void ConfigureForMode(NetMode nextMode)
    {
        CacheSceneRefs();
        if (!HasSceneRefs())
        {
            Debug.LogWarning("PongNetworkSession: scene refs missing, staying offline.");
            mode = NetMode.Offline;
            return;
        }

        mode = nextMode;

        if (mode == NetMode.Host || mode == NetMode.Client)
        {
            matchActive = false;
        }
        else
        {
            matchActive = true;
        }

        ApplyGameplayForCurrentMode();

        if (mode == NetMode.Offline)
        {
            EnsureDiscoveryListener();
        }
    }

    void EnsureDiscoveryListener()
    {
        if (mode != NetMode.Offline || udp == null || udp.IsBound)
        {
            return;
        }

        udp.Bind(port, OnDiscoveryMessage);
    }

    void OnDiscoveryMessage(string message, IPEndPoint from)
    {
        if (string.IsNullOrEmpty(message) || !message.StartsWith("B|", StringComparison.Ordinal))
        {
            return;
        }

        string[] parts = message.Split('|');
        if (parts.Length < 2)
        {
            return;
        }

        string serverName = parts[1];
        string hostIp = from.Address.ToString();
        float now = Time.time;

        for (int i = 0; i < discoveredServers.Count; i++)
        {
            if (discoveredServers[i].IP == hostIp)
            {
                discoveredServers[i] = new PongClientManager.ServerInfo
                {
                    Name = serverName,
                    IP = hostIp,
                    LastSeen = now
                };
                return;
            }
        }

        discoveredServers.Add(new PongClientManager.ServerInfo
        {
            Name = serverName,
            IP = hostIp,
            LastSeen = now
        });
    }

    void PruneStaleDiscoveredServers()
    {
        discoveredServers.RemoveAll(server => Time.time - server.LastSeen > DiscoveryStaleTime);
    }

    void SendDiscoveryBeacon()
    {
        if (!udp.IsBound || Time.time - lastBeaconSentAt < DiscoveryBeaconInterval)
        {
            return;
        }

        string message = "B|PongHost|" + port;
        udp.Broadcast(message, port);
        udp.Send(message, "127.0.0.1", port);
        lastBeaconSentAt = Time.time;
    }

    void ApplyGameplayForCurrentMode()
    {
        if (!HasSceneRefs())
        {
            return;
        }

        switch (mode)
        {
            case NetMode.Offline:
                paddleLeft.enabled = true;
                paddleRight.enabled = true;
                ball.SetSimulate(true);
                break;
            case NetMode.Host:
                paddleLeft.enabled = false;
                paddleRight.enabled = false;
                ApplyBallSimulation();
                break;
            case NetMode.Client:
                paddleLeft.enabled = false;
                paddleRight.enabled = false;
                ApplyBallSimulation();
                break;
        }
    }

    void CacheSceneRefs()
    {
        paddleLeft = FindPaddle(PongPlayer.PlayerLeft);
        paddleRight = FindPaddle(PongPlayer.PlayerRight);
        ball = GameObject.FindFirstObjectByType<PongBall>(FindObjectsInactive.Include);
    }

    PongPaddle FindPaddle(PongPlayer player)
    {
        PongPaddle[] paddles = GameObject.FindObjectsByType<PongPaddle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PongPaddle paddle in paddles)
        {
            if (paddle.Player == player)
            {
                return paddle;
            }
        }

        return null;
    }

    void SendClientInput()
    {
        if (!udp.IsBound)
        {
            return;
        }

        if (Time.time - lastInputSentAt < SendInterval)
        {
            return;
        }

        if (!matchActive)
        {
            SendJoinPacket();
            return;
        }

        float axis = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.isPressed)
            {
                axis += 1f;
            }

            if (Keyboard.current.downArrowKey.isPressed)
            {
                axis -= 1f;
            }
        }

        udp.Send("I|" + axis.ToString(CultureInfo.InvariantCulture) + "\n", remoteIp, port);
        lastInputSentAt = Time.time;
    }

    void BroadcastState(bool force = false)
    {
        if (!udp.IsBound || !HasSceneRefs() || !matchActive || remotePlayers.Count == 0)
        {
            return;
        }

        if (!force && Time.time - lastStateSentAt < SendInterval)
        {
            return;
        }

        string message = string.Join("|", new[]
        {
            "S",
            ball.transform.position.x.ToString("F4", CultureInfo.InvariantCulture),
            ball.transform.position.y.ToString("F4", CultureInfo.InvariantCulture),
            paddleLeft.transform.position.y.ToString("F4", CultureInfo.InvariantCulture),
            paddleRight.transform.position.y.ToString("F4", CultureInfo.InvariantCulture),
            ((int)ball.State).ToString(CultureInfo.InvariantCulture)
        }) + "\n";

        BroadcastToAllRemotes(message);
        lastStateSentAt = Time.time;
    }

    void BroadcastToAllRemotes(string message)
    {
        foreach (RemotePlayer player in remotePlayers.Values)
        {
            udp.Send(message, player.Endpoint);
        }
    }

    static string EndpointKey(IPEndPoint endpoint)
    {
        return endpoint.Address.ToString() + ":" + endpoint.Port;
    }

    static bool TryParseSide(string token, out PongPlayer side)
    {
        side = PongPlayer.PlayerRight;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        if (token.Equals("L", StringComparison.OrdinalIgnoreCase))
        {
            side = PongPlayer.PlayerLeft;
            return true;
        }

        if (token.Equals("R", StringComparison.OrdinalIgnoreCase))
        {
            side = PongPlayer.PlayerRight;
            return true;
        }

        return false;
    }

    static string SideLabel(PongPlayer side)
    {
        return side == PongPlayer.PlayerLeft ? "GAUCHE" : "DROITE";
    }

    RemotePlayer GetOrCreateRemotePlayer(IPEndPoint from)
    {
        string key = EndpointKey(from);
        if (!remotePlayers.TryGetValue(key, out RemotePlayer player))
        {
            player = new RemotePlayer
            {
                Endpoint = from,
                Side = PongPlayer.PlayerRight,
                LastInput = 0f,
                LastPacketAt = Time.time
            };
            remotePlayers[key] = player;
        }

        player.Endpoint = from;
        player.LastPacketAt = Time.time;
        lastClientPacketAt = Time.time;
        return player;
    }

    void RefreshSideAssignments(PongPlayer side)
    {
        int count = GetPlayerCount(side);
        if (count <= 0)
        {
            return;
        }

        float share = 1f / count;
        string sideToken = side == PongPlayer.PlayerLeft ? "L" : "R";
        string message = string.Join("|", new[]
        {
            "A",
            sideToken,
            share.ToString(CultureInfo.InvariantCulture),
            count.ToString(CultureInfo.InvariantCulture)
        }) + "\n";

        foreach (RemotePlayer player in remotePlayers.Values)
        {
            if (player.Side == side)
            {
                udp.Send(message, player.Endpoint);
            }
        }

        if (mode == NetMode.Host && side == localSide)
        {
            localSpeedShare = share;
            localSidePlayerCount = count;
            NotifyLocalSideAssignment();
        }
    }

    void ApplyAssignmentFromHost(string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length < 4 || parts[0] != "A")
        {
            return;
        }

        if (!TryParseSide(parts[1], out PongPlayer side))
        {
            return;
        }

        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float share))
        {
            return;
        }

        if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
        {
            return;
        }

        localSide = side;
        localSpeedShare = share;
        localSidePlayerCount = count;
        hostResponded = true;
        statusMessage = "Assigne " + SideLabel(localSide);
        NotifyLocalSideAssignment();
    }

    void NotifyLocalSideAssignment()
    {
        int percent = Mathf.RoundToInt(localSpeedShare * 100f);
        pendingAnnouncementPrimary = "Tu es a " + SideLabel(localSide);
        pendingAnnouncementSecondary = localSidePlayerCount + " joueur(s) sur ce cote - " + percent + "% de la vitesse";
        hasPendingAnnouncement = true;
    }

    void OnHostMessage(string message, IPEndPoint from)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (message.StartsWith("B|", StringComparison.Ordinal))
        {
            return;
        }

        if (message.StartsWith("R", StringComparison.Ordinal))
        {
            ResetMatch(sendToAll: true);
            return;
        }

        RemotePlayer player = GetOrCreateRemotePlayer(from);
        PongPlayer previousSide = player.Side;
        bool sideChanged = false;

        if (message.StartsWith("J", StringComparison.Ordinal))
        {
            PongPlayer requestedSide = PongPlayer.PlayerRight;
            string[] joinParts = message.Split('|');
            if (joinParts.Length >= 2)
            {
                TryParseSide(joinParts[1], out requestedSide);
            }

            sideChanged = player.Side != requestedSide;
            player.Side = requestedSide;
            SendSideAssignment(player);

            if (!matchActive && remotePlayers.Count > 0)
            {
                matchActive = true;
                statusMessage = "Joueur(s) connecte(s). C'est parti !";
                ResetMatch(sendToAll: true);
            }
            else if (sideChanged || GetPlayerCount(player.Side) > 1)
            {
                RefreshSideAssignments(player.Side);
                if (previousSide != player.Side)
                {
                    RefreshSideAssignments(previousSide);
                }
            }

            return;
        }

        if (message.StartsWith("I|", StringComparison.Ordinal))
        {
            if (!matchActive && remotePlayers.Count > 0)
            {
                matchActive = true;
                statusMessage = "Joueur(s) connecte(s). C'est parti !";
                ResetMatch(sendToAll: true);
            }

            string[] parts = message.Split('|');
            if (parts.Length >= 2 &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float axis))
            {
                player.LastInput = Mathf.Clamp(axis, -1f, 1f);
            }
        }
    }

    void SendSideAssignment(RemotePlayer player)
    {
        int count = GetPlayerCount(player.Side);
        if (count <= 0)
        {
            count = 1;
        }

        float share = 1f / count;
        string sideToken = player.Side == PongPlayer.PlayerLeft ? "L" : "R";
        string message = string.Join("|", new[]
        {
            "A",
            sideToken,
            share.ToString(CultureInfo.InvariantCulture),
            count.ToString(CultureInfo.InvariantCulture)
        }) + "\n";

        udp.Send(message, player.Endpoint);
    }

    void OnClientMessage(string message, IPEndPoint from)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        lastClientPacketAt = Time.time;

        if (message.StartsWith("A", StringComparison.Ordinal))
        {
            ApplyAssignmentFromHost(message);
            return;
        }

        if (message.StartsWith("R", StringComparison.Ordinal))
        {
            matchActive = true;
            ResetMatch(sendToAll: false);
            return;
        }

        string[] parts = message.Split('|');
        if (parts.Length < 6 || parts[0] != "S")
        {
            return;
        }

        matchActive = true;
        statesReceived++;
        statusMessage = "En jeu";

        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ballX) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ballY) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float leftY) ||
            !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float rightY) ||
            !int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int stateInt))
        {
            Debug.LogWarning("PongNetworkSession: invalid state message: " + message);
            return;
        }

        if (paddleLeft == null || paddleRight == null || ball == null)
        {
            CacheSceneRefs();
            if (!HasSceneRefs())
            {
                return;
            }
        }

        Vector3 leftPos = paddleLeft.transform.position;
        leftPos.y = leftY;
        paddleLeft.transform.position = leftPos;

        Vector3 rightPos = paddleRight.transform.position;
        rightPos.y = rightY;
        paddleRight.transform.position = rightPos;

        PongBallState state = Enum.IsDefined(typeof(PongBallState), stateInt)
            ? (PongBallState)stateInt
            : PongBallState.Playing;
        ball.ApplyNetworkState(new Vector3(ballX, ballY, ball.transform.position.z), state);
    }
}
