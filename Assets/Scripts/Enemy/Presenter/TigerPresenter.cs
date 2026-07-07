using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UniRx;

/// <summary>
/// 寅（Tiger）専用の Presenter。逃走する一般の敵（EnemyPresenter）とは異なり、
/// プレイヤーを発見すると追跡し、一定距離まで近づくと加速して突進、
/// さらに攻撃範囲に入ると Tiger_001_Attack（IsAttack トリガ）を再生してプレイヤーへダメージを与える。
/// 検知ロジック（FOV / 近接）と移動・アニメーション駆動は既存の EnemyState / EnemyView を流用する。
/// </summary>
public class TigerPresenter : EnemyBasePresenter, IStunnable
{
    /// <summary>検知パラメータ（DetectionRange / FieldOfViewAngle / ProximityRange を流用）。</summary>
    [Tooltip("検知パラメータ。DetectionRange / FieldOfViewAngle / ProximityRange を流用する EnemyState。")]
    [SerializeField] private EnemyState enemyState;

    /// <summary>移動・アニメーションを担う View。省略時は自動取得する。</summary>
    [Tooltip("移動・アニメーションを担う EnemyView。未設定なら同じ GameObject から自動取得。")]
    [SerializeField] private EnemyView view;

    /// <summary>視線の基準点（左目・右目の Transform）。FOV 判定の起点。</summary>
    [Tooltip("視線（FOV 判定）の基準点。左目・右目の Transform を登録する。")]
    [SerializeField] private Transform[] eyeTransforms;

    /// <summary>視線レイキャストで使うレイヤーマスク。デフォルトはすべてのレイヤー。</summary>
    [Tooltip("視線レイキャストで使うレイヤーマスク。既定は全レイヤー。")]
    [SerializeField] private LayerMask detectionMask = -1;

    [Header("Chase（通常追跡）")]
    /// <summary>通常追跡時の移動速度（m/s）。</summary>
    [Tooltip("通常追跡時の移動速度（m/s）。")]
    [SerializeField] private float chaseSpeed = 5f;

    /// <summary>通常追跡時の加速度（m/s²）。</summary>
    [Tooltip("通常追跡時の加速度（m/s²）。")]
    [SerializeField] private float chaseAcceleration = 12f;

    /// <summary>追跡時の旋回速度（度/秒）。</summary>
    [Tooltip("追跡時の旋回速度（度/秒）。")]
    [SerializeField] private float chaseTurnSpeed = 540f;

    [Header("Charge（近距離で加速突進）")]
    /// <summary>この距離以内に入ると突進（加速）に切り替える（m）。</summary>
    [Tooltip("この距離以内に入ると突進（加速）へ切り替える（m）。")]
    [SerializeField] private float accelerateRange = 6f;

    /// <summary>突進時の移動速度（m/s）。chaseSpeed より速くする。</summary>
    [Tooltip("突進時の移動速度（m/s）。chaseSpeed より速くする。")]
    [SerializeField] private float chargeSpeed = 10f;

    /// <summary>突進時の加速度（m/s²）。大きいほど一気に最高速へ達する。</summary>
    [Tooltip("突進時の加速度（m/s²）。大きいほど一気に最高速へ達する。")]
    [SerializeField] private float chargeAcceleration = 30f;

    [Header("Attack（攻撃）")]
    /// <summary>この距離以内で攻撃を開始する（m）。</summary>
    [Tooltip("この距離以内で攻撃を開始する（m）。")]
    [SerializeField] private float attackRange = 2.2f;

    /// <summary>攻撃モーション開始からヒット判定までの溜め時間（秒）。噛みつきが届く瞬間。</summary>
    [Tooltip("攻撃モーション開始からヒット判定までの溜め時間（秒）。噛みつきが届く瞬間。")]
    [SerializeField] private float attackHitDelay = 0.3f;

    /// <summary>攻撃モーション全体の所要時間（秒）。この間は移動せず正対し続ける。</summary>
    [Tooltip("攻撃モーション全体の所要時間（秒）。この間は移動せず正対し続ける。")]
    [SerializeField] private float attackDuration = 1.0f;

    /// <summary>攻撃の再使用までの間隔（秒）。</summary>
    [Tooltip("攻撃の再使用までの間隔（秒）。")]
    [SerializeField] private float attackCooldown = 2f;

    /// <summary>攻撃中にプレイヤーへ正対する旋回速度（度/秒）。</summary>
    [Tooltip("攻撃中にプレイヤーへ正対する旋回速度（度/秒）。")]
    [SerializeField] private float attackTurnSpeed = 720f;

    /// <summary>プレイヤーへ与えるダメージ。プレイヤーの最大HP以上にして一撃で倒す。</summary>
    [Tooltip("プレイヤーへ与えるダメージ。プレイヤーの最大HP以上にすると一撃で倒す。")]
    [SerializeField] private int attackDamage = 100;

    /// <summary>ヒット判定時にプレイヤーが居なければならない最大距離（m）。attackRange より少し広めにする。</summary>
    [Tooltip("ヒット判定時にプレイヤーが居なければならない最大距離（m）。attackRange より少し広めに。")]
    [SerializeField] private float attackHitRange = 3f;

    [Header("Run-Through（攻撃後の走り抜け）")]
    /// <summary>走り抜け時の移動速度（m/s）。隙を見せず一気に駆け抜ける。</summary>
    [Tooltip("走り抜け時の移動速度（m/s）。隙を見せず一気に駆け抜ける。")]
    [SerializeField] private float runThroughSpeed = 12f;

    /// <summary>前方へ走り抜ける距離（m）。この距離だけ進んだら歩みを止める。</summary>
    [Tooltip("前方へ走り抜ける距離（m）。この距離だけ進んだら歩みを止める。")]
    [SerializeField] private float runThroughDistance = 6f;

    /// <summary>走り抜けの最大継続時間（秒）。到達できなくてもこの時間で打ち切る。</summary>
    [Tooltip("走り抜けの最大継続時間（秒）。到達できなくてもこの時間で打ち切る。")]
    [SerializeField] private float runThroughMaxDuration = 1.5f;

    /// <summary>走り抜け完了とみなす目標までの残距離（m）。</summary>
    [Tooltip("走り抜け完了とみなす目標までの残距離（m）。")]
    [SerializeField] private float runThroughArriveThreshold = 0.6f;

    /// <summary>走り抜けて止まった後、再追跡に移るまで停止する時間（秒）。0 で即再追跡。</summary>
    [Tooltip("走り抜けて止まった後、次の行動へ移るまで停止する時間（秒）。0 で即移行。")]
    [SerializeField] private float runThroughStopDuration = 0.4f;

    [Header("Stalk（攻撃後の間合い取り・周回）")]
    /// <summary>周回時にプレイヤーから保つ距離（m）。</summary>
    [Tooltip("周回（間合い取り）でプレイヤーから保つ距離（m）。攻撃レンジより外側にして正面に居続けないようにする。")]
    [SerializeField] private float stalkRadius = 5f;

    /// <summary>周回時の移動速度（m/s）。</summary>
    [Tooltip("周回（間合い取り）時の移動速度（m/s）。")]
    [SerializeField] private float stalkSpeed = 4.5f;

    /// <summary>周回を続ける時間（秒）。経過後に再び追跡・攻撃へ移る。</summary>
    [Tooltip("周回（間合い取り）を続ける時間（秒）。経過後に再び追跡・攻撃へ移る。")]
    [SerializeField] private float stalkDuration = 2f;

    /// <summary>周回時に接線方向へ先読みする距離（m）。大きいほど速く周回する。</summary>
    [Tooltip("周回時に円周の接線方向へ先読みする距離（m）。大きいほど速く回り込む。")]
    [SerializeField] private float stalkStep = 2.5f;

    [Header("Dodge（プレイヤーの攻撃に反応して回避）")]
    /// <summary>プレイヤーがこの距離以内で攻撃したとき回避する（m）。</summary>
    [Tooltip("プレイヤーがこの距離以内で攻撃を始めたら回避する（m）。プレイヤーの噛みつきリーチ＋余裕の値に。")]
    [SerializeField] private float dodgeReactRange = 4f;

    /// <summary>回避（横移動）の距離（m）。</summary>
    [Tooltip("回避（横っ飛び）で移動する距離（m）。")]
    [SerializeField] private float dodgeDistance = 4f;

    /// <summary>回避時の移動速度（m/s）。素早く離脱させる。</summary>
    [Tooltip("回避時の移動速度（m/s）。素早く離脱させる。")]
    [SerializeField] private float dodgeSpeed = 12f;

    /// <summary>回避の最大継続時間（秒）。到達できなくてもこの時間で打ち切る。</summary>
    [Tooltip("回避の最大継続時間（秒）。到達できなくてもこの時間で打ち切る。")]
    [SerializeField] private float dodgeMaxDuration = 0.5f;

    /// <summary>回避完了とみなす目標までの残距離（m）。</summary>
    [Tooltip("回避完了とみなす目標までの残距離（m）。")]
    [SerializeField] private float dodgeArriveThreshold = 0.4f;

    /// <summary>連続回避を防ぐクールダウン（秒）。</summary>
    [Tooltip("連続回避を防ぐクールダウン（秒）。短すぎると回避ハメで完封されるので注意。")]
    [SerializeField] private float dodgeCooldown = 1.2f;

    /// <summary>回避時の旋回速度（度/秒）。</summary>
    [Tooltip("回避時の旋回速度（度/秒）。")]
    [SerializeField] private float dodgeTurnSpeed = 720f;

    [Header("Pre-Game（カウントダウン中の逃走）")]
    /// <summary>カウントダウン中など非プレイ時の逃走速度（m/s）。</summary>
    [Tooltip("カウントダウン中など非プレイ時にプレイヤーから逃げる速度（m/s）。")]
    [SerializeField] private float fleeSpeed = 6f;

    /// <summary>カウントダウン中の逃走でプレイヤーから離れる目標距離（m）。</summary>
    [Tooltip("カウントダウン中の逃走で、プレイヤーから離れる方向に取る目標距離（m）。")]
    [SerializeField] private float fleeDistance = 8f;

    [Header("Wander（未発見時のランダム徘徊）")]
    /// <summary>徘徊の目的地を選ぶ、初期位置からの半径（m）。</summary>
    [Tooltip("徘徊の目的地を選ぶ、初期位置を中心とした半径（m）。")]
    [SerializeField] private float wanderRadius = 10f;

    /// <summary>徘徊（歩行）時の移動速度（m/s）。追跡より遅くする。</summary>
    [Tooltip("徘徊（歩行）時の移動速度（m/s）。追跡より遅めにする。")]
    [SerializeField] private float wanderSpeed = 3f;

    /// <summary>徘徊時の加速度（m/s²）。</summary>
    [Tooltip("徘徊時の加速度（m/s²）。")]
    [SerializeField] private float wanderAcceleration = 8f;

    /// <summary>徘徊時の旋回速度（度/秒）。</summary>
    [Tooltip("徘徊時の旋回速度（度/秒）。")]
    [SerializeField] private float wanderTurnSpeed = 240f;

    /// <summary>目的地に到着したとみなす残距離（m）。</summary>
    [Tooltip("徘徊の目的地に到着したとみなす残距離（m）。")]
    [SerializeField] private float wanderArriveThreshold = 0.6f;

    /// <summary>1 回の歩行を続ける時間の最小・最大（秒）。到達できなくてもこの時間で打ち切る。</summary>
    [Tooltip("1 回の歩行を続ける時間の最小(x)・最大(y)（秒）。到達前でもこの時間で打ち切る。")]
    [SerializeField] private Vector2 wanderWalkDuration = new Vector2(2f, 4f);

    /// <summary>歩行後に立ち止まる時間の最小・最大（秒）。</summary>
    [Tooltip("歩行後に立ち止まる時間の最小(x)・最大(y)（秒）。")]
    [SerializeField] private Vector2 wanderPauseDuration = new Vector2(1f, 3f);

    [Header("Death")]
    /// <summary>撃破時に Dissolve（崩壊）させる秒数。</summary>
    [Tooltip("撃破時にメッシュを Dissolve（崩壊）させる秒数。")]
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

    /// <summary>攻撃後の走り抜け中か。走り抜け中は再攻撃せず直線移動に専念する。</summary>
    private bool _isRunningThrough;

    /// <summary>走り抜けを打ち切る時刻（Time.time 基準）。</summary>
    private float _runThroughEndTime;

    /// <summary>走り抜けの目標地点（前方 runThroughDistance 先、NavMesh 上）。</summary>
    private Vector3 _runThroughTarget;

    /// <summary>走り抜け後に停止している最中か。</summary>
    private bool _isPausingAfterRun;

    /// <summary>走り抜け後の停止を終える時刻（Time.time 基準）。</summary>
    private float _runPauseEndTime;

    /// <summary>間合い取り（周回）中か。</summary>
    private bool _isStalking;

    /// <summary>周回を終える時刻（Time.time 基準）。</summary>
    private float _stalkEndTime;

    /// <summary>周回の向き（+1=反時計回り / -1=時計回り）。</summary>
    private float _stalkDir = 1f;

    /// <summary>回避（横っ飛び）中か。</summary>
    private bool _isDodging;

    /// <summary>回避の目標地点（NavMesh 上）。</summary>
    private Vector3 _dodgeTarget;

    /// <summary>回避を打ち切る時刻（Time.time 基準）。</summary>
    private float _dodgeEndTime;

    /// <summary>次に回避可能になる時刻（クールダウン管理、Time.time 基準）。</summary>
    private float _nextDodgeTime;

    /// <summary>プレイヤーが攻撃を始めた通知。Tick の冒頭で消費して回避判定する。</summary>
    private bool _dodgeRequested;

    /// <summary>カウントダウン中など非プレイ時の逃走状態か。</summary>
    private bool _isFleeingPreGame;

    /// <summary>ネット被弾によるスタン中か。スタン中は攻撃・移動しない。</summary>
    private bool _isStunned;

    /// <summary>スタンが解除される時刻（Time.time 基準）。</summary>
    private float _stunEndTime;

    /// <summary>スタン揺れ UniTask のキャンセル用。再スタン時に前回を停止する。</summary>
    private CancellationTokenSource _shakeCts;

    /// <summary>死亡処理中か。死亡後は追跡・攻撃を停止する。</summary>
    private bool _isDead;

    /// <summary>徘徊の中心とする初期位置。生成時の足元を記録する。</summary>
    private Vector3 _wanderOrigin;

    /// <summary>徘徊モードが有効か（追跡・逃走から戻った際の初期化判定に使う）。</summary>
    private bool _wanderActive;

    /// <summary>徘徊フェーズが「歩行中」か（false=立ち止まり中）。</summary>
    private bool _wanderWalking;

    /// <summary>現在の徘徊目的地（NavMesh 上）。</summary>
    private Vector3 _wanderTarget;

    /// <summary>現在の徘徊フェーズ（歩行/立ち止まり）を終える時刻（Time.time 基準）。</summary>
    private float _wanderPhaseEndTime;

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

        // 徘徊の中心は生成時の足元（未発見の間はこの周辺をうろつく）。
        _wanderOrigin = transform.position;

        // 追跡対象（プレイヤー）へ常に正対させたいので、経路方向への自動旋回を切り
        // Presenter から FacePosition で向きを制御する。これにより索敵直後の経路再計算で
        // desiredVelocity が一瞬 0 になっても、向き直りがカクつかず滑らかになる。
        if (view != null) view.SetExternalFacing(true);

        // プレイヤーの噛みつき攻撃開始を購読し、近距離なら回避反応する。
        if (_playerTarget != null)
            _playerTarget.OnAttackStarted
                .Subscribe(_ => _dodgeRequested = true)
                .AddTo(this);

        Observable.EveryUpdate()
            .Subscribe(_ => Tick())
            .AddTo(this);
    }

    /// <summary>毎フレームの追跡・攻撃ステート更新。</summary>
    private void Tick()
    {
        if (_player == null || _isDead) return;

        // スタン中は一切行動しない（停止）。時間経過で解除する。
        if (_isStunned)
        {
            if (Time.time >= _stunEndTime)
            {
                _isStunned = false;
                _isFleeingPreGame = false;
                _movementParamsApplied = false; // 復帰後に移動パラメータを再適用させる
            }
            else
            {
                _dodgeRequested = false; // スタン中は回避要求を無視
                return;
            }
        }

        // カウントダウン中など「プレイ開始前」は攻撃せずプレイヤーから逃げる。
        // 入力（IsEnabled）が GO!! 表示で有効化されるまでは非プレイ扱い。
        if (!IsGameActive())
        {
            EnterOrUpdateFlee();
            return;
        }
        if (_isFleeingPreGame)
        {
            // ゲーム開始：逃走を解除して通常の追跡・攻撃へ移行する。
            _isFleeingPreGame = false;
            _isCharging = false;
            _movementParamsApplied = false;
        }

        // プレイヤーの攻撃に反応した回避要求を処理する（攻撃・走り抜け中は無視）。
        if (_dodgeRequested)
        {
            _dodgeRequested = false;
            TryBeginDodge();
        }

        // 回避（横っ飛び）中は最優先で離脱に専念する。完了後は周回（間合い取り）へ。
        if (_isDodging)
        {
            view.FacePosition(_dodgeTarget, dodgeTurnSpeed);
            float remaining = Vector3.Distance(transform.position, _dodgeTarget);
            if (Time.time >= _dodgeEndTime || remaining <= dodgeArriveThreshold)
            {
                _isDodging = false;
                view.StopMoving();
                BeginStalk();
            }
            return;
        }

        // 攻撃中は移動せず、プレイヤーへ正対し続ける（攻撃完了は UniTask 側で解除）。
        if (_isAttacking)
        {
            view.FacePosition(_player.position, attackTurnSpeed);
            return;
        }

        // 走り抜け中は再攻撃・追跡を止め、前方 runThroughDistance 先まで直線で駆け抜ける。
        if (_isRunningThrough)
        {
            view.FacePosition(_runThroughTarget, chaseTurnSpeed);
            float remaining = Vector3.Distance(transform.position, _runThroughTarget);
            if (Time.time >= _runThroughEndTime || remaining <= runThroughArriveThreshold)
            {
                // 目標まで走り切ったら歩みを止める。
                _isRunningThrough = false;
                view.StopMoving();
                _isPausingAfterRun = true;
                _runPauseEndTime = Time.time + runThroughStopDuration;
            }
            return;
        }

        // 走り抜け後の停止中。停止時間が過ぎたら周回（間合い取り）へ移る。
        if (_isPausingAfterRun)
        {
            if (Time.time < _runPauseEndTime)
            {
                view.FacePosition(_player.position, chaseTurnSpeed);
                return;
            }
            _isPausingAfterRun = false;
            BeginStalk();
            return;
        }

        // 周回（間合い取り）中。すぐ正面に戻らず外周を回り込み、時間経過で再攻撃へ移る。
        if (_isStalking)
        {
            if (Time.time < _stalkEndTime)
            {
                UpdateStalk();
                return;
            }
            _isStalking = false;
            _isCharging = false;
            _movementParamsApplied = false; // 追跡パラメータを再適用させる
        }

        float dist = Vector3.Distance(transform.position, _player.position);

        // まだ発見していなければ、FOV か近接で検知する。
        if (!_hasDetected)
        {
            if (CheckFovDetection() || dist < _state.ProximityRange)
            {
                _hasDetected = true;
                // 発見したら徘徊を解除し、追跡用の移動パラメータを再適用させる。
                _wanderActive = false;
                _movementParamsApplied = false;
            }
            else
            {
                // 未発見の間は常時ランダムに歩く⇔止まるを繰り返す。
                UpdateWander();
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
    /// ネットに被弾した際に行動を停止する（他エネミーと同じスタン）。
    /// 停止時間・揺れパラメータは EnemyState から取得し、進行中の行動を解除して停止する。
    /// 再スタン時は前回の揺れをキャンセルして新しい揺れを開始する。
    /// </summary>
    public void Stun()
    {
        if (_isDead) return;

        _isStunned = true;
        _stunEndTime = Time.time + (_state != null ? _state.StunDuration : 3f);

        // 進行中の行動を全て解除して停止する。
        _isAttacking = _isDodging = _isStalking = _isRunningThrough = _isPausingAfterRun = false;
        _isFleeingPreGame = false;
        _isCharging = false;
        _wanderActive = false;
        _movementParamsApplied = false;
        _dodgeRequested = false;
        view.StopMoving();

        _shakeCts?.Cancel();
        _shakeCts?.Dispose();
        _shakeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        if (_state != null)
            view.PlayStunShakeAsync(_state.StunDuration, _state.StunShakeAmplitude,
                _state.StunShakeFrequency, _shakeCts.Token).Forget();
    }

    /// <summary>
    /// 未発見・プレイ中のランダム徘徊を更新する。
    /// 「ランダムな地点まで歩く」→「ランダムな時間立ち止まる」を繰り返す。
    /// 外部旋回モード（SetExternalFacing(true)）のため、向きは FacePosition で制御する。
    /// </summary>
    private void UpdateWander()
    {
        // 追跡・逃走から戻った直後の初期化。徘徊速度を適用し、まず少し立ち止まる。
        if (!_wanderActive)
        {
            _wanderActive = true;
            _wanderWalking = false;
            _wanderPhaseEndTime = Time.time + Random.Range(wanderPauseDuration.x, wanderPauseDuration.y);
            view.SetMovementParams(wanderSpeed, wanderAcceleration, wanderTurnSpeed);
            view.StopMoving();
            return;
        }

        if (_wanderWalking)
        {
            view.FacePosition(_wanderTarget, wanderTurnSpeed);
            float remaining = Vector3.Distance(transform.position, _wanderTarget);
            if (Time.time >= _wanderPhaseEndTime || remaining <= wanderArriveThreshold)
            {
                _wanderWalking = false;
                _wanderPhaseEndTime = Time.time + Random.Range(wanderPauseDuration.x, wanderPauseDuration.y);
                view.StopMoving();
            }
        }
        else
        {
            // 立ち止まり時間が過ぎたら次の目的地を選んで歩き出す。
            if (Time.time >= _wanderPhaseEndTime) PickWanderDestination();
        }
    }

    /// <summary>
    /// 徘徊の次の目的地を初期位置周辺からランダムに選び、歩行を開始する。
    /// NavMesh 上に有効な点が見つからない場合は短時間後に再試行する。
    /// </summary>
    private void PickWanderDestination()
    {
        Vector2 r = Random.insideUnitCircle * wanderRadius;
        Vector3 candidate = _wanderOrigin + new Vector3(r.x, 0f, r.y);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            _wanderTarget = hit.position;
            _wanderWalking = true;
            _wanderPhaseEndTime = Time.time + Random.Range(wanderWalkDuration.x, wanderWalkDuration.y);
            view.SetMovementParams(wanderSpeed, wanderAcceleration, wanderTurnSpeed);
            view.FacePosition(_wanderTarget, wanderTurnSpeed);
            view.SetDestination(_wanderTarget);
        }
        else
        {
            // 有効点が見つからなければ少し待って再挑戦（立ち止まりのまま）。
            _wanderPhaseEndTime = Time.time + 0.5f;
        }
    }

    /// <summary>
    /// ゲームがプレイ中（カウントダウン完了後・ポーズ/終了でない）かどうか。
    /// InputManager の入力有効状態を「プレイ中」の判定に流用する。
    /// </summary>
    private bool IsGameActive()
        => InputManager.Instance != null && InputManager.Instance.IsEnabled;

    /// <summary>
    /// 非プレイ時（カウントダウン中など）の逃走処理。プレイヤーと反対方向へ移動して距離を取る。
    /// 初回に進行中ステートをクリアし、逃走用の移動パラメータを設定する。
    /// </summary>
    private void EnterOrUpdateFlee()
    {
        if (!_isFleeingPreGame)
        {
            _isFleeingPreGame = true;
            // 進行中の行動を全てクリアして逃走に専念する。
            _isAttacking = _isDodging = _isStalking = _isRunningThrough = _isPausingAfterRun = false;
            _isCharging = false;
            _wanderActive = false;
            _movementParamsApplied = false;
            view.SetMovementParams(fleeSpeed, chaseAcceleration, chaseTurnSpeed);
        }

        // プレイヤーと反対方向 fleeDistance 先を目標にする。
        Vector3 away = transform.position - _player.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
        away.y = 0f;
        away.Normalize();

        Vector3 target = transform.position + away * fleeDistance;
        if (NavMesh.Raycast(transform.position, target, out NavMeshHit edge, NavMesh.AllAreas))
            target = edge.position;
        if (NavMesh.SamplePosition(target, out NavMeshHit snap, 2f, NavMesh.AllAreas))
            target = snap.position;

        view.FacePosition(target, chaseTurnSpeed);
        view.SetDestination(target);
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

        // 非プレイ時（カウントダウン中など）は万一攻撃が走ってもダメージを与えない。
        if (!_isDead && IsGameActive() && _player != null && _playerTarget != null)
        {
            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= attackHitRange)
                _playerTarget.TakeDamage(attackDamage);
        }

        float rest = attackDuration - attackHitDelay;
        if (rest > 0f)
            await UniTask.Delay(System.TimeSpan.FromSeconds(rest), cancellationToken: ct);

        _isAttacking = false;

        // 攻撃モーション終了後、隙を見せないよう直線で走り抜ける（プレイ中のみ）。
        if (!_isDead && IsGameActive()) BeginRunThrough();
    }

    /// <summary>
    /// 攻撃後の走り抜けを開始する。攻撃で正対した前方（プレイヤーを通り抜ける向き）へ、
    /// runThroughDistance で指定した距離だけ直線で走って止まる。
    /// 指定距離が NavMesh から外れる場合のみ、外れる手前（境界）で頭打ちにして場外へ出ないようにする。
    /// </summary>
    private void BeginRunThrough()
    {
        // 前方（攻撃時にプレイヤーへ正対済み）。万一ゼロなら対プレイヤー方向にフォールバック。
        Vector3 dir = transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f && _player != null)
            dir = _player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        // 前方 runThroughDistance 先を目標にする。
        Vector3 target = transform.position + dir * runThroughDistance;

        // 目標が NavMesh 外なら、外れる手前（境界）に補正して場外へ出ないようにする（安全用）。
        if (NavMesh.Raycast(transform.position, target, out NavMeshHit edgeHit, NavMesh.AllAreas))
            target = edgeHit.position;

        _runThroughTarget = target;
        _isRunningThrough = true;
        _runThroughEndTime = Time.time + runThroughMaxDuration;
        _isCharging = false;
        _movementParamsApplied = false;

        view.SetMovementParams(runThroughSpeed, chargeAcceleration, chaseTurnSpeed);
        view.FacePosition(target, chaseTurnSpeed);
        view.SetDestination(target);
    }

    /// <summary>
    /// プレイヤーの攻撃に反応して回避できるか判定し、可能なら回避を開始する。
    /// 自分の攻撃中・走り抜け中・回避中・クールダウン中、または遠距離では回避しない。
    /// </summary>
    private void TryBeginDodge()
    {
        if (_isDead || _isDodging || _isAttacking || _isRunningThrough) return;
        if (Time.time < _nextDodgeTime) return;
        if (Vector3.Distance(transform.position, _player.position) > dodgeReactRange) return;

        BeginDodge();
    }

    /// <summary>
    /// 回避（横っ飛び）を開始する。プレイヤーに対して左右どちらか NavMesh 上で有効な方へ
    /// 素早く横移動する。両側が塞がっている場合は後方へバックステップする。
    /// </summary>
    private void BeginDodge()
    {
        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = transform.forward;
        toPlayer.Normalize();

        Vector3 perp = Vector3.Cross(Vector3.up, toPlayer); // プレイヤー方向に対する横方向
        Vector3 left = transform.position + perp * dodgeDistance;
        Vector3 right = transform.position - perp * dodgeDistance;

        bool leftOk = !NavMesh.Raycast(transform.position, left, out _, NavMesh.AllAreas);
        bool rightOk = !NavMesh.Raycast(transform.position, right, out _, NavMesh.AllAreas);

        Vector3 target;
        if (leftOk && rightOk) target = (Random.value < 0.5f) ? left : right;
        else if (leftOk) target = left;
        else if (rightOk) target = right;
        else
        {
            // 両側塞がり：後方へバックステップ（NavMesh 境界で頭打ち）。
            target = transform.position - toPlayer * dodgeDistance;
            if (NavMesh.Raycast(transform.position, target, out NavMeshHit back, NavMesh.AllAreas))
                target = back.position;
        }

        // 念のため NavMesh 上へスナップする。
        if (NavMesh.SamplePosition(target, out NavMeshHit snap, 1.5f, NavMesh.AllAreas))
            target = snap.position;

        _dodgeTarget = target;
        _isDodging = true;
        _isStalking = false;
        _isPausingAfterRun = false;
        _dodgeEndTime = Time.time + dodgeMaxDuration;
        _nextDodgeTime = Time.time + dodgeCooldown;
        _isCharging = false;
        _movementParamsApplied = false;

        view.SetMovementParams(dodgeSpeed, chargeAcceleration, dodgeTurnSpeed);
        view.FacePosition(target, dodgeTurnSpeed);
        view.SetDestination(target);
    }

    /// <summary>
    /// 周回（間合い取り）を開始する。攻撃直後すぐ正面へ戻らず、stalkRadius を保って
    /// プレイヤーの周囲を回り込み、隙を見せない立ち回りにする。
    /// </summary>
    private void BeginStalk()
    {
        _isStalking = true;
        _stalkEndTime = Time.time + stalkDuration;
        _stalkDir = (Random.value < 0.5f) ? 1f : -1f; // 周回方向をランダムに
        _isCharging = false;
        _movementParamsApplied = false;
        view.SetMovementParams(stalkSpeed, chaseAcceleration, chaseTurnSpeed);
    }

    /// <summary>
    /// 周回中の移動更新。プレイヤーを中心とした半径 stalkRadius の円周上で、
    /// 接線方向へ stalkStep 先の点を目標にして回り込む。NavMesh 外なら周回方向を反転する。
    /// </summary>
    private void UpdateStalk()
    {
        Vector3 toTiger = transform.position - _player.position;
        toTiger.y = 0f;
        Vector3 radial = toTiger.sqrMagnitude > 0.0001f ? toTiger.normalized : transform.forward;
        Vector3 tangent = Vector3.Cross(Vector3.up, radial) * _stalkDir;
        Vector3 aim = _player.position + radial * stalkRadius + tangent * stalkStep;

        if (NavMesh.SamplePosition(aim, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            aim = hit.position;
        }
        else
        {
            // 目標が NavMesh 外：周回方向を反転して内側に留める。
            _stalkDir = -_stalkDir;
            tangent = Vector3.Cross(Vector3.up, radial) * _stalkDir;
            aim = _player.position + radial * stalkRadius + tangent * stalkStep;
            if (NavMesh.SamplePosition(aim, out hit, 2f, NavMesh.AllAreas)) aim = hit.position;
        }

        view.FacePosition(aim, chaseTurnSpeed);
        view.SetDestination(aim);
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

    /// <summary>クローンした EnemyState と進行中の揺れタスクを破棄する。</summary>
    private void OnDestroy()
    {
        _shakeCts?.Cancel();
        _shakeCts?.Dispose();
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

        // 赤＝回避反応距離、紫＝周回半径。
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, dodgeReactRange);
        if (_player != null)
        {
            Gizmos.color = new Color(0.7f, 0.3f, 1f, 0.5f);
            Gizmos.DrawWireSphere(_player.position, stalkRadius);
        }

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
