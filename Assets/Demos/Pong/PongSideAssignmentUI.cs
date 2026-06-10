using UnityEngine;

/// <summary>
/// Affiche un bandeau au centre de l'ecran indiquant le cote assigne et la part de vitesse.
/// </summary>
public class PongSideAssignmentUI : MonoBehaviour
{
    const float DisplayDuration = 4f;

    float hideAt;
    string line1 = "";
    string line2 = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Pong")
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<PongSideAssignmentUI>() != null)
        {
            return;
        }

        GameObject go = new GameObject("PongSideAssignmentUI");
        go.AddComponent<PongSideAssignmentUI>();
    }

    void Update()
    {
        if (PongNetworkSession.Instance == null)
        {
            return;
        }

        if (PongNetworkSession.Instance.TryConsumeSideAnnouncement(out string primary, out string secondary))
        {
            line1 = primary;
            line2 = secondary;
            hideAt = Time.time + DisplayDuration;
        }
    }

    void OnGUI()
    {
        if (Time.time >= hideAt || string.IsNullOrEmpty(line1))
        {
            return;
        }

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Pong")
        {
            return;
        }

        const int boxWidth = 420;
        const int boxHeight = 90;
        float x = (Screen.width - boxWidth) * 0.5f;
        float y = Screen.height * 0.35f;

        GUI.Box(new Rect(x, y, boxWidth, boxHeight), "");
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };

        GUI.Label(new Rect(x, y + 12, boxWidth, 36), line1, titleStyle);
        if (!string.IsNullOrEmpty(line2))
        {
            GUI.Label(new Rect(x, y + 48, boxWidth, 28), line2, subStyle);
        }
    }
}
