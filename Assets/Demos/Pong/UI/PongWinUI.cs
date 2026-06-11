using UnityEngine;
using UnityEngine.SceneManagement;

public class PongWinUI : MonoBehaviour
{
  public GameObject Panel;
  public GameObject PlayerLeft;
  public GameObject PlayerRight;

  PongBall Ball;

  // Start is called once before the first execution of Update after the MonoBehaviour is created
  void Start()
  {
    Panel.SetActive(false);
    PlayerLeft.SetActive(false);
    PlayerRight.SetActive(false);
    Ball = GameObject.FindAnyObjectByType<PongBall>();
  }

  // Update is called once per frame
  void Update()
  {
    // 1. Hide everything if we are still in the Menu
    if (PongNetworkSession.Instance != null && PongNetworkSession.Instance.IsInMenu)
    {
      Panel.SetActive(false);
      PlayerLeft.SetActive(false);
      PlayerRight.SetActive(false);
      return;
    }

    // 2. Try to find the ball if we don't have it yet
    if (Ball == null)
    {
      Ball = GameObject.FindAnyObjectByType<PongBall>();
      if (Ball == null) return;
    }

    // 3. Update visibility based on the ball's current state
    switch (Ball.State)
    {
      case PongBallState.Playing:
        // Match is ongoing: Hide the entire Win Panel
        Panel.SetActive(false);
        PlayerLeft.SetActive(false);
        PlayerRight.SetActive(false);
        break;

      case PongBallState.PlayerLeftWin:
        // Left player won: Show panel and specific text
        Panel.SetActive(true);
        PlayerLeft.SetActive(true);
        PlayerRight.SetActive(false); // Hide the other one just in case
        break;

      case PongBallState.PlayerRightWin:
        // Right player won: Show panel and specific text
        Panel.SetActive(true);
        PlayerLeft.SetActive(false); // Hide the other one just in case
        PlayerRight.SetActive(true);
        break;
    }
  }

  public void OnReplay()
  {
    if (PongNetworkSession.Instance != null && PongNetworkSession.Instance.IsNetworkSession)
    {
      PongNetworkSession.Instance.RequestReplay();
      return;
    }

    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
  }
}
