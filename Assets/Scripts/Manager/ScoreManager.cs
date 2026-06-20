using UnityEngine;
using TMPro;

/// <summary>
/// スコア管理クラス。スコアの加算とスコアテキストの更新を担当するシングルトン。
/// EnemyBase の Die() で ScoreManager.Instance.AddScore() を呼び出す
/// などしてスコアを加算し、UI のスコアテキストを更新する。
/// </summary>
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

    /// <summary>
    /// スコアを加算し、スコアテキストを更新する。
    /// </summary>
    public void AddScore(int amount)
    {
        Score += amount;
        SoundManager.Instance.PlaySE(SEEnum.Score);
        if (_scoreText != null)
            _scoreText.text = Score.ToString();
    }

    public void SetScoreText(TMP_Text text) { _scoreText = text; }
}
