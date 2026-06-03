using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

[DefaultExecutionOrder(-50)]
public class TCPServer : MonoBehaviour
{
    public int ListenPort = 25000;
    public string OnConnectionMessage = "Welcome client!";

    TcpListener tcp;
    IPEndPoint localEP;

    public delegate void TCPMessageReceive(string message);

    private TCPMessageReceive OnMessageReceive;
    private string receiveBuffer = "";

    private List<TcpClient> Connections = new List<TcpClient>();

    public string LastError { get; private set; } = "";

    public bool Listen(TCPMessageReceive handler) {
        if (tcp != null) {
            Debug.LogWarning("Socket already initialized! Close it first.");
            LastError = "Serveur deja actif.";
            return false;
        }
        try {
            LastError = "";
            tcp = new TcpListener(IPAddress.Any, ListenPort);
            tcp.Start();
            Debug.Log("Host en ecoute sur le port " + ListenPort + " (toutes les interfaces reseau).");
            OnMessageReceive = handler;
            return true;
        } catch (System.Exception ex)
        {
            LastError = ex.Message;
            Debug.LogWarning("Error creating TCP listener on port " + ListenPort + ": " + ex.Message);
            CloseTCP();
            return false;
        }
    }

    public void BroadcastTCPMessage(string message) {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message);
        BroadcastTCPBytes(bytes);
    }

    public void Close() {
        CloseTCP();
    }

    public bool IsListening {
        get {
            return (tcp != null);
        }
    }

    public int ConnectionCount {
        get {
            return Connections.Count;
        }
    }

    private void BroadcastTCPBytes(byte[] bytes) {
        if (tcp == null) {
            return;
        }
        foreach (TcpClient client in Connections) {
            if (!client.Connected) {
                continue;
            }

            SendTCPBytes(client, bytes);

        }        
    }

    private void SendTCPBytes(TcpClient client, byte[] bytes) {
        if (client == null) {
            return;
        }

        try {
            NetworkStream stream = client.GetStream();
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        } catch (System.Exception e)
        {
            Debug.LogWarning(e.Message);
        }
    }

    void OnDisable() {
        CloseTCP();
    }

    void Update() {
        ReceiveTCP();
    }


    private void ReceiveTCP() {
        if (tcp == null) { return; }

        while (tcp.Pending()) {
            TcpClient tcpClient = tcp.AcceptTcpClient();
            tcpClient.NoDelay = true;
            Debug.Log("New connection received from: " + ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address);
            Connections.Add(tcpClient);

            // Welcome message
            if (!string.IsNullOrEmpty(OnConnectionMessage))
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(OnConnectionMessage + "\n");
                SendTCPBytes(tcpClient, bytes);
            }
        }

        foreach (TcpClient client in GetConnectionSnapshot()) {
            try {
                NetworkStream stream = client.GetStream();
                while (stream.DataAvailable)
                {   
                    byte[] data = new byte[client.Available];
                    stream.Read(data, 0, data.Length);

                    ParseString(data);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Client disconnected: " + ex.Message);
                Connections.Remove(client);
            }
        }
    }

    List<TcpClient> GetConnectionSnapshot()
    {
        return new List<TcpClient>(Connections);
    }

    private void ParseString(byte[] bytes) {
        receiveBuffer += System.Text.Encoding.UTF8.GetString(bytes);

        int newlineIndex;
        while ((newlineIndex = receiveBuffer.IndexOf('\n')) >= 0)
        {
            string message = receiveBuffer.Substring(0, newlineIndex).Trim('\r', ' ', '\t');
            receiveBuffer = receiveBuffer.Substring(newlineIndex + 1);

            if (OnMessageReceive == null || string.IsNullOrEmpty(message))
            {
                continue;
            }

            OnMessageReceive.Invoke(message);
        }
    }

    private void CloseTCP() {
        if (tcp != null) {
            tcp.Stop();            
            tcp = null;            
        }
        Connections.Clear();
        receiveBuffer = "";
        OnMessageReceive = null;
    }

}
