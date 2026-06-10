using System.Globalization;
using UnityEngine;

/// <summary>
/// Decompte 3-2-1-GO et annonce de cote partages entre host et clients.
/// </summary>
public class PongRoundFlow
{
    const float StepDuration = 1f;
    const float GoDuration = 0.7f;

    bool countdownActive;
    bool showingGo;
    int countdownNumber = 3;
    float nextStepAt;

    string announcementPrimary = "";
    string announcementSecondary = "";
    bool hasAnnouncement;

    public PongPlayer LocalSide { get; private set; } = PongPlayer.PlayerLeft;
    public float LocalSpeedShare { get; private set; } = 1f;
    public int LocalSidePlayerCount { get; private set; } = 1;

    public bool IsCountdownActive => countdownActive;

    public string CountdownText
    {
        get
        {
            if (!countdownActive)
            {
                return "";
            }

            return showingGo ? "GO!" : countdownNumber.ToString(CultureInfo.InvariantCulture);
        }
    }

    public bool IsGameplayUnlocked(bool matchActive) => matchActive && !countdownActive;

    public void Reset()
    {
        countdownActive = false;
        showingGo = false;
        hasAnnouncement = false;
    }

    public void BeginCountdown()
    {
        countdownActive = true;
        showingGo = false;
        countdownNumber = 3;
        nextStepAt = Time.unscaledTime + StepDuration;
    }

    public void UpdateCountdown()
    {
        if (!countdownActive || Time.unscaledTime < nextStepAt)
        {
            return;
        }

        if (showingGo)
        {
            countdownActive = false;
            showingGo = false;
            return;
        }

        countdownNumber--;
        if (countdownNumber <= 0)
        {
            showingGo = true;
            nextStepAt = Time.unscaledTime + GoDuration;
        }
        else
        {
            nextStepAt = Time.unscaledTime + StepDuration;
        }
    }

    public void SetSideAssignment(PongPlayer side, float share, int count, bool showAnnouncement)
    {
        LocalSide = side;
        LocalSpeedShare = share;
        LocalSidePlayerCount = count;

        if (!showAnnouncement)
        {
            return;
        }

        int percent = Mathf.RoundToInt(share * 100f);
        string sideName = side == PongPlayer.PlayerLeft ? "GAUCHE" : "DROITE";
        announcementPrimary = "Tu es a " + sideName;
        announcementSecondary = count + " joueur(s) sur ce cote - " + percent + "% de la vitesse";
        hasAnnouncement = true;
    }

    public bool TryConsumeAnnouncement(out string primary, out string secondary)
    {
        if (!hasAnnouncement)
        {
            primary = "";
            secondary = "";
            return false;
        }

        primary = announcementPrimary;
        secondary = announcementSecondary;
        hasAnnouncement = false;
        return true;
    }
}
