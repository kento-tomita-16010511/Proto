using UnityEngine;

/// <summary>
/// Net（蜘蛛の巣）エフェクトの当たり判定とスタン付与を担うクラス。
/// ParticleSystem のパーティクル衝突（OnParticleCollision）で敵を検出し、一定時間停止させる。
/// FX_SpiderWeb_Impact prefab にアタッチする。
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class WebStunEffect : MonoBehaviour
{
    /// <summary>エフェクト GameObject の自動破棄までの時間（秒）。</summary>
    [SerializeField] private float lifetime = 2f;

    /// <summary>移動速度（m/s）。Rigidbody を使わない場合の簡易移動用。</summary>
    [SerializeField] private float moveSpeed = 10f;

    private ParticleSystem _particle;
    private ParticleSystemRenderer _psRenderer;
    private MaterialPropertyBlock _mpb;
    private float _elapsed;
    private float _psLifetime;

    /// <summary>ParticleSystem を取得し、衝突メッセージ（OnParticleCollision）を有効化する。</summary>
    private void Awake()
    {
        _particle = GetComponent<ParticleSystem>();
        _psRenderer = GetComponent<ParticleSystemRenderer>();
        _mpb = new MaterialPropertyBlock();
        _psLifetime = _particle.main.startLifetime.constant;

        // ParticleSystem の当たり判定でスタンを与えるため、衝突モジュールを有効化する。
        // CharacterController は Unity の標準物理コライダーとして認識されないため、
        // 敵 Prefab に別途 CapsuleCollider（StunCollider）を追加して検出している。
        var collision = _particle.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.High; // Medium 以下はコールバックが不安定
        collision.enableDynamicColliders = true; // 動的オブジェクト（NavMeshAgent 等）も検出
        collision.sendCollisionMessages = true;
        collision.radiusScale = 5f; // 衝突判定球を粒子サイズの5倍に拡大（デフォルト1→0.13m、5倍で0.65m）
    }

    /// <summary>パーティクルを再生し、寿命経過で自身を破棄する。</summary>
    private void Start()
    {
        _particle.Play();
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// パーティクルのライフタイムに合わせて _Progress を 0→1 で更新する。
    /// ShaderGraph の Saturate(Progress) が正しく機能するよう毎フレーム設定する。
    /// </summary>
    private void Update()
    {
        // Rigidbody がない場合は、トランスフォームを直接移動させる
        if (!TryGetComponent<Rigidbody>(out _))
        {
            transform.position += transform.forward * moveSpeed * Time.deltaTime;
        }

        _elapsed += Time.deltaTime;
        float progress = (_psLifetime > 0f) ? Mathf.Clamp01(_elapsed / _psLifetime) : 1f;
        _psRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_Progress", progress);
        _psRenderer.SetPropertyBlock(_mpb);
    }

    /// <summary>
    /// パーティクルが衝突した相手が敵ならスタンを与える。
    /// 停止時間・シェイクパラメータは敵側の EnemyState（EnemyModel）から取得するため、
    /// ここでは Stun() を呼ぶだけでよい。
    /// </summary>
    /// <param name="other">パーティクルが衝突した GameObject。</param>
    private void OnParticleCollision(GameObject other)
    {
        var enemy = other.GetComponentInParent<EnemyPresenter>();
        if (enemy != null) enemy.Stun();
    }
}
