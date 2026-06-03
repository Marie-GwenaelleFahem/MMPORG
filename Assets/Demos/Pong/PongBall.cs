using UnityEngine;

public enum PongBallState {
  Playing = 0,
  PlayerLeftWin = 1,
  PlayerRightWin = 2,
}

public class PongBall : MonoBehaviour
{
    public float Speed = 1;
    public bool Simulate = true;

    Vector3 Direction;
    PongBallState _State = PongBallState.Playing;

    public PongBallState State {
      get {
        return _State;
      }
    } 

    void Start() {
      ResetBall();
    }

    void Update() {
      if (!Simulate) {
        return;
      }

      if (State != PongBallState.Playing) {
        return;
      }

      transform.position = transform.position + (Direction * Speed * Time.deltaTime);
    }

    void OnCollisionEnter(Collision c) {
      if (!Simulate) {
        return;
      }

      switch (c.collider.name) {
        case "BoundTop":
        case "BoundBottom":
          Direction.y = -Direction.y;
          break;

        case "PaddleLeft":
        case "PaddleRight":
          Direction.x = -Direction.x;
          break;

        case "BoundLeft":
          _State = PongBallState.PlayerRightWin;
          break;

        case "BoundRight":
          _State = PongBallState.PlayerLeftWin;
          break;

      }
    }

    public void SetSimulate(bool simulate) {
      Simulate = simulate;
    }

    public void ApplyNetworkState(Vector3 position, PongBallState state) {
      transform.position = position;
      _State = state;
    }

    public void ResetBall() {
      transform.position = Vector3.zero;
      _State = PongBallState.Playing;
      Direction = new Vector3(
        Random.Range(0.5f, 1),
        Random.Range(-0.5f, 0.5f),
        0
      );
      Direction.x *= Mathf.Sign(Random.Range(-100, 100));
      Direction.Normalize();
    }

}
