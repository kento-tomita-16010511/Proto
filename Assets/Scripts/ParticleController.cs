using UnityEngine;

public class ParticleController : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particle;

    [SerializeField] private RectTransform _rootRect;

    [Header("Rotation Settings")]
    [SerializeField, Tooltip("ランダム回転の最小角度")]
    private float _minAngle = 0f;
    [SerializeField, Tooltip("ランダム回転の最大角度")]
    private float _maxAngle = 140f;

    /// <summary>
    /// ランダムな角度を設定してパーティクルを再生します。
    /// Animation Event からの呼び出しにも対応しています。
    /// </summary>
    public void PlayParticle()
    {
        if (_particle != null)
        {
            // 1. 指定された範囲内で Z軸（2Dの回転）をランダムに決定
            float randomZ = Random.Range(_minAngle, _maxAngle);

            // 2. ルートとなる RectTransform の角度を更新
            if (_rootRect == null) _rootRect = GetComponent<RectTransform>();
            _rootRect.transform.localRotation = Quaternion.Euler(0f, 0f, randomZ);

            // 3. パーティクルを再生
            _particle.Play();
        }
    }
}
