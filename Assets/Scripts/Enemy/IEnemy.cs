using UnityEngine;

/// <summary>
/// 全ての敵ユニットが実装すべきインターフェース
/// </summary>
public interface IEnemy
{
    /// <summary>現在のHP</summary>
    int CurrentHP { get; }
    /// <summary>最大HP</summary>
    int MaxHP { get; }
    /// <summary>
    /// ダメージを与える処理
    /// </summary>
    /// <param name="amount">ダメージ量</param>
    void TakeDamage(int amount);
}