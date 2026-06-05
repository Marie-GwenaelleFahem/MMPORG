using UnityEngine;
using TMPro;

/// <summary>
/// Manages the Host Panel UI for difficulty selection.
/// When a difficulty button is clicked, the game starts immediately with that difficulty.
/// </summary>
public class HostPanelUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text SelectedDifficultyText;

    private string _currentDifficulty = "Normal";

    private void Start()
    {
        // Display default selection
        UpdateDifficultyDisplay("Normal");
        Debug.Log("[HostPanelUI] Host Panel initialized with default difficulty: Normal");
    }

    /// <summary>
    /// Called when a difficulty button is clicked in the UI.
    /// Immediately starts the game with the selected difficulty.
    /// This method must be PUBLIC to be accessible from the Unity UI Click event.
    /// </summary>
    /// <param name="difficulty">The selected difficulty level (Easy, Normal, Hard, Intense)</param>
    public void SelectDifficulty(string difficulty)
    {
        _currentDifficulty = difficulty;
        UpdateDifficultyDisplay(difficulty);

        Debug.Log($"[HostPanelUI] Difficulty selected: {difficulty} - Starting game...");

        // Immediately start the host session
        StartGame();
    }

    /// <summary>
    /// Starts the game by telling the GameNetworkManager to begin hosting.
    /// </summary>
    private void StartGame()
    {
        if (GameNetworkManager.Instance == null)
        {
            Debug.LogError("[HostPanelUI] GameNetworkManager.Instance is NULL! Is there a GameNetworkManager in your scene?");
            return;
        }

        // Start hosting with the selected difficulty
        GameNetworkManager.Instance.StartHost(_currentDifficulty);
    }

    /// <summary>
    /// Updates the UI text to show the selected difficulty.
    /// </summary>
    /// <param name="difficulty">The difficulty name to display</param>
    private void UpdateDifficultyDisplay(string difficulty)
    {
        if (SelectedDifficultyText != null)
        {
            SelectedDifficultyText.text = $"Difficulty: {difficulty}";
        }
    }
}
