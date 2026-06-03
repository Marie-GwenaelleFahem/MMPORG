using System;
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

    static PongNetworkSession _instance;

    public static PongNetworkSession Instance => _instance;
    public bool IsNetworkSession => mode != NetMode.Offline;

    TCPServer server;
    TCPClient client;

    PongPaddle paddleLeft;
    PongPaddle paddleRight;
    PongBall ball;

    NetMode mode = NetMode.Offline;
    string remoteIp = "127.0.0.1";
    int port = DefaultPort;
    float lastInputSentAt;
    float lastStateSentAt;
    float remoteClientInput;
    bool hasClient;
    bool matchActive;
    string statusMessage = "";
    int statesReceived;

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
        server = gameObject.AddComponent<TCPServer>();
        client = gameObject.AddComponent<TCPClient>();
        server.ListenPort = port;
        client.DestinationPort = port;
        client.DestinationIP = remoteIp;
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
            if (!client.IsConnected)
            {
                if (matchActive)
                {
                    matchActive = false;
                    ApplyGameplayForCurrentMode();
                    statusMessage = "Deconnecte du host.";
                }
                return;
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
        rightPos.y = Mathf.Clamp(rightPos.y + remoteClientInput * paddleRight.Speed * Time.deltaTime, paddleRight.MinY, paddleRight.MaxY);
        paddleRight.transform.position = rightPos;
        BroadcastState();
    }

    void UpdateHostMatchState()
    {
        bool opponentConnected = server.IsListening && server.ConnectionCount >= 1;
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

        GUILayout.BeginArea(new Rect(10, 10, 360, 280), GUI.skin.box);
        GUILayout.Label("Pong Self-Host TCP");
        GUILayout.Label("Mode: " + mode);

        if (mode == NetMode.Offline)
        {
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
                GUILayout.Label("Ecoute: " + (server.IsListening ? "OUI" : "NON") + " | Port: " + port);
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

            GUILayout.Label(mode == NetMode.Host
                ? ("Clients: " + server.ConnectionCount)
                : ("Connected: " + client.IsConnected + " | Etats: " + statesReceived));

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
        server.ListenPort = port;
        server.OnConnectionMessage = "";
        bool ok = server.Listen(OnServerMessage);
        if (!ok)
        {
            statusMessage = "Echec Start Host sur port " + port + ": "
                + (string.IsNullOrEmpty(server.LastError) ? "port deja utilise ?" : server.LastError);
            ConfigureForMode(NetMode.Offline);
            return;
        }

        hasClient = false;
        remoteClientInput = 0f;
        matchActive = false;
        statusMessage = "Host pret. Donne une IP ci-dessus au client.";
        ConfigureForMode(NetMode.Host);
    }

    void StartClient()
    {
        ShutdownNetwork();
        client.DestinationIP = remoteIp;
        client.DestinationPort = port;
        bool ok = client.Connect(OnClientMessage);
        if (!ok)
        {
            statusMessage = "Connexion refusee vers " + remoteIp + ":" + port + ". "
                + "Le host ecoute ? Bonne IP ? Pare-feu ouvert ?";
            ConfigureForMode(NetMode.Offline);
            return;
        }

        statusMessage = "Connecte a " + remoteIp + ":" + port + " - en attente du host";
        matchActive = false;
        statesReceived = 0;
        ConfigureForMode(NetMode.Client);
    }

    public void RequestReplay()
    {
        if (mode == NetMode.Host)
        {
            ResetMatch(sendToClient: true);
            return;
        }

        if (mode == NetMode.Client && client.IsConnected)
        {
            client.SendTCPMessage("R\n");
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
            || (mode == NetMode.Host && server.ConnectionCount >= 1)
            || (mode == NetMode.Client && client.IsConnected);
        ApplyGameplayForCurrentMode();

        if (sendToClient && mode == NetMode.Host && server.IsListening)
        {
            server.BroadcastTCPMessage("R\n");
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
        var preferred = new System.Collections.Generic.List<string>();
        var others = new System.Collections.Generic.List<string>();

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
        ConfigureForMode(NetMode.Offline);
    }

    void ShutdownNetwork()
    {
        if (server != null && server.IsListening)
        {
            server.Close();
        }

        if (client != null && client.IsConnected)
        {
            client.Close();
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
        if (!client.IsConnected)
        {
            return;
        }

        if (Time.time - lastInputSentAt < SendInterval)
        {
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

        client.SendTCPMessage("I|" + axis.ToString(CultureInfo.InvariantCulture) + "\n");
        lastInputSentAt = Time.time;
    }

    void BroadcastState(bool force = false)
    {
        if (!server.IsListening || !HasSceneRefs() || !matchActive)
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

        server.BroadcastTCPMessage(message);
        lastStateSentAt = Time.time;
    }

    void OnServerMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (message.StartsWith("R", StringComparison.Ordinal))
        {
            ResetMatch(sendToClient: true);
            return;
        }

        if (!message.StartsWith("I|", StringComparison.Ordinal))
        {
            return;
        }

        hasClient = true;

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

    void OnClientMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        message = message.Trim('\r', ' ', '\t');

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
