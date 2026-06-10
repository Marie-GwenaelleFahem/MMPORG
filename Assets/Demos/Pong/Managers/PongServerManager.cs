using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using UnityEngine;
using UnityEngine.InputSystem;

public class PongServerManager : MonoBehaviour
{
    class RemotePlayer
    {
        public IPEndPoint Endpoint;
        public PongPlayer Side;
        public float LastInput;
        public float LastPacketAt;
    }

    [Header("Network Settings")]
    public int ListenPort = 25000;
    public float StateSendInterval = 0.02f;
    public float ClientTimeout = 3f;

    [Header("References")]
    public PongPaddle PaddleLeft;
    public PongPaddle PaddleRight;
    public PongBall Ball;

    readonly Dictionary<string, RemotePlayer> remotePlayers = new Dictionary<string, RemotePlayer>();

    UDPService udp;
    float lastStateSentAt;
    float lastBeaconSentAt;
    bool matchActive;

    public bool IsMatchActive => matchActive;

    public void StartServer()
    {
        udp = GetComponentInParent<UDPService>();
        if (udp == null) udp = GetComponent<UDPService>();
        if (udp == null) udp = gameObject.AddComponent<UDPService>();

        if (!udp.Bind(ListenPort, OnMessageReceived))
        {
            Debug.LogError("[Server] Failed to bind to port " + ListenPort);
            return;
        }

        matchActive = false;
        remotePlayers.Clear();
        Debug.Log("[Server] Started listening on port " + ListenPort);
        RefreshHostSideAssignment(false);
    }

    public void StopServer()
    {
        if (udp != null)
        {
            udp.CloseUDP();
        }

        matchActive = false;
        remotePlayers.Clear();
        SetGameplayActive(false);
    }

    void Update()
    {
        if (udp == null || !udp.IsBound)
        {
            return;
        }

        SendDiscoveryBeacon();
        RemoveStalePlayers();
        UpdateMatchStatus();

        if (!matchActive)
        {
            return;
        }

        ApplyAggregatedPaddleMovement(PongPlayer.PlayerLeft, PaddleLeft);
        ApplyAggregatedPaddleMovement(PongPlayer.PlayerRight, PaddleRight);

        if (remotePlayers.Count > 0 && Time.unscaledTime - lastStateSentAt >= StateSendInterval)
        {
            BroadcastState();
            lastStateSentAt = Time.unscaledTime;
        }
    }

    void ApplyAggregatedPaddleMovement(PongPlayer side, PongPaddle paddle)
    {
        if (paddle == null)
        {
            return;
        }

        int count = GetPlayerCount(side);
        if (count <= 0)
        {
            return;
        }

        float axis = GetAggregatedAxis(side);
        float shareSpeed = paddle.Speed / count;
        Vector3 pos = paddle.transform.position;
        pos.y = Mathf.Clamp(
            pos.y + axis * shareSpeed * Time.unscaledDeltaTime,
            paddle.MinY,
            paddle.MaxY);
        paddle.transform.position = pos;
    }

    float GetAggregatedAxis(PongPlayer side)
    {
        float axis = 0f;

        if (side == PongPlayer.PlayerLeft)
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

        if (Keyboard.current.wKey.isPressed) axis += 1f;
        if (Keyboard.current.sKey.isPressed) axis -= 1f;
        return axis;
    }

    int GetPlayerCount(PongPlayer side)
    {
        int count = side == PongPlayer.PlayerLeft ? 1 : 0;
        foreach (RemotePlayer player in remotePlayers.Values)
        {
            if (player.Side == side)
            {
                count++;
            }
        }

        return count;
    }

    void SendDiscoveryBeacon()
    {
        if (Time.unscaledTime - lastBeaconSentAt < 1f)
        {
            return;
        }

        string message = "B|PongHost|" + ListenPort;
        udp.Broadcast(message, ListenPort);
        udp.SendToHost(message, "127.0.0.1", ListenPort);
        lastBeaconSentAt = Time.unscaledTime;
    }

    void UpdateMatchStatus()
    {
        bool opponentConnected = remotePlayers.Count > 0 && HasAnyActiveRemotePlayer();
        if (opponentConnected == matchActive)
        {
            return;
        }

        matchActive = opponentConnected;
        if (matchActive)
        {
            Debug.Log("[Server] Client(s) connected!");
            ResetMatch(true);
        }
        else
        {
            Debug.Log("[Server] No active clients.");
            SetGameplayActive(false);
        }
    }

    bool HasAnyActiveRemotePlayer()
    {
        foreach (RemotePlayer player in remotePlayers.Values)
        {
            if (Time.unscaledTime - player.LastPacketAt <= ClientTimeout)
            {
                return true;
            }
        }

        return false;
    }

    void RemoveStalePlayers()
    {
        var removedSides = new HashSet<PongPlayer>();
        var keysToRemove = new List<string>();

        foreach (KeyValuePair<string, RemotePlayer> entry in remotePlayers)
        {
            if (Time.unscaledTime - entry.Value.LastPacketAt > ClientTimeout)
            {
                removedSides.Add(entry.Value.Side);
                keysToRemove.Add(entry.Key);
            }
        }

        foreach (string key in keysToRemove)
        {
            remotePlayers.Remove(key);
        }

        foreach (PongPlayer side in removedSides)
        {
            RefreshSideAssignments(side);
        }

        if (remotePlayers.Count == 0 && matchActive)
        {
            matchActive = false;
            SetGameplayActive(false);
        }
    }

    public void ResetMatch(bool sendToClients)
    {
        if (Ball == null || PaddleLeft == null || PaddleRight == null)
        {
            return;
        }

        Ball.ResetBall();
        ResetPaddles();

        foreach (RemotePlayer player in remotePlayers.Values)
        {
            player.LastInput = 0f;
        }

        if (Ball != null)
        {
            Ball.SetSimulate(false);
        }

        RefreshSideAssignments(PongPlayer.PlayerLeft);
        RefreshSideAssignments(PongPlayer.PlayerRight);

        if (sendToClients)
        {
            BroadcastToAll("R\n");
            BroadcastState();
        }

        if (PongNetworkSession.Instance != null)
        {
            PongNetworkSession.Instance.BeginRoundCountdown();
        }
    }

    void ResetPaddles()
    {
        PaddleLeft.transform.position = new Vector3(PaddleLeft.transform.position.x, 0f, PaddleLeft.transform.position.z);
        PaddleRight.transform.position = new Vector3(PaddleRight.transform.position.x, 0f, PaddleRight.transform.position.z);
    }

    void SetGameplayActive(bool active)
    {
        if (Ball != null)
        {
            Ball.SetSimulate(active && PongNetworkSession.Instance != null && PongNetworkSession.Instance.IsGameplayUnlocked);
        }

        if (PaddleLeft != null) PaddleLeft.enabled = false;
        if (PaddleRight != null) PaddleRight.enabled = false;
    }

    void BroadcastState()
    {
        PongMatchState state = new PongMatchState
        {
            BallX = Ball.transform.position.x,
            BallY = Ball.transform.position.y,
            PaddleLeftY = PaddleLeft.transform.position.y,
            PaddleRightY = PaddleRight.transform.position.y,
            BallState = Ball.State
        };

        BroadcastToAll(state.ToString() + "\n");
    }

    void BroadcastToAll(string message)
    {
        foreach (RemotePlayer player in remotePlayers.Values)
        {
            udp.SendToEndpoint(message, player.Endpoint);
        }
    }

    static string EndpointKey(IPEndPoint endpoint)
    {
        return endpoint.Address + ":" + endpoint.Port;
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
                LastPacketAt = Time.unscaledTime
            };
            remotePlayers[key] = player;
        }

        player.Endpoint = from;
        player.LastPacketAt = Time.unscaledTime;
        return player;
    }

    void RefreshHostSideAssignment(bool showAnnouncement)
    {
        if (PongNetworkSession.Instance == null)
        {
            return;
        }

        int count = GetPlayerCount(PongPlayer.PlayerLeft);
        float share = count > 0 ? 1f / count : 1f;
        PongNetworkSession.Instance.SetSideAssignment(PongPlayer.PlayerLeft, share, count, showAnnouncement);
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
                udp.SendToEndpoint(message, player.Endpoint);
            }
        }

        if (side == PongPlayer.PlayerLeft)
        {
            RefreshHostSideAssignment(true);
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

        udp.SendToEndpoint(message, player.Endpoint);
    }

    void OnMessageReceived(string message, IPEndPoint from)
    {
        if (string.IsNullOrEmpty(message) || message.StartsWith("B|", StringComparison.Ordinal))
        {
            return;
        }

        if (message.StartsWith("R", StringComparison.Ordinal))
        {
            ResetMatch(true);
            return;
        }

        RemotePlayer player = GetOrCreateRemotePlayer(from);
        PongPlayer previousSide = player.Side;

        if (message.StartsWith("J", StringComparison.Ordinal))
        {
            PongPlayer requestedSide = PongPlayer.PlayerRight;
            string[] joinParts = message.Split('|');
            if (joinParts.Length >= 2)
            {
                TryParseSide(joinParts[1], out requestedSide);
            }

            player.Side = requestedSide;
            SendSideAssignment(player);

            if (!matchActive && remotePlayers.Count > 0)
            {
                matchActive = true;
                ResetMatch(true);
            }
            else if (previousSide != player.Side || GetPlayerCount(player.Side) > 1)
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
                ResetMatch(true);
            }

            string[] parts = message.Split('|');
            if (parts.Length >= 2 &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float axis))
            {
                player.LastInput = Mathf.Clamp(axis, -1f, 1f);
            }
        }
    }

    public void OnCountdownFinished()
    {
        SetGameplayActive(matchActive);
    }
}
