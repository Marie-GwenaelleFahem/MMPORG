using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Globalization;

/*
* Manages the client-side logic for a Pong match.
* Responsible for: Discovering servers, joining a host, 
* sending player inputs, and applying state updates from the server.
*/
public class PongClientManager : MonoBehaviour
{
    [Header("Network Settings")]
    public string ServerIP = "127.0.0.1";
    public int ServerPort = 25000;
    public float SendInterval = 0.02f;
    public float HostTimeout = 3f;

    [Header("References")]
    public PongPaddle PaddleLeft;
    public PongPaddle PaddleRight;
    public PongBall Ball;

    public struct ServerInfo { public string Name; public string IP; public float LastSeen; }
    private List<ServerInfo> discoveredServers = new List<ServerInfo>();
    public List<ServerInfo> DiscoveredServers => new List<ServerInfo>(); // Placeholder for compatibility

    private UDPService udp;
    private float lastInputSentAt;
    private float lastHostPacketAt;
    private bool matchActive = false;
    private bool hostResponded = false;

    public bool IsMatchActive => matchActive;

    public void StartClient(string ip)
    {
        ServerIP = ip;
        udp = GetComponentInParent<UDPService>();
        if (udp == null) udp = GetComponent<UDPService>();
        if (udp == null) udp = gameObject.AddComponent<UDPService>();

        bool ok = udp.Bind(0, OnMessageReceived);
        if (!ok)
        {
            Debug.LogError("[Client] Failed to bind client port.");
            return;
        }

        matchActive = false;
        hostResponded = false;
        lastHostPacketAt = Time.time;
        SendJoinPacket();
        Debug.Log($"[Client] Attempting to join {ServerIP}:{ServerPort}");
    }

    public void StopClient()
    {
        if (udp != null) udp.CloseUDP();
        matchActive = false;
    }

    private void Update()
    {
        if (udp == null || !udp.IsBound) return;

        if (matchActive)
        {
            if (Time.time - lastHostPacketAt > HostTimeout)
            {
                Debug.Log("[Client] Host timed out!");
                HandleHostMigration();
                return;
            }

            SendInput();
        }
        else if (hostResponded || Time.time - lastInputSentAt > 1.0f)
        {
            // Keep trying to join until match is active
            SendJoinPacket();
        }
    }

    private void SendJoinPacket()
    {
        udp.SendToHost("J\n", ServerIP, ServerPort);
        lastInputSentAt = Time.time;
    }

    private void SendInput()
    {
        if (Time.time - lastInputSentAt < SendInterval) return;

        float axis = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.isPressed) axis += 1f;
            if (Keyboard.current.downArrowKey.isPressed) axis -= 1f;
        }

        udp.SendToHost("I|" + axis.ToString(CultureInfo.InvariantCulture) + "\n", ServerIP, ServerPort);
        lastInputSentAt = Time.time;
    }

    public void RequestReplay()
    {
        udp.SendToHost("R\n", ServerIP, ServerPort);
    }

    private void HandleHostMigration()
    {
        StopClient();
        if (GameNetworkManager.Instance != null)
        {
            GameNetworkManager.Instance.SetHostMode(true);
            // The GameNetworkManager or a wrapper should now start the ServerManager
            SendMessageUpwards("OnHostMigrationTriggered", SendMessageOptions.DontRequireReceiver);
        }
    }

    private void OnMessageReceived(string message, IPEndPoint from)
    {
        lastHostPacketAt = Time.time;

        if (message.StartsWith("A", System.StringComparison.Ordinal))
        {
            hostResponded = true;
            return;
        }

        if (message.StartsWith("R", System.StringComparison.Ordinal))
        {
            matchActive = true;
            Ball.ResetBall();
            return;
        }

        if (message.StartsWith("S|", System.StringComparison.Ordinal))
        {
            matchActive = true;
            PongMatchState state = new PongMatchState();
            if (state.FromString(message))
            {
                ApplyState(state);
            }
        }
    }

    private void ApplyState(PongMatchState state)
    {
        PaddleLeft.transform.position = new Vector3(PaddleLeft.transform.position.x, state.PaddleLeftY, PaddleLeft.transform.position.z);
        PaddleRight.transform.position = new Vector3(PaddleRight.transform.position.x, state.PaddleRightY, PaddleRight.transform.position.z);
        Ball.ApplyNetworkState(new Vector3(state.BallX, state.BallY, Ball.transform.position.z), state.BallState);
    }
}
