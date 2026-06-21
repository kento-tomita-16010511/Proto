using UnityEngine;
using UniRx;

/// <summary>
/// Net（蜘蛛の巣）エフェクトの射出・落下・当たり判定とスタン付与を担うクラス。
/// 射出後は Rigidbody の重力に従って落下し、衝突した敵をスタン状態にする。
/// 飛翔中は向きを速度ベクトルへ追従させ、重力で落ちるにつれ自然に地面方向へ傾ける。
/// </summary>
public class WebStunEffect : MonoBehaviour
{
    /// <summary>エフェクト GameObject の自動破棄までの時間（秒）。</summary>
    [SerializeField] private float lifetime = 2f;

    /// <summary>射出時の前方初速（m/s）。以降は Rigidbody の重力で落下する。</summary>
    [SerializeField] private float moveSpeed = 10f;

    /// <summary>射出時に加える上向き初速（m/s）。重力と合わせて放物線を描かせる。</summary>
    [SerializeField] private float launchUpwardSpeed = 3f;

    /// <summary>飛翔中、速度方向へ向きを合わせる追従の速さ。大きいほど即座に向く。</summary>
    [SerializeField] private float faceVelocityLerpSpeed = 12f;

    private Animator _animator;

    private Rigidbody _rigidbody;

    /// <summary>多重スタン呼び出しを防ぐフラグ。最初の敵ヒットで true になる。</summary>
    private bool _hasStunned;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    /// <summary>アニメーションを再生し、lifetimeで自身を破棄する。飛翔中の向き追従を開始する。</summary>
    private void Start()
    {
        if (_animator != null) _animator.SetTrigger("Play");
        Destroy(gameObject, lifetime);

        // 毎フレーム処理は Update を使わず EveryUpdate で行う（CLAUDE.md 規約）。
        Observable.EveryUpdate()
            .Subscribe(_ => FaceVelocity())
            .AddTo(this);
    }

    /// <summary>
    /// 向きを現在の速度ベクトルへ滑らかに合わせる。重力で速度が下を向くにつれ、
    /// web も地面方向へ傾いて自然に落下しているように見える。敵ヒット後は追従を止める。
    /// </summary>
    private void FaceVelocity()
    {
        if (_rigidbody == null || _hasStunned) return;

        Vector3 v = _rigidbody.linearVelocity;
        if (v.sqrMagnitude < 0.01f) return;

        Quaternion target = Quaternion.LookRotation(v.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, faceVelocityLerpSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 指定方向へ射出する。方向は webMuzzle（カメラの子）の forward をそのまま受け取り、
    /// 上下の照準角（pitch）を保持する。さらに上向き初速を加え、以降は Rigidbody の重力で落下する。
    /// Player から生成直後に呼ばれる。
    /// </summary>
    /// <param name="direction">射出方向（上下角を含む 3D ベクトル）。</param>
    public void Launch(Vector3 direction)
    {
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
    /// コライダーはルート（Rigidbody 所有者）の WebStunEffect に通知されるため、
    /// 子 Collision がトリガー/非トリガーのどちらでも検知できるよう両方を受ける。
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        TryStun(other.gameObject);
    }

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
