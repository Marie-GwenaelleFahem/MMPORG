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
        Ball = GameObject.FindFirstObjectByType<PongBall>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Ball == null)
        {
            Ball = GameObject.FindFirstObjectByType<PongBall>();
            if (Ball == null)
            {
                return;
            }
        }

        switch (Ball.State) {
          case PongBallState.Playing:
            Panel.SetActive(false);
            break;
          case PongBallState.PlayerLeftWin:
            Panel.SetActive(true);
            PlayerLeft.SetActive(true);
            break;
          case PongBallState.PlayerRightWin:
            Panel.SetActive(true);
            PlayerRight.SetActive(true);
            break;
        }
       
    }

    public void OnReplay() {
      if (PongNetworkSession.Instance != null && PongNetworkSession.Instance.IsNetworkSession)
      {
        PongNetworkSession.Instance.RequestReplay();
        return;
      }

      SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
