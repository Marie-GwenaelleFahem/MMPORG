using UnityEngine;
using UnityEngine.InputSystem;

public enum PongPlayer {
  PlayerLeft = 1,
  PlayerRight = 2
}

public class PongPaddle : MonoBehaviour
{ 
    public PongPlayer Player = PongPlayer.PlayerLeft;
    public float Speed = 1;
    public float MinY = -4;
    public float MaxY = 4;

    PongInput inputActions;
    InputAction PlayerAction;


    void Awake()
    {
        EnsureInputReady();
    }

    void OnEnable()
    {
        EnsureInputReady();
        PlayerAction?.Enable();
    }

    void EnsureInputReady()
    {
        if (inputActions != null)
        {
            return;
        }

        inputActions = new PongInput();
        switch (Player) {
          case PongPlayer.PlayerLeft:
            PlayerAction = inputActions.Pong.Player1;
            break;
          case PongPlayer.PlayerRight:
            PlayerAction = inputActions.Pong.Player2;
            break;
        }
    }

    public void SetColor(Color color)
    {
        Renderer r = GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = color;
        }
    }

    // Update is called once per frame
    void Update()
    {
      if (PlayerAction == null)
      {
        return;
      }

      if (PongNetworkSession.Instance != null && !PongNetworkSession.Instance.CanMovePaddle(Player))
      {
        return;
      }

      float speedMultiplier = 1f;
      if (PongNetworkSession.Instance != null)
      {
        speedMultiplier = PongNetworkSession.Instance.GetPaddleSpeedMultiplier(Player);
        if (speedMultiplier <= 0f)
        {
          return;
        }
      }

      float direction = PlayerAction.ReadValue<float>();
      Vector3 newPos = transform.position + (Vector3.up * Speed * speedMultiplier * direction * Time.deltaTime);
      newPos.y = Mathf.Clamp(newPos.y, MinY, MaxY);
      transform.position = newPos;
    }

    void OnDisable() {
      PlayerAction?.Disable();
    }
}
