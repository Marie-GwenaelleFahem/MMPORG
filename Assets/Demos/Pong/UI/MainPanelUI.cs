using UnityEngine;
using UnityEngine.UI;

public class MainPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MenuController menuController;

    [Header("Buttons")]
    [SerializeField] private Button launchGameButton;
    [SerializeField] private Button joinGameButton;

    private void Awake()
    {
        if (menuController == null) menuController = GetComponentInParent<MenuController>();

        if (launchGameButton != null) launchGameButton.onClick.AddListener(OnLaunchClicked);

        if (joinGameButton != null) joinGameButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnLaunchClicked()
    {
        if (menuController != null) menuController.ShowHostPanel();
    }

    private void OnJoinClicked()
    {
        if (menuController != null) menuController.ShowClientPanel();
    }
}
