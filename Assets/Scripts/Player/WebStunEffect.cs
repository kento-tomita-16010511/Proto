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

    private float _stunDuration;
    private ParticleSystem _particle;

    /// <summary>Player から停止時間を受け取る。生成直後に呼ぶこと。</summary>
    /// <param name="duration">敵を停止させる時間（秒）。</param>
    public void Initialize(float duration)
    {
        _stunDuration = duration;
    }

    /// <summary>ParticleSystem を取得し、衝突メッセージ（OnParticleCollision）を有効化する。</summary>
    private void Awake()
    {
        _particle = GetComponent<ParticleSystem>();

        // ParticleSystem の当たり判定でスタンを与えるため、衝突モジュールを有効化する。
        var collision = _particle.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.sendCollisionMessages = true;
    }

    /// <summary>パーティクルを再生し、寿命経過で自身を破棄する。</summary>
    private void Start()
    {
        _particle.Play();
        Destroy(gameObject, lifetime);
    }

    /// <summary>パーティクルが衝突した相手が敵ならスタンを与える。</summary>
    /// <param name="other">パーティクルが衝突した GameObject。</param>
    private void OnParticleCollision(GameObject other)
    {
        Debug.Log($"[WebStunEffect] OnParticleCollision with {other.name}");
        var enemy = other.GetComponentInParent<EnemyPresenter>();
        if (enemy != null) enemy.Stun(_stunDuration);
    }
}
