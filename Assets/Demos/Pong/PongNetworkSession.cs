using System;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

    TCPServer server;
    TCPClient client;

    PongPaddle paddleLeft;
    PongPaddle paddleRight;
    PongBall ball;

    NetMode mode = NetMode.Offline;
    string remoteIp = "127.0.0.1";
    int port = DefaultPort;
    float lastSendAt;
    float remoteClientInput;
    bool hasClient;
    string statusMessage = "";

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

        StopSession();
        CacheSceneRefs();
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
            if (!HasSceneRefs())
            {
                return;
            }

            Vector3 rightPos = paddleRight.transform.position;
            rightPos.y = Mathf.Clamp(rightPos.y + remoteClientInput * paddleRight.Speed * Time.deltaTime, paddleRight.MinY, paddleRight.MaxY);
            paddleRight.transform.position = rightPos;
            BroadcastState();
        }
        else if (mode == NetMode.Client)
        {
            SendClientInput();
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

        GUILayout.BeginArea(new Rect(10, 10, 320, 220), GUI.skin.box);
        GUILayout.Label("Pong Self-Host TCP");
        GUILayout.Label("Mode: " + mode);

        if (mode == NetMode.Offline)
        {
            GUILayout.Label("Host IP (LAN): " + GetLocalIPv4());
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
            GUILayout.Label(mode == NetMode.Host
                ? ("Clients: " + server.ConnectionCount)
                : ("Connected: " + client.IsConnected));

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
            ConfigureForMode(NetMode.Offline);
            return;
        }

        hasClient = false;
        remoteClientInput = 0f;
        statusMessage = "Host actif sur " + GetLocalIPv4() + ":" + port;
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
            statusMessage = "Connexion refusee vers " + remoteIp + ":" + port
                + ". Verifie que le host est lance et le pare-feu ouvert.";
            ConfigureForMode(NetMode.Offline);
            return;
        }

        statusMessage = "Connecte a " + remoteIp + ":" + port;
        ConfigureForMode(NetMode.Client);
    }

    static string GetLocalIPv4()
    {
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
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    return address.Address.ToString();
                }
            }
        }

        return "IP introuvable";
    }

    void StopSession()
    {
        ShutdownNetwork();
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

        switch (mode)
        {
            case NetMode.Offline:
                paddleLeft.enabled = true;
                paddleRight.enabled = true;
                ball.SetSimulate(true);
                break;
            case NetMode.Host:
                paddleLeft.enabled = true;
                paddleRight.enabled = false;
                ball.SetSimulate(true);
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
        ball = GameObject.FindFirstObjectByType<PongBall>();
    }

    PongPaddle FindPaddle(PongPlayer player)
    {
        PongPaddle[] paddles = GameObject.FindObjectsByType<PongPaddle>(FindObjectsSortMode.None);
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

        if (Time.time - lastSendAt < SendInterval)
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
        lastSendAt = Time.time;
    }

    void BroadcastState()
    {
        if (!server.IsListening || !HasSceneRefs())
        {
            return;
        }

        if (Time.time - lastSendAt < SendInterval)
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
        lastSendAt = Time.time;
    }

    void OnServerMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        // First message from a client is the built-in welcome exchange.
        if (!hasClient && !message.StartsWith("I|", StringComparison.Ordinal))
        {
            hasClient = true;
            return;
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

    void OnClientMessage(string message)
    {
        if (string.IsNullOrEmpty(message) || !message.StartsWith("S|", StringComparison.Ordinal))
        {
            return;
        }

        string[] parts = message.Split('|');
        if (parts.Length < 6)
        {
            return;
        }

        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ballX) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ballY) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float leftY) ||
            !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float rightY) ||
            !int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int stateInt))
        {
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
