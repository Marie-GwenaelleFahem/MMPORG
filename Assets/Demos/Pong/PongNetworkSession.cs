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

    const int DefaultPort = 25000;
    const float SendInterval = 0.02f;
    const float ClientTimeout = 3f;

    static PongNetworkSession _instance;

    public static PongNetworkSession Instance => _instance;
    public bool IsNetworkSession => mode != NetMode.Offline;

    UDPService udp;

    PongPaddle paddleLeft;
    PongPaddle paddleRight;
    PongBall ball;

    NetMode mode = NetMode.Offline;
    string remoteIp = "127.0.0.1";
    int port = DefaultPort;
    float lastInputSentAt;
    float lastStateSentAt;
    float lastClientPacketAt;
    float remoteClientInput;
    bool matchActive;
    string statusMessage = "";
    int statesReceived;
    int joinPacketsSent;
    bool hostResponded;

    IPEndPoint clientEndpoint;

    public bool CanMovePaddle(PongPlayer player)
    {
        if (mode == NetMode.Offline)
        {
            return true;
        }

        if (!matchActive)
        {
            return false;
        }

        return mode == NetMode.Host && player == PongPlayer.PlayerLeft;
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

        if (mode == NetMode.Host)
        {
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

        Vector3 rightPos = paddleRight.transform.position;
        rightPos.y = Mathf.Clamp(
            rightPos.y + remoteClientInput * paddleRight.Speed * Time.deltaTime,
            paddleRight.MinY,
            paddleRight.MaxY);
        paddleRight.transform.position = rightPos;
        BroadcastState();
    }

    void UpdateHostMatchState()
    {
        bool opponentConnected = udp.IsBound
            && clientEndpoint != null
            && Time.time - lastClientPacketAt <= ClientTimeout;

        if (opponentConnected == matchActive)
        {
            return;
        }

        matchActive = opponentConnected;
        if (matchActive)
        {
            statusMessage = "Adversaire connecte. C'est parti !";
            ResetMatch(sendToClient: true);
        }
        else
        {
            statusMessage = "En attente d'un client...";
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

    void OnGUI()
    {
        if (SceneManager.GetActiveScene().name != "Pong")
        {
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 420, 360), GUI.skin.box);
        GUILayout.Label("Pong Self-Host UDP");
        GUILayout.Label("Mode: " + mode);

        if (mode == NetMode.Offline)
        {
            GUILayout.Label("2 PC: meme Wi-Fi/box, IP LAN du host, pare-feu UDP " + DefaultPort + " entrant sur le host.");

            foreach (string ip in GetLocalIPv4Addresses())
            {
                GUILayout.Label("IP host possible: " + ip);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("IP", GUILayout.Width(30));
            remoteIp = GUILayout.TextField(remoteIp, GUILayout.Width(120));
            GUILayout.Label("Port", GUILayout.Width(40));
            int.TryParse(GUILayout.TextField(port.ToString(), GUILayout.Width(70)), out port);
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Label(statusMessage);
            }

            if (GUILayout.Button("Start Host"))
            {
                StartHost();
            }

            if (GUILayout.Button("Join Client"))
            {
                StartClient();
            }
        }
        else
        {
            if (mode == NetMode.Host)
            {
                GUILayout.Label("Ecoute UDP: " + (udp.IsBound ? "OUI" : "NON") + " | Port: " + port);
                GUILayout.Label("Pare-feu Windows: autoriser UDP entrant port " + port + " sur CE PC.");
                foreach (string ip in GetLocalIPv4Addresses())
                {
                    GUILayout.Label("IP a donner au client: " + ip);
                }
            }

            if (mode == NetMode.Host && !matchActive)
            {
                GUILayout.Label("En attente d'un client...");
            }
            else if (mode == NetMode.Client && !matchActive)
            {
                GUILayout.Label("Connecte. En attente du host...");
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Label(statusMessage);
            }

            if (mode == NetMode.Host)
            {
                GUILayout.Label("Client: " + (clientEndpoint != null ? "OUI" : "NON"));
                if (clientEndpoint != null)
                {
                    GUILayout.Label("Endpoint client: " + clientEndpoint);
                }
            }
            else
            {
                GUILayout.Label("Join envoyes: " + joinPacketsSent + " | Host repondu: " + (hostResponded ? "OUI" : "NON"));
                GUILayout.Label("Etats recus: " + statesReceived);
            }

            if (GUILayout.Button("Disconnect"))
            {
                StopSession();
            }
        }

        GUILayout.EndArea();
    }

    void StartHost()
    {
        ShutdownNetwork();
        clientEndpoint = null;
        lastClientPacketAt = 0f;

        bool ok = udp.Bind(port, OnHostMessage);
        if (!ok)
        {
            statusMessage = "Echec Start Host UDP port " + port + ": "
                + (string.IsNullOrEmpty(udp.LastError) ? "port deja utilise ?" : udp.LastError);
            ConfigureForMode(NetMode.Offline);
            return;
        }

        remoteClientInput = 0f;
        matchActive = false;
        statusMessage = "Host UDP pret. Donne une IP au client.";
        ConfigureForMode(NetMode.Host);
    }

    void StartClient()
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
        clientEndpoint = null;
        lastClientPacketAt = Time.time;
        joinPacketsSent = 0;
        hostResponded = false;
        statesReceived = 0;

        bool ok = udp.Bind(0, OnClientMessage);
        if (!ok)
        {
            statusMessage = "Echec bind UDP client: " + udp.LastError;
            ConfigureForMode(NetMode.Offline);
            return;
        }

        SendJoinPacket();
        statusMessage = "Client UDP vers " + remoteIp + ":" + port;
        matchActive = false;
        statesReceived = 0;
        ConfigureForMode(NetMode.Client);
    }

    void SendJoinPacket()
    {
        udp.Send("J\n", remoteIp, port);
        joinPacketsSent++;
        lastInputSentAt = Time.time;
    }

    public void RequestReplay()
    {
        if (mode == NetMode.Host)
        {
            ResetMatch(sendToClient: true);
            return;
        }

        if (mode == NetMode.Client && udp.IsBound)
        {
            udp.Send("R\n", remoteIp, port);
        }
    }

    void ResetMatch(bool sendToClient)
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
        remoteClientInput = 0f;
        matchActive = mode == NetMode.Offline
            || (mode == NetMode.Host && clientEndpoint != null)
            || (mode == NetMode.Client && udp.IsBound);
        ApplyGameplayForCurrentMode();

        if (sendToClient && mode == NetMode.Host && clientEndpoint != null)
        {
            udp.Send("R\n", clientEndpoint);
            BroadcastState(force: true);
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

    void StopSession()
    {
        ShutdownNetwork();
        matchActive = false;
        clientEndpoint = null;
        ConfigureForMode(NetMode.Offline);
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
                paddleLeft.enabled = matchActive;
                paddleRight.enabled = false;
                ball.SetSimulate(matchActive);
                break;
            case NetMode.Client:
                paddleLeft.enabled = false;
                paddleRight.enabled = false;
                ball.SetSimulate(false);
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
        if (!udp.IsBound || !HasSceneRefs() || !matchActive || clientEndpoint == null)
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

        udp.Send(message, clientEndpoint);
        lastStateSentAt = Time.time;
    }

    void RegisterClient(IPEndPoint from)
    {
        if (from == null)
        {
            return;
        }

        clientEndpoint = from;
        lastClientPacketAt = Time.time;
    }

    void OnHostMessage(string message, IPEndPoint from)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        RegisterClient(from);

        if (message.StartsWith("R", StringComparison.Ordinal))
        {
            ResetMatch(sendToClient: true);
            return;
        }

        if (message.StartsWith("J", StringComparison.Ordinal))
        {
            udp.Send("A\n", from);

            if (!matchActive)
            {
                matchActive = true;
                statusMessage = "Adversaire connecte. C'est parti !";
                ResetMatch(sendToClient: true);
            }

            return;
        }

        if (message.StartsWith("I|", StringComparison.Ordinal))
        {
            if (!matchActive)
            {
                matchActive = true;
                statusMessage = "Adversaire connecte. C'est parti !";
                ResetMatch(sendToClient: true);
            }
        }

        if (!message.StartsWith("I|", StringComparison.Ordinal))
        {
            return;
        }

        string[] parts = message.Split('|');
        if (parts.Length < 2)
        {
            return;
        }

        if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float axis))
        {
            remoteClientInput = Mathf.Clamp(axis, -1f, 1f);
        }
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
            hostResponded = true;
            statusMessage = "Host joignable. Attente demarrage partie...";
            return;
        }

        if (message.StartsWith("R", StringComparison.Ordinal))
        {
            matchActive = true;
            ResetMatch(sendToClient: false);
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
