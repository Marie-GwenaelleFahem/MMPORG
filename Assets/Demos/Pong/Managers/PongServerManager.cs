using System.Collections.Generic;
using System.Net;
using UnityEngine;
using System.Globalization;

/* Manages the server-side logic for a Pong match.
 * Responsible for: Broadcasting beacons, accepting connections, 
 * processing client inputs, and broadcasting the match state.
*/
public class PongServerManager : MonoBehaviour
{
    [Header("Network Settings")]
    public int ListenPort = 25000;
    public float StateSendInterval = 0.02f;
    public float ClientTimeout = 3f;

    [Header("References")]
    public PongPaddle PaddleLeft;
    public PongPaddle PaddleRight;
    public PongBall Ball;

    private UDPService udp;
    private IPEndPoint clientEndpoint;
    private float lastStateSentAt;
    private float lastClientPacketAt;
    private float lastBeaconSentAt;
    private float remoteClientInput;
    private bool matchActive = false;
    private float countdownRemaining = 0f;

    public bool IsMatchActive => matchActive;
    public bool IsCountingDown => countdownRemaining > 0;
    public float CountdownRemaining => countdownRemaining;

    public void StartServer()
    {
        udp = GetComponentInParent<UDPService>();
        if (udp == null) udp = GetComponent<UDPService>();
        if (udp == null) udp = gameObject.AddComponent<UDPService>();

        bool ok = udp.Bind(ListenPort, OnMessageReceived);
        if (!ok)
        {
            Debug.LogError($"[Server] Failed to bind to port {ListenPort}");
            return;
        }

        matchActive = false;
        clientEndpoint = null;
        Debug.Log($"[Server] Started listening on port {ListenPort}");
    }

    public void StopServer()
    {
        if (udp != null)
        {
            udp.CloseUDP();
        }
        matchActive = false;
        clientEndpoint = null;
    }

    private void Update()
    {
        if (udp == null || !udp.IsBound) return;

        SendDiscoveryBeacon();
        UpdateMatchStatus();

        if (matchActive && clientEndpoint != null)
        {
            if (IsCountingDown)
            {
                countdownRemaining -= Time.unscaledDeltaTime;
                if (countdownRemaining <= 0)
                {
                    countdownRemaining = 0;
                    ResetMatch(sendToClient: true);
                }
                else
                {
                    Ball.SetSimulate(false); // Ensure ball doesn't move during countdown
                }
            }

            // Apply remote input to the right paddle
            Vector3 rightPos = PaddleRight.transform.position;
            rightPos.y = Mathf.Clamp(
                rightPos.y + remoteClientInput * PaddleRight.Speed * Time.deltaTime,
                PaddleRight.MinY,
                PaddleRight.MaxY);
            PaddleRight.transform.position = rightPos;

            // Broadcast the state to the client using unscaledTime (WaitUI freezes Time.time)
            if (Time.unscaledTime - lastStateSentAt >= StateSendInterval)
            {
                BroadcastState();
                lastStateSentAt = Time.unscaledTime;
            }
        }
    }

    private void SendDiscoveryBeacon()
    {
        // Uses unscaledTime because the game might be paused while waiting
        if (Time.unscaledTime - lastBeaconSentAt < 1.0f) return;

        string message = $"B|PongHost|{ListenPort}";
        udp.Broadcast(message, ListenPort);
        udp.SendToHost(message, "127.0.0.1", ListenPort); // Local testing

        lastBeaconSentAt = Time.unscaledTime;
    }

    private void UpdateMatchStatus()
    {
        // Uses unscaledTime for timeout detection
        bool opponentConnected = clientEndpoint != null && (Time.unscaledTime - lastClientPacketAt <= ClientTimeout);

        if (opponentConnected != matchActive)
        {
            matchActive = opponentConnected;
            if (matchActive)
            {
                Debug.Log("[Server] Client connected!");
                StartCountdown();
            }
            else
            {
                Debug.Log("[Server] Client disconnected or timed out.");
                FreezeGame();
            }
        }
    }

    public void StartCountdown()
    {
        countdownRemaining = 3f;
        ResetMatch(sendToClient: true);
        Ball.SetSimulate(false); // Ball should not move during countdown
    }

    public void ResetMatch(bool sendToClient)
    {
        Ball.ResetBall();
        Ball.SetSimulate(!IsCountingDown);
        ResetPaddles();
        remoteClientInput = 0f;

        if (sendToClient && clientEndpoint != null)
        {
            udp.SendToEndpoint("R\n", clientEndpoint);
            BroadcastState();
        }
    }

    private void ResetPaddles()
    {
        PaddleLeft.transform.position = new Vector3(PaddleLeft.transform.position.x, 0, PaddleLeft.transform.position.z);
        PaddleRight.transform.position = new Vector3(PaddleRight.transform.position.x, 0, PaddleRight.transform.position.z);
        PaddleLeft.enabled = true; // Ensure paddles are enabled
        PaddleRight.enabled = true;
    }

    private void FreezeGame()
    {
        Ball.SetSimulate(false);
        PaddleLeft.enabled = false;
        countdownRemaining = 0; // Cancel countdown if someone disconnects
    }

    private void BroadcastState()
    {
        PongMatchState state = new PongMatchState
        {
            BallX = Ball.transform.position.x,
            BallY = Ball.transform.position.y,
            PaddleLeftY = PaddleLeft.transform.position.y,
            PaddleRightY = PaddleRight.transform.position.y,
            BallState = Ball.State,
            Countdown = countdownRemaining
        };

        udp.SendToEndpoint(state.ToString() + "\n", clientEndpoint);
    }

    private void OnMessageReceived(string message, IPEndPoint from)
    {
        // Ignore discovery beacons (don't treat ourselves as a client)
        if (message.StartsWith("B|", System.StringComparison.Ordinal)) return;

        // Only update the client endpoint if we receive a valid gameplay message
        bool isGameplayMessage = message.StartsWith("J", System.StringComparison.Ordinal) ||
                                 message.StartsWith("I|", System.StringComparison.Ordinal) ||
                                 message.StartsWith("R", System.StringComparison.Ordinal);

        if (isGameplayMessage)
        {
            clientEndpoint = from;
            lastClientPacketAt = Time.unscaledTime;
        }

        if (message.StartsWith("J", System.StringComparison.Ordinal))
        {
            udp.SendToEndpoint("A\n", from); // Acknowledge join
        }
        else if (message.StartsWith("I|", System.StringComparison.Ordinal))
        {
            string[] parts = message.Split('|');
            if (parts.Length >= 2 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float axis))
            {
                remoteClientInput = Mathf.Clamp(axis, -1f, 1f);
            }
        }
        else if (message.StartsWith("R", System.StringComparison.Ordinal))
        {
            StartCountdown();
        }
    }
}
