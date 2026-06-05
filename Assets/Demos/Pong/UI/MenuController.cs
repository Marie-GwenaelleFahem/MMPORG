using UnityEngine;

public class MenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject MainPanel;
    public GameObject HostPanel;
    public GameObject ClientPanel;

    private void Start()
    {
        // Start by showing the Main Panel
        ShowMainPanel();
    }

    public void ShowMainPanel()
    {
        SetAllPanelsInactive();
        MainPanel.SetActive(true);

        // When returning to the Main Panel, we should stop any active session
        // and hide the game elements.
        if (PongNetworkSession.Instance != null)
        {
            // IMPORTANT: We pass 'false' here to avoid an infinite loop (recursion).
            // The menu is already showing the panel, so we don't need the session to trigger it again.
            PongNetworkSession.Instance.StopSession(false);
        }
    }

    public void ShowHostPanel()
    {
        SetAllPanelsInactive();
        HostPanel.SetActive(true);
    }

    public void ShowClientPanel()
    {
        SetAllPanelsInactive();
        ClientPanel.SetActive(true);
    }

    /// <summary>
    /// Hides all menu panels. Useful when transitioning into the actual game.
    /// </summary>
    public void HideAll()
    {
        SetAllPanelsInactive();
    }

    private void SetAllPanelsInactive()
    {
        MainPanel.SetActive(false);
        HostPanel.SetActive(false);
        ClientPanel.SetActive(false);
    }
}
