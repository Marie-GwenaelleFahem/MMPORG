using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class UDPService : MonoBehaviour
{
    public delegate void UDPMessageHandler(string message, IPEndPoint from);

    UdpClient udp;
    IPEndPoint listenEndPoint;
    UDPMessageHandler onMessage;

    public string LastError { get; private set; } = "";
    public bool IsBound => udp != null;
    public int LocalPort => listenEndPoint != null ? listenEndPoint.Port : 0;

    public bool Bind(int port, UDPMessageHandler handler)
    {
        if (udp != null)
        {
            LastError = "Socket deja actif.";
            Debug.LogWarning(LastError);
            return false;
        }

        try
        {
            LastError = "";
            listenEndPoint = new IPEndPoint(IPAddress.Any, port);
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.ExclusiveAddressUse = false;
            udp.Client.Bind(listenEndPoint);
            onMessage = handler;
            Debug.Log("UDP en ecoute sur le port " + LocalPort);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.LogWarning("Erreur bind UDP port " + port + ": " + ex.Message);
            Close();
            return false;
        }
    }

    public void Send(string message, string host, int port)
    {
        Send(message, new IPEndPoint(IPAddress.Parse(host), port));
    }

    public void Send(string message, IPEndPoint endpoint)
    {
        if (endpoint == null)
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(message);
        Send(bytes, endpoint);
    }

    public void Send(byte[] bytes, IPEndPoint endpoint)
    {
        if (udp == null || endpoint == null)
        {
            return;
        }

        try
        {
            udp.Send(bytes, bytes.Length, endpoint);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Erreur envoi UDP: " + ex.Message);
        }
    }

    public void Close()
    {
        if (udp != null)
        {
            udp.Close();
            udp = null;
        }

        listenEndPoint = null;
        onMessage = null;
    }

    void OnDisable()
    {
        Close();
    }

    void Update()
    {
        if (udp == null)
        {
            return;
        }

        try
        {
            while (udp.Available > 0)
            {
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udp.Receive(ref from);
                string message = Encoding.UTF8.GetString(data).Trim('\r', ' ', '\t');

                if (onMessage != null && !string.IsNullOrEmpty(message))
                {
                    onMessage.Invoke(message, from);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Erreur reception UDP: " + ex.Message);
        }
    }
}
