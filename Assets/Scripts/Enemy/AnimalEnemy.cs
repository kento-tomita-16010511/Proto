using UnityEngine;

public class AnimalEnemy : EnemyBase
{
    [SerializeField] private int scoreValue = 10;

    protected override void Die()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(scoreValue);
        base.Die();
    }
}
