using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    public TimerController timerController;
    public EnemySpawner enemySpawner;
    public Player player;

    [Header("Result UI")]
    public GameObject resultPanel;
    public TMP_Text resultScoreText;

    void Start()
    {
        if (timerController != null) timerController.OnTimeUp += HandleTimeUp;
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (timerController != null) timerController.OnTimeUp -= HandleTimeUp;
    }

    private void HandleTimeUp()
    {
        if (player != null) player.SetInputEnabled(false);
        if (enemySpawner != null) enemySpawner.StopSpawning();
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            if (resultScoreText != null && ScoreManager.Instance != null)
                resultScoreText.text = ScoreManager.Instance.Score.ToString();
        }
    }
}
