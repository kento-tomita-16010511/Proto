using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [SerializeField] private TMP_Text _scoreText;

    public int Score { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddScore(int amount)
    {
        Score += amount;
        if (_scoreText != null)
            _scoreText.text = Score.ToString();
    }

    public void SetScoreText(TMP_Text text) { _scoreText = text; }
}
