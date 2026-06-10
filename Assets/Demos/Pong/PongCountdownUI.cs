using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Affiche le decompte 3, 2, 1, GO! au centre de l'ecran avant le debut d'une manche.
/// </summary>
public class PongCountdownUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "Pong")
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<PongCountdownUI>() != null)
        {
            return;
        }

        GameObject go = new GameObject("PongCountdownUI");
        go.AddComponent<PongCountdownUI>();
    }

    void OnGUI()
    {
        if (SceneManager.GetActiveScene().name != "Pong")
        {
            return;
        }

        if (PongNetworkSession.Instance == null || !PongNetworkSession.Instance.IsCountdownActive)
        {
            return;
        }

        string text = PongNetworkSession.Instance.CountdownText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 96,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        float size = 200f;
        float x = (Screen.width - size) * 0.5f;
        float y = (Screen.height - size) * 0.4f;
        GUI.Label(new Rect(x, y, size, size), text, style);
    }
}
