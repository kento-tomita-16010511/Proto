using System.Threading;
using Cysharp.Threading.Tasks;
using ithappy.Animals_FREE;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMeshAgent または CreatureMover を操作するエネミーの View クラス。
/// 移動指示のみを受け付け、判定ロジックは持たない。
/// NavMeshAgent が存在しない場合は CreatureMover にフォールバックする。
/// </summary>
public class EnemyView : MonoBehaviour
{

    [SerializeField] private DamageVFX damageVFX;

    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private string verticalParam = "Vert";
    [SerializeField] private string stateParam = "State";
    private Vector3 _lastTargetPosition;

    /// <summary>
    /// スタン中に横揺れさせるビジュアル用の子 Transform。
    /// NavMeshAgent はルート Transform を制御するため、揺れはこの子で行う。
    /// 未設定の場合はシェイクをスキップする。
    /// </summary>
    [SerializeField] private Transform visualRoot;

    [SerializeField] private SEEnum seEnum;

    /// <summary>Dissolve（崩壊）演出で溶かす対象のメッシュ。子の SkinnedMeshRenderer をアサインする。</summary>
    [SerializeField] private SkinnedMeshRenderer _skinnedMeshRenderer;

    /// <summary>シェーダーの _DissolveAmount プロパティID（Shader.PropertyToID でキャッシュ）。</summary>
    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

    /// <summary>個体ごとのマテリアルインスタンス。共有マテリアルを汚さないよう初回アクセスでキャッシュする。</summary>
    private Material _dissolveMaterialInstance;


    /// <summary>破壊エフェクト（DamageVFX）がアサインされているか。</summary>
    public bool HasDamageVFX => damageVFX != null;

    /// <summary>
    /// 死亡時の SE（敵種別ごとに seEnum で設定）を再生する。
    /// SoundManager 未初期化時は何もしない。
    /// </summary>
    public void PlayDeathSE()
    {
        SoundManager.Instance?.PlaySE(seEnum);
    }

    /// <summary>
    /// Dissolve 量（0=通常表示 / 1=完全消滅）をマテリアルに設定する（表示操作のみ）。
    /// 他の敵に影響しないよう、共有マテリアルではなく個体インスタンス（renderer.material）に対して設定する。
    /// </summary>
    /// <param name="value">_DissolveAmount に設定する値（0〜1）。</param>
    public void SetDissolveAmount(float value)
    {
        if (_skinnedMeshRenderer == null) return;

        // 初回アクセスでマテリアルがインスタンス化される（共有マテリアルは書き換わらない）。
        if (_dissolveMaterialInstance == null)
            _dissolveMaterialInstance = _skinnedMeshRenderer.material;

        if (_dissolveMaterialInstance.HasProperty(DissolveAmountId))
            _dissolveMaterialInstance.SetFloat(DissolveAmountId, value);
    }

    /// <summary>
    /// 破壊エフェクト（砕け散る VFX）を再生する。
    /// DamageVFX 未アサインの場合は何もせず即完了する。
    /// </summary>
    public UniTask PlayDamageVFXAsync()
    {
        if (damageVFX == null) return UniTask.CompletedTask;
        return damageVFX.DieAsync();
    }

    /// <summary>移動を担う NavMeshAgent。未設定時は Awake で自動取得する。</summary>

    private NavMeshAgent _agent;
    /// <summary>コンポーネント参照を確立する。</summary>
    private void Awake()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK()
    {
        if (animator == null) return;
        // CreatureMover の LookWeight ロジックと同様の設定
        animator.SetLookAtPosition(_lastTargetPosition);
        animator.SetLookAtWeight(1f, 0.3f, 0.7f, 1f);
    }

    /// <summary>エージェントの移動速度と加速度を設定する。NavMeshAgent がある場合のみ有効。</summary>
    /// <param name="speed">速度（m/s）。</param>
    /// <param name="acceleration">加速度（m/s²）。</param>
    public void SetMovementParams(float speed, float acceleration)
    {
        if (_agent == null) return;
        _agent.speed = speed;
        _agent.acceleration = acceleration;
    }

    /// <summary>
    /// 指定位置へ移動を開始する。
    /// NavMeshAgent がある場合は SetDestination、ない場合は CreatureMover.SetInput を呼ぶ。
    /// </summary>
    /// <param name="destination">目標位置（ワールド座標）。</param>
    public void SetDestination(Vector3 destination)
    {
        if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.SetDestination(destination);
            _lastTargetPosition = destination;
            return;
        }

        // CreatureMover が削除されたため、NavMeshAgent がない場合は移動不可
        Debug.LogWarning($"[EnemyView:{name}] NavMeshAgent がないため移動できません。");
    }

    /// <summary>
    /// スタン中に横揺れアニメーションを再生する。
    /// visualRoot を sin 波で左右にオフセットし、duration 秒後（または ct キャンセル時）に元位置へ戻す。
    /// </summary>
    public async UniTask PlayStunShakeAsync(float duration, float amplitude, float frequency, CancellationToken ct)
    {
        if (visualRoot == null)
        {
            Debug.LogWarning($"[EnemyView:{name}] visualRoot が未設定のためスタンシェイクをスキップします。");
            return;
        }

        var originalLocalPos = visualRoot.localPosition;
        float elapsed = 0f;
        try
        {
            while (elapsed < duration)
            {
                float offset = Mathf.Sin(elapsed * frequency * Mathf.PI * 2f) * amplitude;
                visualRoot.localPosition = originalLocalPos + Vector3.right * offset;
                elapsed += Time.deltaTime;
                await UniTask.Yield(ct);
            }
        }
        finally
        {
            if (visualRoot != null)
                visualRoot.localPosition = originalLocalPos;
        }
    }

    /// <summary>移動を停止する。</summary>
    public void StopMoving()
    {
        if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
            return;
        }
    }

    /// <summary>
    /// 外部の制御ロジックから計算されたアニメーションパラメータを適用します。
    /// </summary>
    public void SetAnimationParams(float vertical, float state)
    {
        if (animator == null) return;

        animator.SetFloat(verticalParam, vertical);
        animator.SetFloat(stateParam, state);
    }
}
