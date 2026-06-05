using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// This class acts as a bridge between the UI and the Networking logic.
/// It follows the Singleton pattern so it can be accessed from any script.
/// It persists across scenes to manage the transition from the menu to the game.
/// </summary>
public class GameNetworkManager : MonoBehaviour
{
    public static GameNetworkManager Instance { get; private set; }

    [Header("Settings")]
    public string GameSceneName = "Pong";

    // State variables
    public bool IsHost { get; private set; }
    public string SelectedDifficulty { get; private set; }
    public string HostIP { get; private set; }

    /// <summary>
    /// Checks if a client is connected by asking the PongNetworkSession.
    /// </summary>
    public bool IsClientConnected => PongNetworkSession.Instance != null && PongNetworkSession.Instance.IsMatchActive;

    private void Awake()
    {
        // Ensure only one instance exists (Singleton pattern)
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep this object alive when changing scenes

            // Subscribe to the sceneLoaded event to know when the game scene is ready
            SceneManager.sceneLoaded += OnSceneLoaded;

            // If the script starts already in the Pong scene, trigger immediately
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
        // Always unsubscribe from events when the object is destroyed to avoid memory leaks
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    /// <summary>
    /// Prepares to start a host session and loads the game scene.
    /// </summary>
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
                Debug.LogError($"[NetworkManager] CANNOT LOAD SCENE: '{GameSceneName}'. Make sure it is added to 'File -> Build Settings'!");
            }
        }
    }
    /// <summary>
    /// Prepares to join a game at a specific IP address and loads the game scene.
    /// </summary>
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
        // When the Pong scene is loaded, we trigger the network logic
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
            menu.HideAll();
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
            PongNetworkSession.Instance.SetRemoteIP(HostIP);
            PongNetworkSession.Instance.StartClient();
        }
    }

    /// <summary>
    /// Updates whether this instance is acting as a host.
    /// Used during "Host Migration" if the original host leaves.
    /// </summary>
    public void SetHostMode(bool isHost)
    {
        IsHost = isHost;
        Debug.Log($"[NetworkManager] Mode changed. IsHost is now: {isHost}");
    }
}
