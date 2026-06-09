using UnityEngine;

public class MenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject MainPanel;
    public GameObject HostPanel;
    public GameObject ClientPanel;

    private void Start()
    {
        ShowMainPanel();
    }

    public void ShowMainPanel()
    {
        SetAllPanelsInactive();
        MainPanel.SetActive(true);

        // When returning to the Main Panel, we should stop any active session and hide the game elements
        if (PongNetworkSession.Instance != null) PongNetworkSession.Instance.StopSession(false);
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

    public void HideAllPanels()
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
