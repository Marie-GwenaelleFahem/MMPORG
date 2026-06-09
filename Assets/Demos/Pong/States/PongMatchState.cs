using System;
using System.Globalization;
using UnityEngine;

/* Data container for the current state of a Pong match.
 * This class handles serialization and deserialization of the game state 
 * into strings for network transmission.
*/
[Serializable]
public class PongMatchState
{
    public float BallX;
    public float BallY;
    public float PaddleLeftY;
    public float PaddleRightY;
    public PongBallState BallState;

    // Converts the current state into a network-ready string.
    public override string ToString()
    {
        return string.Join("|", new[]
        {
            "S",
            BallX.ToString("F4", CultureInfo.InvariantCulture),
            BallY.ToString("F4", CultureInfo.InvariantCulture),
            PaddleLeftY.ToString("F4", CultureInfo.InvariantCulture),
            PaddleRightY.ToString("F4", CultureInfo.InvariantCulture),
            ((int)BallState).ToString(CultureInfo.InvariantCulture)
        });
    }

    // Populates this state object from a network string.
    public bool FromString(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;

        string[] parts = message.Split('|');
        if (parts.Length < 6 || parts[0] != "S") return false;

        try
        {
            BallX = float.Parse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture);
            BallY = float.Parse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture);
            PaddleLeftY = float.Parse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture);
            PaddleRightY = float.Parse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture);

            int stateInt = int.Parse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture);
            BallState = Enum.IsDefined(typeof(PongBallState), stateInt)
                ? (PongBallState)stateInt
                : PongBallState.Playing;

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MatchState] Failed to parse state message: {ex.Message}");
            return false;
        }
    }
}
