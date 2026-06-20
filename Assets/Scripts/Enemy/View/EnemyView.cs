using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMeshAgent を操作するエネミーの View クラス。
/// 移動指示のみを受け付け、判定ロジックは持たない。
/// 移動に応じたアニメーション駆動（旧 CreatureMover.AnimationHandler）を内包し、
/// 毎フレーム NavMeshAgent の速度から Animator パラメータを平滑化して設定する。
/// </summary>
public class EnemyView : MonoBehaviour
{

    [SerializeField] private DamageVFX damageVFX;

    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private string verticalParam = "Vert";
    [SerializeField] private string stateParam = "State";
    [SerializeField] private string attackParam = "Attack";
    private Vector3 _lastTargetPosition;

    // --- ロコモーションアニメーション補間（CreatureMover.AnimationHandler から移植）---
    /// <summary>速度変化に対するアニメーション値の追従速度（CreatureMover の k_InputFlow 相当）。</summary>
    private const float AnimInputFlow = 4.5f;
    /// <summary>前後左右移動量の平滑化値（verticalParam=Vert 用）。</summary>
    private Vector2 _flowAxis;
    /// <summary>歩行/走行ステートの平滑化値（stateParam=State 用）。</summary>
    private float _flowState;

    /// <summary>
    /// スタン中に横揺れさせるビジュアル用の子 Transform。
    /// NavMeshAgent はルート Transform を制御するため、揺れはこの子で行う。
    /// 未設定の場合はシェイクをスキップする。
    /// </summary>
    [SerializeField] private Transform visualRoot;

    [SerializeField] private SEEnum seEnum;

    /// <summary>Dissolve（崩壊）演出で溶かす対象のメッシュ。子の SkinnedMeshRenderer をアサインする。</summary>
    [SerializeField] private SkinnedMeshRenderer _skinnedMeshRenderer;

    [SerializeField] private NavMeshAgent _agent;
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

    /// <summary>コンポーネント参照を確立する。</summary>
    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 毎フレーム、NavMeshAgent の速度からロコモーションアニメーションを駆動する。
    /// CreatureMover が内部で行っていた「移動量 → Animator パラメータ」変換と平滑化を移植したもの。
    /// </summary>
    private void Update()
    {
        UpdateLocomotionAnimation(Time.deltaTime);
    }

    /// <summary>
    /// NavMeshAgent の速度をローカル空間の前後左右成分に変換し、最大速度で正規化したうえで
    /// CreatureMover.AnimationHandler と同じ平滑化を行って Animator に反映する。
    /// </summary>
    private void UpdateLocomotionAnimation(float deltaTime)
    {
        if (animator == null) return;

        Vector3 velocity = _agent != null ? _agent.velocity : Vector3.zero;
        float maxSpeed = (_agent != null && _agent.speed > Mathf.Epsilon) ? _agent.speed : 1f;

        // ワールド速度をローカル前後左右成分へ変換し、最大速度で 0〜1 に正規化（GenAnimationAxis 相当）。
        Vector2 axis = new Vector2(
            Vector3.Dot(velocity, transform.right),
            Vector3.Dot(velocity, transform.forward)) / maxSpeed;
        axis = Vector2.ClampMagnitude(axis, 1f);

        // 移動していれば走行ステート(1)、停止していれば待機ステート(0)へ寄せる。
        float state = velocity.sqrMagnitude > 0.0001f ? 1f : 0f;

        // CreatureMover.AnimationHandler.Animate と同一の平滑化ロジック。
        animator.SetFloat(verticalParam, _flowAxis.magnitude);
        animator.SetFloat(stateParam, Mathf.Clamp01(_flowState));

        _flowAxis = Vector2.ClampMagnitude(_flowAxis + AnimInputFlow * deltaTime * (axis - _flowAxis).normalized, 1f);
        _flowState = Mathf.Clamp01(_flowState + AnimInputFlow * deltaTime * Mathf.Sign(state - _flowState));
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
}
