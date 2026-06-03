using UnityEngine;
using System.Net;
using System.Net.Sockets;

public class TCPClient : MonoBehaviour
{
    public int DestinationPort = 25000;
    public string DestinationIP = "127.0.0.1";

    TcpClient tcp;
    IPEndPoint localEP;

    public delegate void TCPMessageReceive(string message);

    private TCPMessageReceive OnMessageReceive;
    private string receiveBuffer = "";


    public bool Connect(TCPMessageReceive handler) {
        if (tcp != null) {
            Debug.LogWarning("Socket already initialized! Close it first.");
            return false;
        }
        try {
            tcp = new TcpClient();
            tcp.Connect(DestinationIP, DestinationPort);
            OnMessageReceive = handler;
            return true;
        } catch (System.Exception ex)
        {
            Debug.LogWarning("Error creating connection: " + ex.Message);
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
            tcp.GetStream().Write(bytes, 0, bytes.Length);            
        } catch (SocketException e)
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

        while (tcp.Available > 0)
		{   
            byte[] data = new byte[tcp.Available];
			tcp.GetStream().Read(data, 0, tcp.Available);

			try
			{
				ParseString(data);
			}
			catch (System.Exception ex)
			{
				Debug.LogWarning("Error receiving TCP message: " + ex.Message);
			}
		}
    }

    private void ParseString(byte[] bytes) {
        receiveBuffer += System.Text.Encoding.UTF8.GetString(bytes);

        int newlineIndex;
        while ((newlineIndex = receiveBuffer.IndexOf('\n')) >= 0)
        {
            string message = receiveBuffer.Substring(0, newlineIndex);
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
