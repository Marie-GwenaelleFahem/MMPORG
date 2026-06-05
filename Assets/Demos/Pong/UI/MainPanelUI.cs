using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// This script handles the main menu panel where the user chooses to either
/// host a game or join an existing one.
/// </summary>
public class MainPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MenuController menuController;
    
    [Header("Buttons")]
    [SerializeField] private Button launchGameButton;  // "Lancer une partie"
    [SerializeField] private Button joinGameButton;    // "Rejoindre une partie"

    private void Awake()
    {
        // Safety check for references if not set in inspector
        if (menuController == null)
            menuController = GetComponentInParent<MenuController>();

        // Setup listeners
        if (launchGameButton != null)
            launchGameButton.onClick.AddListener(OnLaunchClicked);

        if (joinGameButton != null)
            joinGameButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnLaunchClicked()
    {
        Debug.Log("MainPanelUI: User clicked 'Lancer une partie'");
        // Navigate to the Host Panel to configure the game
        if (menuController != null)
            menuController.ShowHostPanel();
    }

    private void OnJoinClicked()
    {
        Debug.Log("MainPanelUI: User clicked 'Rejoindre une partie'");
        // Navigate to the Client Panel to find/join a game
        if (menuController != null)
            menuController.ShowClientPanel();
    }
}
