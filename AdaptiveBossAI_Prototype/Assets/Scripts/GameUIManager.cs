using UnityEngine;
using TMPro;

public class GameUIManager : MonoBehaviour
{
    [Header("References")]
    public EvaderAgent evaderAgent;
    public ChaserAgent chaserAgent;
    
    [Header("UI Elements")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI chaserScoreText;
    public TextMeshProUGUI evaderScoreText;
    public TextMeshProUGUI statusText;
    
    private int chaserScore = 0;
    private int evaderScore = 0;
    
    void Update()
    {
        // Update timer display
        if (evaderAgent != null && timerText != null)
        {
            float timeRemaining = evaderAgent.maxEpisodeTime - evaderAgent.GetEpisodeTimer();
            
            // Clamp to prevent negative values
            timeRemaining = Mathf.Max(0f, timeRemaining);
            
            timerText.text = $"Time: {timeRemaining:F1}s";
            
            // Change color based on urgency (as percentage of total time)
            float timePercent = timeRemaining / evaderAgent.maxEpisodeTime;
            if (timePercent < 0.33f)
                timerText.color = Color.red;
            else if (timePercent < 0.66f)
                timerText.color = Color.yellow;
            else
                timerText.color = Color.white;
        }
        
        // Update scores - force update every frame
        if (chaserScoreText != null)
            chaserScoreText.text = $"Chaser Wins: {chaserScore}";
        
        if (evaderScoreText != null)
            evaderScoreText.text = $"Evader Wins: {evaderScore}";
    }
    
    public void OnChaserWin()
    {
        chaserScore++;
        if (statusText != null)
        {
            statusText.text = "CAUGHT!";
            statusText.color = Color.blue;
            Invoke("ClearStatus", 1f);
        }
    }
    
    public void OnEvaderWin()
    {
        evaderScore++;
        if (statusText != null)
        {
            statusText.text = "ESCAPED!";
            statusText.color = Color.green;
            Invoke("ClearStatus", 1f);
        }
    }
    
    void ClearStatus()
    {
        if (statusText != null)
            statusText.text = "";
    }
    
    public void ResetScores()
    {
        chaserScore = 0;
        evaderScore = 0;
    }
}