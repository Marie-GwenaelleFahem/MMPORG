using System;
using System.Globalization;
using System.Net;
using UnityEngine;
public class PongClientManager : MonoBehaviour
{
    [Header("Network Settings")]
    public string ServerIP = "127.0.0.1";
    public int ServerPort = PongNetworkPorts.GamePort;
    public float SendInterval = 0.02f;
    public float HostTimeout = 5f;

    [Header("References")]
    public PongPaddle PaddleLeft;
    public PongPaddle PaddleRight;
    public PongBall Ball;

    public struct ServerInfo { public string Name; public string IP; public float LastSeen; }

    PongPlayer joinSide = PongPlayer.PlayerRight;

    UDPService udp;
    float lastInputSentAt;
    float lastHostPacketAt;
    bool matchActive;
    bool hostResponded;

    public bool IsMatchActive => matchActive;

    public bool IsConnectedToHost => udp != null && udp.IsBound && hostResponded && (
        !matchActive || Time.unscaledTime - lastHostPacketAt <= HostTimeout);

    public void SetJoinSide(PongPlayer side)
    {
        joinSide = side;
    }

    public void StartClient(string ip)
    {
        ServerIP = ip;
        udp = EnsureUdp();

        if (!udp.Bind(0, OnMessageReceived))
        {
            Debug.LogError("[Client] Failed to bind client port.");
            return;
        }

        matchActive = false;
        hostResponded = false;
        lastHostPacketAt = Time.unscaledTime;
        SendJoinPacket();
        Debug.Log("[Client] Joining " + ServerIP + ":" + ServerPort + " as " + joinSide);
    }

    public void StopClient()
    {
        if (udp != null)
        {
            udp.CloseUDP();
        }

        matchActive = false;
    }

    void Update()
    {
        if (udp == null || !udp.IsBound)
        {
            return;
        }

        if (matchActive)
        {
            bool inCountdown = PongNetworkSession.Instance != null && PongNetworkSession.Instance.IsCountdownActive;
            if (!inCountdown && Time.unscaledTime - lastHostPacketAt > HostTimeout)
            {
                Debug.Log("[Client] Host timed out!");
                matchActive = false;
                hostResponded = false;
                return;
            }

            SendInput();
        }
        else if (hostResponded || Time.unscaledTime - lastInputSentAt > 1f)
        {
            SendJoinPacket();
        }
    }

    void SendJoinPacket()
    {
        string sideToken = joinSide == PongPlayer.PlayerLeft ? "L" : "R";
        udp.SendToHost("J|" + sideToken + "\n", ServerIP, ServerPort);
        lastInputSentAt = Time.unscaledTime;
    }

    void SendInput()
    {
        if (Time.unscaledTime - lastInputSentAt < SendInterval)
        {
            return;
        }

        float axis = PongPaddleInput.ReadVerticalAxis();

        udp.SendToHost("I|" + axis.ToString(CultureInfo.InvariantCulture) + "\n", ServerIP, ServerPort);
        lastInputSentAt = Time.unscaledTime;
    }

    public void RequestReplay()
    {
        udp.SendToHost("R\n", ServerIP, ServerPort);
    }

    void OnMessageReceived(string message, IPEndPoint from)
    {
        lastHostPacketAt = Time.unscaledTime;

        if (message.StartsWith("A", StringComparison.Ordinal))
        {
            hostResponded = true;
            ApplyAssignmentFromHost(message);
            return;
        }

        if (message.StartsWith("R", StringComparison.Ordinal))
        {
            matchActive = true;
            ResetLocalRound();
            return;
        }

        if (message.StartsWith("S|", StringComparison.Ordinal))
        {
            matchActive = true;
            PongMatchState state = new PongMatchState();
            if (state.FromString(message))
            {
                ApplyState(state);
            }
        }
    }

    void ApplyAssignmentFromHost(string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length < 4 || parts[0] != "A")
        {
            return;
        }

        PongPlayer side = PongPlayer.PlayerRight;
        if (parts[1].Equals("L", StringComparison.OrdinalIgnoreCase))
        {
            side = PongPlayer.PlayerLeft;
        }

        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float share))
        {
            return;
        }

        if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
        {
            return;
        }

        if (PongNetworkSession.Instance != null)
        {
            PongNetworkSession.Instance.SetSideAssignment(side, share, count, true);
        }
    }

    void ResetLocalRound()
    {
        if (Ball != null)
        {
            Ball.ResetBall();
        }

        if (PongNetworkSession.Instance != null)
        {
            PongNetworkSession.Instance.BeginRoundCountdown();
        }
    }

    void ApplyState(PongMatchState state)
    {
        PaddleLeft.transform.position = new Vector3(PaddleLeft.transform.position.x, state.PaddleLeftY, PaddleLeft.transform.position.z);
        PaddleRight.transform.position = new Vector3(PaddleRight.transform.position.x, state.PaddleRightY, PaddleRight.transform.position.z);
        Ball.ApplyNetworkState(new Vector3(state.BallX, state.BallY, Ball.transform.position.z), state.BallState);
    }

    UDPService EnsureUdp()
    {
        if (udp == null)
        {
            udp = GetComponent<UDPService>();
        }

        if (udp == null)
        {
            udp = gameObject.AddComponent<UDPService>();
        }

        return udp;
    }
}
