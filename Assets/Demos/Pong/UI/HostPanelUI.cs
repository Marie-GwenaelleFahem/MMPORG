using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HostPanelUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Button StartButton;
    public TMP_Text SelectedDifficultyText;

    private string _currentDifficulty = "Normal";

    private void Start()
    {
        // Default selection
        SelectDifficulty("Normal");

        // Setup Start button listener via code
        if (StartButton != null)
        {
            StartButton.onClick.AddListener(OnStartClicked);
            Debug.Log("[HostPanelUI] StartButton listener attached successfully.");
        }
        else
        {
            Debug.LogError("[HostPanelUI] StartButton is NOT assigned in the Inspector!");
        }
    }

    /// <summary>
    /// Called by Difficulty Buttons in the UI.
    /// This must be PUBLIC to be seen by the Unity UI Click event list.
    /// </summary>
    /// <param name="difficulty">The name of the difficulty (Easy, Normal, Hard, Intense)</param>
    public void SelectDifficulty(string difficulty)
    {
        _currentDifficulty = difficulty;

        if (SelectedDifficultyText != null)
            SelectedDifficultyText.text = $"Selected: {difficulty}";

        Debug.Log($"Difficulty selected: {difficulty}");
    }

    /// <summary>
    /// This must be PUBLIC to be seen by the Unity UI Click event list.
    /// </summary>
    public void OnStartClicked()
    {
        Debug.Log("[HostPanelUI] OnStartClicked triggered!");

        if (GameNetworkManager.Instance == null)
        {
            Debug.LogError("[HostPanelUI] GameNetworkManager.Instance is NULL! Is there a GameNetworkManager in your scene?");
            return;
        }

        // Tell the NetworkManager to start the host
        GameNetworkManager.Instance.StartHost(_currentDifficulty);
    }
}
