using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniRx;

/// <summary>
/// 寅（Tiger）専用の Presenter。逃走する一般の敵（EnemyPresenter）とは異なり、
/// プレイヤーを発見すると追跡し、一定距離まで近づくと加速して突進、
/// さらに攻撃範囲に入ると Tiger_001_Attack（IsAttack トリガ）を再生してプレイヤーへダメージを与える。
/// 検知ロジック（FOV / 近接）と移動・アニメーション駆動は既存の EnemyState / EnemyView を流用する。
/// </summary>
public class TigerPresenter : EnemyBasePresenter
{
    /// <summary>検知パラメータ（DetectionRange / FieldOfViewAngle / ProximityRange を流用）。</summary>
    [SerializeField] private EnemyState enemyState;

    /// <summary>移動・アニメーションを担う View。省略時は自動取得する。</summary>
    [SerializeField] private EnemyView view;

    /// <summary>視線の基準点（左目・右目の Transform）。FOV 判定の起点。</summary>
    [SerializeField] private Transform[] eyeTransforms;

    /// <summary>視線レイキャストで使うレイヤーマスク。デフォルトはすべてのレイヤー。</summary>
    [SerializeField] private LayerMask detectionMask = -1;

    [Header("Chase（通常追跡）")]
    /// <summary>通常追跡時の移動速度（m/s）。</summary>
    [SerializeField] private float chaseSpeed = 5f;

    /// <summary>通常追跡時の加速度（m/s²）。</summary>
    [SerializeField] private float chaseAcceleration = 12f;

    /// <summary>追跡時の旋回速度（度/秒）。</summary>
    [SerializeField] private float chaseTurnSpeed = 540f;

    [Header("Charge（近距離で加速突進）")]
    /// <summary>この距離以内に入ると突進（加速）に切り替える（m）。</summary>
    [SerializeField] private float accelerateRange = 6f;

    /// <summary>突進時の移動速度（m/s）。chaseSpeed より速くする。</summary>
    [SerializeField] private float chargeSpeed = 10f;

    /// <summary>突進時の加速度（m/s²）。大きいほど一気に最高速へ達する。</summary>
    [SerializeField] private float chargeAcceleration = 30f;

    [Header("Attack（攻撃）")]
    /// <summary>この距離以内で攻撃を開始する（m）。</summary>
    [SerializeField] private float attackRange = 2.2f;

    /// <summary>攻撃モーション開始からヒット判定までの溜め時間（秒）。噛みつきが届く瞬間。</summary>
    [SerializeField] private float attackHitDelay = 0.3f;

    /// <summary>攻撃モーション全体の所要時間（秒）。この間は移動せず正対し続ける。</summary>
    [SerializeField] private float attackDuration = 1.0f;

    /// <summary>攻撃の再使用までの間隔（秒）。</summary>
    [SerializeField] private float attackCooldown = 2f;

    /// <summary>攻撃中にプレイヤーへ正対する旋回速度（度/秒）。</summary>
    [SerializeField] private float attackTurnSpeed = 720f;

    /// <summary>プレイヤーへ与えるダメージ。プレイヤーの最大HP以上にして一撃で倒す。</summary>
    [SerializeField] private int attackDamage = 100;

    /// <summary>ヒット判定時にプレイヤーが居なければならない最大距離（m）。attackRange より少し広めにする。</summary>
    [SerializeField] private float attackHitRange = 3f;

    [Header("Death")]
    /// <summary>撃破時に Dissolve（崩壊）させる秒数。</summary>
    [SerializeField] private float _dissolveDuration = 0.25f;

    private EnemyState _state;
    private Transform _player;

    /// <summary>プレイヤーの被ダメージ受け口。</summary>
    private Player _playerTarget;

    /// <summary>一度プレイヤーを発見したか。発見後は追跡を継続する。</summary>
    private bool _hasDetected;

    /// <summary>現在突進（加速）状態か。移動パラメータの切り替え判定に使う。</summary>
    private bool _isCharging;

    /// <summary>移動パラメータを一度でも適用したか。初回の強制適用に使う。</summary>
    private bool _movementParamsApplied;

    /// <summary>攻撃モーション再生中か。再生中は移動せず正対のみ行う。</summary>
    private bool _isAttacking;

    /// <summary>次に攻撃可能になる時刻（Time.time 基準）。</summary>
    private float _nextAttackTime;

    /// <summary>死亡処理中か。死亡後は追跡・攻撃を停止する。</summary>
    private bool _isDead;

    /// <summary>EnemyState を per-instance にクローンし、View を取得する。</summary>
    protected override void Awake()
    {
        base.Awake();
        if (enemyState != null) _state = ScriptableObject.Instantiate(enemyState);
        if (view == null) view = GetComponent<EnemyView>();
    }

    /// <summary>プレイヤーを検索し、毎フレームの追跡・攻撃判定を開始する。</summary>
    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogWarning($"[TigerPresenter:{name}] Player タグが見つかりません。追跡ロジックを無効化します。");
            return;
        }
        _player = playerObj.transform;
        _playerTarget = playerObj.GetComponentInChildren<Player>();

        // 追跡対象（プレイヤー）へ常に正対させたいので、経路方向への自動旋回を切り
        // Presenter から FacePosition で向きを制御する。これにより索敵直後の経路再計算で
        // desiredVelocity が一瞬 0 になっても、向き直りがカクつかず滑らかになる。
        if (view != null) view.SetExternalFacing(true);

        Observable.EveryUpdate()
            .Subscribe(_ => Tick())
            .AddTo(this);
    }

    /// <summary>毎フレームの追跡・攻撃ステート更新。</summary>
    private void Tick()
    {
        if (_player == null || _isDead) return;

        // 攻撃中は移動せず、プレイヤーへ正対し続ける（攻撃完了は UniTask 側で解除）。
        if (_isAttacking)
        {
            view.FacePosition(_player.position, attackTurnSpeed);
            return;
        }

        float dist = Vector3.Distance(transform.position, _player.position);

        // まだ発見していなければ、FOV か近接で検知する。
        if (!_hasDetected)
        {
            if (CheckFovDetection() || dist < _state.ProximityRange)
            {
                _hasDetected = true;
            }
            else
            {
                view.StopMoving();
                return;
            }
        }

        // 攻撃範囲内かつクールダウン明けなら攻撃開始。
        if (dist <= attackRange && Time.time >= _nextAttackTime)
        {
            BeginAttack();
            return;
        }

        // 追跡：近距離では突進（加速）へ切り替える。
        bool charge = dist <= accelerateRange;
        if (!_movementParamsApplied || charge != _isCharging)
        {
            _isCharging = charge;
            _movementParamsApplied = true;
            if (charge)
                view.SetMovementParams(chargeSpeed, chargeAcceleration, chaseTurnSpeed);
            else
                view.SetMovementParams(chaseSpeed, chaseAcceleration, chaseTurnSpeed);
        }

        // 経路方向ではなくプレイヤー方向へ毎フレーム滑らかに正対する（カクつき防止）。
        view.FacePosition(_player.position, chaseTurnSpeed);
        view.SetDestination(_player.position);
    }

    /// <summary>
    /// 攻撃を開始する。移動を止め、Tiger_001_Attack（IsAttack）を再生し、
    /// 溜め時間後にプレイヤーが範囲内なら一撃でダメージを与える。
    /// </summary>
    private void BeginAttack()
    {
        _isAttacking = true;
        _nextAttackTime = Time.time + attackCooldown;
        _isCharging = false;
        _movementParamsApplied = false;
        view.StopMoving();
        view.FacePosition(_player.position, attackTurnSpeed);
        view.PlayAttack();
        AttackRoutineAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>
    /// 攻撃モーションのヒット判定タイミングを待ち、プレイヤーが攻撃範囲内なら
    /// ダメージを与える。攻撃終了後に移動可能状態へ戻す。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    private async UniTask AttackRoutineAsync(CancellationToken ct)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(attackHitDelay), cancellationToken: ct);

        if (!_isDead && _player != null && _playerTarget != null)
        {
            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= attackHitRange)
                _playerTarget.TakeDamage(attackDamage);
        }

        float rest = attackDuration - attackHitDelay;
        if (rest > 0f)
            await UniTask.Delay(System.TimeSpan.FromSeconds(rest), cancellationToken: ct);

        _isAttacking = false;
    }

    /// <summary>
    /// 全 eyeTransforms からレイキャスト付き FOV 判定を行い、
    /// いずれかの目からプレイヤーが見えた場合 true を返す。
    /// 障害物がなければ可視、最初のヒットが Player タグなら可視と判定する。
    /// </summary>
    private bool CheckFovDetection()
    {
        if (eyeTransforms == null || eyeTransforms.Length == 0) return false;

        float halfFov = _state.FieldOfViewAngle * 0.5f;
        float range = _state.DetectionRange;

        foreach (var eye in eyeTransforms)
        {
            if (eye == null) continue;

            Vector3 toPlayer = _player.position - eye.position;
            float dist = toPlayer.magnitude;
            if (dist >= range) continue;

            float angle = Vector3.Angle(eye.forward, toPlayer / dist);
            if (angle >= halfFov) continue;

            if (!Physics.Raycast(eye.position, toPlayer / dist, out RaycastHit hit, dist,
                    detectionMask, QueryTriggerInteraction.Collide)
                || hit.transform.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 死亡時の破棄処理。メッシュを Dissolve（崩壊）させてから破棄する。
    /// SE 再生・VFX 再生・コライダー無効化を行う（EnemyPresenter と同方針）。
    /// </summary>
    protected override void OnDie()
    {
        _isDead = true;
        view.StopMoving();
        view.PlayDeathSE();
        view.PlayDamageVFXAsync().Forget();

        foreach (var col in GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        PlayDissolveAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>
    /// メッシュの Dissolve（崩壊）演出を再生し、完了後に本体を破棄する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    private async UniTask PlayDissolveAsync(CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < _dissolveDuration)
        {
            elapsed += Time.deltaTime;
            view.SetDissolveAmount(Mathf.Clamp01(elapsed / _dissolveDuration));
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
        view.SetDissolveAmount(1f);

        if (this != null) Destroy(gameObject);
    }

    /// <summary>クローンした EnemyState を破棄する。</summary>
    private void OnDestroy()
    {
        if (_state != null) Destroy(_state);
    }

#if UNITY_EDITOR
    /// <summary>
    /// シーンビューに判定範囲を表示する。
    /// 黄球＝目、緑球＝攻撃範囲、橙球＝加速開始範囲、シアン球＝近接感知。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, accelerateRange);

        if (enemyState != null)
        {
            Gizmos.color = new Color(0f, 0.85f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, enemyState.ProximityRange);
        }

        if (eyeTransforms != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0f, 1f);
            foreach (var eye in eyeTransforms)
                if (eye != null) Gizmos.DrawSphere(eye.position, 0.08f);
        }
    }
#endif
}
