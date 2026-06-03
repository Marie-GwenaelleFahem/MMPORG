using UnityEngine;
using System.Net;
using System.Net.Sockets;

[DefaultExecutionOrder(-50)]
public class TCPClient : MonoBehaviour
{
    public int DestinationPort = 25000;
    public string DestinationIP = "127.0.0.1";

    TcpClient tcp;
    IPEndPoint localEP;

    public delegate void TCPMessageReceive(string message);

    private TCPMessageReceive OnMessageReceive;
    private string receiveBuffer = "";

    public string LastError { get; private set; } = "";

    public bool Connect(TCPMessageReceive handler) {
        if (tcp != null) {
            Debug.LogWarning("Socket already initialized! Close it first.");
            return false;
        }
        try {
            LastError = "";
            tcp = new TcpClient();
            tcp.NoDelay = true;
            tcp.Connect(DestinationIP, DestinationPort);
            OnMessageReceive = handler;
            return true;
        } catch (System.Exception ex)
        {
            LastError = ex.Message;
            Debug.LogWarning("Error creating connection to " + DestinationIP + ":" + DestinationPort + " -> " + ex.Message);
            CloseTCP();
            return false;
        }
    }

    public void SendTCPMessage(string message) {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message);
        SendTCPBytes(bytes);
    }

    public void Close() {
        CloseTCP();
    }

    public bool IsConnected {
        get {
            return (tcp != null && tcp.Connected);
        }
    }


    private void SendTCPBytes(byte[] bytes) {
        if (tcp == null) {
            return;
        }

        try {
            NetworkStream stream = tcp.GetStream();
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

        try {
            NetworkStream stream = tcp.GetStream();
            while (stream.DataAvailable)
            {   
                byte[] data = new byte[tcp.Available];
                stream.Read(data, 0, data.Length);
                ParseString(data);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Error receiving TCP message: " + ex.Message);
            CloseTCP();
        }
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
            tcp.Close();
            tcp = null;            
        }
        receiveBuffer = "";
        OnMessageReceive = null;
    }

}
