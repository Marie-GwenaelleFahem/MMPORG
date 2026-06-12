using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

[Serializable]
public class PlayerNetData
{
    public string Name;
    public string ColorHex;
    public int Side; // 1 = Left, 2 = Right

    public override string ToString()
    {
        return $"{Name},{ColorHex},{Side}";
    }

    public static PlayerNetData FromString(string data)
    {
        string[] parts = data.Split(',');
        if (parts.Length < 3) return null;
        return new PlayerNetData
        {
            Name = parts[0],
            ColorHex = parts[1],
            Side = int.Parse(parts[2], CultureInfo.InvariantCulture)
        };
    }
}

[Serializable]
public class PongMatchState
{
    public float BallX;
    public float BallY;
    public float PaddleLeftY;
    public float PaddleRightY;
    public PongBallState BallState;
    public float Countdown;
    public List<PlayerNetData> Players = new List<PlayerNetData>();

    public override string ToString()
    {
        string playersStr = string.Join(";", Players.Select(p => p.ToString()));
        return string.Join("|", new[]
        {
            "S",
            BallX.ToString("F4", CultureInfo.InvariantCulture),
            BallY.ToString("F4", CultureInfo.InvariantCulture),
            PaddleLeftY.ToString("F4", CultureInfo.InvariantCulture),
            PaddleRightY.ToString("F4", CultureInfo.InvariantCulture),
            ((int)BallState).ToString(CultureInfo.InvariantCulture),
            Countdown.ToString("F2", CultureInfo.InvariantCulture),
            playersStr
        });
    }

    public bool FromString(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;

        string[] parts = message.Split('|');
        if (parts.Length < 8 || parts[0] != "S") return false;

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

            Countdown = float.Parse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture);

            Players.Clear();
            if (!string.IsNullOrEmpty(parts[7]))
            {
                string[] playerParts = parts[7].Split(';');
                foreach (string p in playerParts)
                {
                    var data = PlayerNetData.FromString(p);
                    if (data != null) Players.Add(data);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MatchState] Failed to parse state message: {ex.Message}");
            return false;
        }
    }
}
