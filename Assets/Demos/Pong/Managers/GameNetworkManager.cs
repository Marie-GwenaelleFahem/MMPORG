using UnityEngine;
using UnityEngine.SceneManagement;

// This class acts as a bridge between the UI and the Networking logic.
public class GameNetworkManager : MonoBehaviour
{
    public static GameNetworkManager Instance { get; private set; }

    [Header("Settings")]
    public string GameSceneName = "Pong";

    public bool IsHost { get; private set; }
    public string SelectedDifficulty { get; private set; }
    public string HostIP { get; private set; }

    // Checks if a client is connected by asking the PongNetworkSession.
    public bool IsClientConnected => PongNetworkSession.Instance != null && PongNetworkSession.Instance.IsMatchActive;

    private void Awake()
    {
        // Ensure only one instance exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;

            if (SceneManager.GetActiveScene().name == GameSceneName)
            {
                TriggerNetworkStart();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    // Prepares to start a host session and loads the game scene.
    public void StartHost(string difficulty)
    {
        IsHost = true;
        SelectedDifficulty = difficulty;

        Debug.Log($"[NetworkManager] Preparing to Host - Difficulty: {difficulty}");

        // If we are already in the Pong scene, start immediately
        if (SceneManager.GetActiveScene().name == GameSceneName)
        {
            TriggerNetworkStart();
        }
        else
        {
            // Check if scene exists in build settings
            if (Application.CanStreamedLevelBeLoaded(GameSceneName))
            {
                SceneManager.LoadScene(GameSceneName);
            }
            else
            {
                Debug.LogError($"[NetworkManager] CANNOT LOAD SCENE: {GameSceneName}");
            }
        }
    }

    // Prepares to join a game at a specific IP address and loads the game scene.
    public void JoinGame(string ipAddress)
    {
        IsHost = false;
        HostIP = ipAddress;

        Debug.Log($"[NetworkManager] Preparing to Join Game at IP: {ipAddress}");

        if (SceneManager.GetActiveScene().name == GameSceneName)
        {
            TriggerNetworkStart();
        }
        else
        {
            SceneManager.LoadScene(GameSceneName);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == GameSceneName)
        {
            TriggerNetworkStart();
        }
    }

    private void TriggerNetworkStart()
    {
        // Try to find the MenuController and hide it if we are starting a game
        MenuController menu = Object.FindAnyObjectByType<MenuController>();
        if (menu != null)
        {
            Debug.Log("[NetworkManager] Hiding Menu UI");
            menu.HideAllPanels();
        }

        if (PongNetworkSession.Instance == null)
        {
            Debug.Log("[NetworkManager] PongNetworkSession instance not found in scene!");
            return;
        }

        if (IsHost)
        {
            Debug.Log("[NetworkManager] Triggering PongNetworkSession.StartHost()");
            PongNetworkSession.Instance.StartHost();
        }
        else
        {
            Debug.Log($"[NetworkManager] Triggering PongNetworkSession.StartClient() for IP: {HostIP}");
            PongNetworkSession.Instance.StartClient(HostIP);
        }
    }

    public void SetHostMode(bool isHost)
    {
        IsHost = isHost;
        Debug.Log($"[NetworkManager] Mode changed. IsHost is now: {isHost}");
    }
}
