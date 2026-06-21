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
    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private string verticalParam = "Vert";
    [SerializeField] private string stateParam = "State";
    [SerializeField] private string attackParam = "Attack";
    /// <summary>攻撃モーションを発火する Trigger パラメータ名（Tiger.controller の IsAttack）。</summary>
    [SerializeField] private string attackTrigger = "IsAttack";
    private Vector3 _lastTargetPosition;

    // --- ロコモーションアニメーション補間（CreatureMover.AnimationHandler から移植）---
    /// <summary>速度変化に対するアニメーション値の追従速度（CreatureMover の k_InputFlow 相当）。</summary>
    private const float AnimInputFlow = 4.5f;
    /// <summary>前後左右移動量の平滑化値（verticalParam=Vert 用）。</summary>
    private Vector2 _flowAxis;
    /// <summary>歩行/走行ステートの平滑化値（stateParam=State 用）。</summary>
    private float _flowState;

    /// <summary>IK 注視に使う、平滑化された注視先。離散的に飛ぶ _lastTargetPosition を滑らかに追従する。</summary>
    private Vector3 _smoothedLookPosition;
    private bool _hasLookPosition;
    /// <summary>注視先の追従速度（1/秒）。大きいほど素早く向き直る。</summary>
    private const float LookFollowSpeed = 5f;

    /// <summary>
    /// 進行方向へ向き直る旋回速度（度/秒）。
    /// NavMeshAgent の自動回転（updateRotation）は 180° 反転時に左右どちらに回るかが
    /// 毎フレーム取り合いになりカクつくため、自動回転を切ってこの速度で手動旋回する。
    /// </summary>
    private float _turnSpeedDeg = 540f;

    /// <summary>
    /// 旋回を外部（Presenter）が制御するモード。true の間 UpdateRotation は何もしない。
    /// 寅のように「経路方向」ではなく「ターゲット方向」へ正対させたい場合に使う。
    /// </summary>
    private bool _externalFacing;

    /// <summary>直近の有効な進行方向（desiredVelocity が一瞬 0 に落ちても回転を継続するため保持）。</summary>
    private Vector3 _lastFacingDir;
    private bool _hasFacingDir;

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

    [SerializeField] private　ParticleSystem _particleSystem;

    [SerializeField] private CharacterController _characterController;

    /// <summary>シェーダーの _DissolveAmount プロパティID（Shader.PropertyToID でキャッシュ）。</summary>
    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

    /// <summary>個体ごとのマテリアルインスタンス。共有マテリアルを汚さないよう初回アクセスでキャッシュする。</summary>
    private Material _dissolveMaterialInstance;

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
        _characterController.enabled = false;
        if (_particleSystem == null) return UniTask.CompletedTask;
        _particleSystem.gameObject.SetActive(true);
        _particleSystem.Play();
        return UniTask.CompletedTask;
    }

    /// <summary>移動を担う NavMeshAgent。未設定時は Awake で自動取得する。</summary>

    /// <summary>コンポーネント参照を確立する。</summary>
    private void Awake()
    {
        _particleSystem.gameObject.SetActive(false);
        if (animator == null) animator = GetComponent<Animator>();

        // Agent の自動回転を停止し、Update で進行方向へ滑らかに手動旋回する。
        // 180° 反転時の回転軸あいまいによるカクツキを防ぐため。
        if (_agent != null) _agent.updateRotation = false;
    }

    /// <summary>
    /// 毎フレーム、NavMeshAgent の速度からロコモーションアニメーションを駆動する。
    /// CreatureMover が内部で行っていた「移動量 → Animator パラメータ」変換と平滑化を移植したもの。
    /// </summary>
    private void Update()
    {
        UpdateLocomotionAnimation(Time.deltaTime);
        UpdateRotation(Time.deltaTime);

        // 離散的に更新される注視先（逃走先）を毎フレーム滑らかに追従させ、IK のビクつきを防ぐ。
        if (_hasLookPosition)
        {
            _smoothedLookPosition = Vector3.Lerp(
                _smoothedLookPosition, _lastTargetPosition,
                1f - Mathf.Exp(-LookFollowSpeed * Time.deltaTime));
        }
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

        // 目標値へ一定速度で追従しつつ、到達したら正確に停止する（オーバーシュート＝振動を防ぐ）。
        // CreatureMover の normalized/Sign による固定ステップは目標付近で永久に振動するため MoveTowards に変更。
        _flowAxis = Vector2.MoveTowards(_flowAxis, axis, AnimInputFlow * deltaTime);
        _flowState = Mathf.MoveTowards(_flowState, state, AnimInputFlow * deltaTime);

        animator.SetFloat(verticalParam, _flowAxis.magnitude);
        animator.SetFloat(stateParam, Mathf.Clamp01(_flowState));
    }

    /// <summary>
    /// NavMeshAgent の進行方向（desiredVelocity）へ一定角速度で滑らかに旋回する。
    /// updateRotation=false で自動回転を切ったうえで本処理が向きを制御する。
    /// desiredVelocity は経路の進みたい方向を即座に反映するため、実速度 velocity を使うより
    /// 旋回が遅れず、真後ろ（180°）への逃走でも左右にブレずに転回できる。
    /// </summary>
    private void UpdateRotation(float deltaTime)
    {
        // 旋回を Presenter 側（FacePosition）に委ねている場合はここでは何もしない。
        if (_externalFacing || _agent == null) return;

        // 進行したい方向。
        Vector3 dir = _agent.desiredVelocity;
        dir.y = 0f;

        // desiredVelocity は経路再計算（SetDestination 直後）や AutoBraking による減速で
        // 瞬間的に 0 付近へ落ちる。その間に return して回転をスキップすると「カクッ」と途切れて
        // 見えるため、直近の有効な向きを保持して連続的に旋回させる。
        if (dir.sqrMagnitude < 0.0001f)
        {
            if (!_hasFacingDir) return;
            dir = _lastFacingDir;
        }
        else
        {
            _lastFacingDir = dir.normalized;
            _hasFacingDir = true;
        }

        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, _turnSpeedDeg * deltaTime);
    }

    /// <summary>
    /// 旋回の外部制御モードを切り替える。
    /// true にすると UpdateRotation（経路方向への自動旋回）を停止し、
    /// Presenter が FacePosition で向きを制御できるようにする。
    /// </summary>
    /// <param name="enabled">外部制御を有効にするか。</param>
    public void SetExternalFacing(bool enabled)
    {
        _externalFacing = enabled;
    }

    private void OnAnimatorIK()
    {
        if (animator == null || !_hasLookPosition) return;
        // CreatureMover の LookWeight ロジックと同様の設定（注視先は平滑化済みの値を使う）
        animator.SetLookAtPosition(_smoothedLookPosition);
        animator.SetLookAtWeight(1f, 0.3f, 0.7f, 1f);
    }

    /// <summary>エージェントの移動速度・加速度・旋回速度を設定する。NavMeshAgent がある場合のみ有効。</summary>
    /// <param name="speed">速度（m/s）。</param>
    /// <param name="acceleration">加速度（m/s²）。</param>
    /// <param name="turnSpeedDeg">旋回速度（度/秒）。手動旋回（UpdateRotation）で使用する。</param>
    public void SetMovementParams(float speed, float acceleration, float turnSpeedDeg)
    {
        if (_agent == null) return;
        _agent.speed = speed;
        _agent.acceleration = acceleration;
        _turnSpeedDeg = turnSpeedDeg;
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
            // 初回はスナップ、以降は Update で滑らかに追従する。
            if (!_hasLookPosition)
            {
                _smoothedLookPosition = destination;
                _hasLookPosition = true;
            }
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

    /// <summary>
    /// 攻撃モーション（Tiger_001_Attack）を発火する。
    /// AnimatorController 側で AnyState→Attack の遷移が attackTrigger（IsAttack）で用意されている前提。
    /// </summary>
    public void PlayAttack()
    {
        if (animator != null) animator.SetTrigger(attackTrigger);
    }

    /// <summary>
    /// 指定したワールド座標の方向へ一定角速度で旋回し正対する（水平成分のみ）。
    /// NavMeshAgent 停止中（攻撃中など）に手動で向きを合わせるために使う。
    /// </summary>
    /// <param name="worldPos">向きたい対象のワールド座標。</param>
    /// <param name="turnSpeedDeg">旋回速度（度/秒）。</param>
    public void FacePosition(Vector3 worldPos, float turnSpeedDeg)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, turnSpeedDeg * Time.deltaTime);
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