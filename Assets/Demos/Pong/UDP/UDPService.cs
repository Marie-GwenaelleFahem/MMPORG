using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class UDPService : MonoBehaviour
{
    public delegate void UDPMessageHandler(string message, IPEndPoint source);

    UdpClient udp;
    IPEndPoint listenEndPoint;
    UDPMessageHandler onMessageReceive;

    public string LastError { get; private set; } = "";
    public bool IsBound => udp != null;
    public int LocalPort => listenEndPoint != null ? listenEndPoint.Port : 0;

    public bool Bind(int port, UDPMessageHandler handler)
    {
        if (udp != null)
        {
            Debug.LogWarning("Socket already initialized! Close it first");
            return false;
        }

        try
        {
            LastError = "";
            listenEndPoint = new IPEndPoint(IPAddress.Any, port);

            // Allow multiple apps to use the same port for listening to broadcasts
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.ExclusiveAddressUse = false;

            udp.EnableBroadcast = true;
            udp.Client.Bind(listenEndPoint);
            DisableUdpConnReset(udp.Client);

            // Update the endpoint to reflect the actual port assigned (especially if port was 0)
            listenEndPoint = (IPEndPoint)udp.Client.LocalEndPoint;

            Debug.Log($"Server listening on port: {LocalPort}");

            onMessageReceive = handler;
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Error binding UDP port " + port + ": " + ex.Message);
            CloseUDP();
            return false;
        }
    }

    public void SendToHost(string message, string host, int port)
    {
        SendToEndpoint(message, new IPEndPoint(IPAddress.Parse(host), port));
    }

    public void SendToEndpoint(string message, IPEndPoint endpoint)
    {
        if (endpoint == null) return;

        byte[] bytes = Encoding.UTF8.GetBytes(message);
        SendBytesToEndpoint(bytes, endpoint);
    }

    public void SendBytesToEndpoint(byte[] bytes, IPEndPoint endpoint)
    {
        if (udp == null || endpoint == null) return;

        try
        {
            udp.Send(bytes, bytes.Length, endpoint);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Erreur envoi UDP: " + ex.Message);
        }
    }

    // Sends a message to every computer on the local network at the specified port. Used for server discovery.
    public void Broadcast(string message, int port)
    {
        if (udp == null) return;

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            // 255.255.255.255 is the "universal" broadcast address
            IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, port);
            udp.Send(bytes, bytes.Length, broadcastEndpoint);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Erreur Broadcast UDP: " + ex.Message);
        }
    }

    public void CloseUDP()
    {
        if (udp != null)
        {
            udp.Close();
            udp = null;
        }

        listenEndPoint = null;
        onMessageReceive = null;
    }

    void OnDisable()
    {
        CloseUDP();
    }

    void Update()
    {
        if (udp == null) { return; }

        try
        {
            while (udp.Available > 0)
            {
                IPEndPoint sourceEndpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udp.Receive(ref sourceEndpoint);
                string message = Encoding.UTF8.GetString(data).Trim('\r', ' ', '\t');

                if (onMessageReceive != null && !string.IsNullOrEmpty(message))
                {
                    onMessageReceive.Invoke(message, sourceEndpoint);
                }
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
        {
            // Windows: ICMP "port unreachable" after sending to a closed UDP port.
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Erreur reception UDP: " + ex.Message);
        }
    }

    static void DisableUdpConnReset(Socket socket)
    {
        if (socket == null)
        {
            return;
        }

        if (Application.platform != RuntimePlatform.WindowsEditor &&
            Application.platform != RuntimePlatform.WindowsPlayer)
        {
            return;
        }

        try
        {
            const int SIO_UDP_CONNRESET = -1744830452;
            socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("UDP SIO_UDP_CONNRESET: " + ex.Message);
        }
    }
}
