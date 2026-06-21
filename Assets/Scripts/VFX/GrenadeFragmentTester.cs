using UnityEngine;

/// <summary>
/// テストシーン用の動作確認ドライバ。
/// 指定座標で GrenadeFragmentEffect を再生し、必要なら一定間隔で繰り返す。
/// 実運用コードからは不要（GrenadeFragmentEffect.Spawn / Explode を直接呼べばよい）。
/// </summary>
public class GrenadeFragmentTester : MonoBehaviour
{
    [Tooltip("再生する破片エフェクトのプレハブ。")]
    [SerializeField] private GrenadeFragmentEffect fragmentPrefab;

    [Tooltip("爆発させるワールド座標。")]
    [SerializeField] private Vector3 explosionPoint = new Vector3(0f, 0.3f, 0f);

    [Tooltip("一定間隔で繰り返し再生するか。")]
    [SerializeField] private bool repeat = true;

    [Tooltip("繰り返し再生の間隔（秒）。")]
    [SerializeField] private float interval = 4f;

    private float _timer;

    private void Start()
    {
        Fire();
    }

    private void Update()
    {
        if (!repeat) return;
        _timer += Time.deltaTime;
        if (_timer >= interval)
        {
            _timer = 0f;
            Fire();
        }
    }

    /// <summary>破片エフェクトを 1 ショット再生する。</summary>
    [ContextMenu("Explode Now")]
    private void Fire()
    {
        if (fragmentPrefab == null)
        {
            Debug.LogWarning("[GrenadeFragmentTester] fragmentPrefab が未設定です。");
            return;
        }
        GrenadeFragmentEffect.Spawn(fragmentPrefab, explosionPoint);
    }
}
