using UnityEngine;
using TMPro;

public class HostPanelUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text SelectedDifficultyText;

    private string _defaultDifficulty = "Normal";

    private void Start()
    {
        UpdateDifficultyDisplay("Normal");
    }

    public void SelectDifficulty(string difficulty)
    {
        _defaultDifficulty = difficulty;
        UpdateDifficultyDisplay(difficulty);

        StartGame();
    }

    private void StartGame()
    {
        if (GameNetworkManager.Instance == null)
        {
            Debug.LogError("[HostPanelUI] GameNetworkManager.Instance is NULL");
            return;
        }

        GameNetworkManager.Instance.StartHost(_defaultDifficulty);
    }

    private void UpdateDifficultyDisplay(string difficulty)
    {
        if (SelectedDifficultyText != null)
        {
            SelectedDifficultyText.text = $"Difficulté: {difficulty}";
        }
    }
}
