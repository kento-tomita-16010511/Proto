using UnityEngine;
using System;
using UniRx;

/// <summary>
/// 敵の基本クラス。HP管理や死亡処理などの共通機能を持ちます。
/// </summary>
public abstract class EnemyBasePresenter : MonoBehaviour, IEnemy
{
    [Header("Base Stats")]
    [SerializeField, Tooltip("敵の最大体力")]
    protected int maxHP = 100;
    [SerializeField] private int scoreValue = 10;

    /// <summary>最大HPを取得します</summary>
    public int MaxHP => maxHP;
    /// <summary>現在のHPを取得します</summary>
    public int CurrentHP { get; protected set; }

    /// <summary>死亡時に通知を受け取りたい場合に購読するイベント（エフェクト再生やスコア加算用）。</summary>
    public IObservable<Unit> OnDeath => _onDeath;
    private readonly Subject<Unit> _onDeath = new Subject<Unit>();

    protected virtual void Awake()
    {
        CurrentHP = maxHP;
        _onDeath.AddTo(this);
    }

    /// <summary>
    /// ダメージを受ける処理
    /// </summary>
    public virtual void TakeDamage(int amount)
    {
        if (CurrentHP <= 0) return;

        CurrentHP -= amount;
        if (CurrentHP <= 0) Die();
    }

    /// <summary>
    /// 死亡時の基本処理。必要に応じてオーバーライドしてください。
    /// </summary>
    protected virtual void Die()
    {
        _onDeath.OnNext(Unit.Default);
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(scoreValue);
        Destroy(gameObject);
    }

    void IEnemy.Die()
    {
        Die();
    }
}