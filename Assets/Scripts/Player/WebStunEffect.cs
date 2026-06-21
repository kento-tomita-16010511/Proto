using UnityEngine;

/// <summary>
/// Net（蜘蛛の巣）エフェクトの射出・落下・当たり判定とスタン付与を担うクラス。
/// 射出後は Rigidbody の重力に従って落下し、衝突した敵をスタン状態にする。
/// </summary>
public class WebStunEffect : MonoBehaviour
{
    /// <summary>エフェクト GameObject の自動破棄までの時間（秒）。</summary>
    [SerializeField] private float lifetime = 2f;

    /// <summary>射出時の前方初速（m/s）。以降は Rigidbody の重力で落下する。</summary>
    [SerializeField] private float moveSpeed = 10f;

    /// <summary>射出時に加える上向き初速（m/s）。重力と合わせて放物線を描かせる。</summary>
    [SerializeField] private float launchUpwardSpeed = 3f;

    private Animator _animator;

    private Rigidbody _rigidbody;

    /// <summary>多重スタン呼び出しを防ぐフラグ。最初の敵ヒットで true になる。</summary>
    private bool _hasStunned;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    /// <summary>アニメーションを再生し、lifetimeで自身を破棄する。</summary>
    private void Start()
    {
        if (_animator != null) _animator.SetTrigger("Play");
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// 指定方向へ射出する。前方初速＋上向き初速を与え、以降は Rigidbody の重力で落下する。
    /// Player から生成直後に呼ばれる。
    /// </summary>
    /// <param name="direction">射出する水平方向。</param>
    public void Launch(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
        direction.Normalize();

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = direction * moveSpeed + Vector3.up * launchUpwardSpeed;
        }
    }

    /// <summary>
    /// 衝突した相手が敵ならスタンを与える。
    /// 停止時間・シェイクパラメータは敵側の EnemyState（EnemyModel）から取得するため、
    /// ここでは Stun() を呼ぶだけでよい。
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        TryStun(collision.gameObject);
    }

    /// <summary>衝突相手が Enemy なら一度だけスタンを付与する。</summary>
    private void TryStun(GameObject other)
    {
        if (_hasStunned || other == null) return;
        if (!other.CompareTag("Enemy")) return;

        // コライダーは子オブジェクト側に付いている場合があるため、親方向も探索する。
        var enemy = other.GetComponentInParent<EnemyPresenter>();
        if (enemy == null) return;

        enemy.Stun();
        _hasStunned = true;
    }
}
