using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UniRx;

/// <summary>
/// エネミーの視野角・距離判定と逃走ロジックを担う Presenter クラス。
/// 毎フレーム Observable.EveryUpdate() で判定し、EnemyState へ状態を書き込む。
/// EnemyState の変化を Subscribe して EnemyView に移動指示を出す。
/// 判定①：左右の eyeTransforms を起点としたレイキャスト付き FOV 感知
/// 判定②：ボディ中心からの近接全方位球体（近接感知）
/// </summary>
public class EnemyPresenter : EnemyBasePresenter
{
    /// <summary>検知パラメータ（敵ごとに専用の EnemyState.asset をアサインする）。</summary>
    [SerializeField, FormerlySerializedAs("enemyStateTemplate")]
    private EnemyState enemyState;

    /// <summary>移動指示を受け取る View。省略時は自動取得する。</summary>
    [SerializeField] private EnemyView view;

    /// <summary>視線の基準点（左目・右目の Transform を登録する）。</summary>
    [SerializeField] private Transform[] eyeTransforms;

    /// <summary>視線レイキャストで使うレイヤーマスク。デフォルトはすべてのレイヤー。</summary>
    [SerializeField] private LayerMask detectionMask = -1;

    /// <summary>撃破時に Dissolve（崩壊）させる秒数。</summary>
    [SerializeField] private float _dissolveDuration = 0.25f;

    private EnemyState _state;
    private Transform _player;

    /// <summary>現在逃走中かどうか。ヒステリシス判定に使う。</summary>
    private bool _isFleeing;

    /// <summary>スタン（行動停止）中かどうか。</summary>
    private bool _isStunned;

    /// <summary>死亡処理中かどうか。死亡後は逃走判定・スタンを停止する。</summary>
    private bool _isDead;

    /// <summary>スタンが解除される時刻（Time.time 基準）。</summary>
    private float _stunEndTime;

    /// <summary>シェイク UniTask のキャンセル用。再スタン時に前回シェイクを停止する。</summary>
    private CancellationTokenSource _shakeCts;

    /// <summary>EnemyState を per-instance にクローンし、View を取得する。</summary>
    protected override void Awake()
    {
        base.Awake();
        _state = ScriptableObject.Instantiate(enemyState);
        if (view == null) view = GetComponent<EnemyView>();
    }

    /// <summary>プレイヤーを検索し、行動状態の購読と毎フレーム判定を開始する。</summary>
    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogWarning($"[EnemyPresenter:{name}] Player タグが見つかりません。逃走ロジックを無効化します。");
            return;
        }
        _player = playerObj.transform;
        Debug.Log($"[EnemyPresenter:{name}] Player 発見。FleeSpeed={_state.FleeSpeed} Accel={_state.FleeAcceleration}");
        view.SetMovementParams(_state.FleeSpeed, _state.FleeAcceleration);

        _state.CurrentBehavior
            .Where(b => b == EnemyBehavior.Idle)
            .Subscribe(_ => view.StopMoving())
            .AddTo(this);

        Observable.EveryUpdate()
            .Subscribe(_ => UpdateDetection())
            .AddTo(this);
    }

    /// <summary>視野角・近接・ヒステリシスに基づいて逃走状態を更新する。</summary>
    private float _debugLogTimer;

    private void UpdateDetection()
    {
        if (_player == null || _isDead) return;

        // スタン中は逃走判定を行わず行動を停止する。時間経過で Idle に復帰する。
        if (_isStunned)
        {
            if (Time.time >= _stunEndTime)
            {
                _isStunned = false;
                _state.SetBehavior(EnemyBehavior.Idle);
            }
            else return;
        }

        // 判定① FOV 感知（各目からレイキャストで実際に見えているか確認）
        bool inFov = CheckFovDetection();

        // 判定② 近接感知（ボディ中心から全方位）
        float bodyDist = Vector3.Distance(transform.position, _player.position);
        bool inProximity = bodyDist < _state.ProximityRange;

        float stopDist = _state.DetectionRange * _state.FleeStopMultiplier;

        _debugLogTimer -= Time.deltaTime;
        if (_debugLogTimer <= 0f)
        {
            _debugLogTimer = 1f;
            Debug.Log($"[EnemyPresenter:{name}] inFov={inFov} bodyDist={bodyDist:F1} inProximity={inProximity} isFleeing={_isFleeing}");
        }

        if (!_isFleeing)
        {
            if (inFov || inProximity) BeginFlee();
        }
        else
        {
            // 逃走停止：ボディ距離で判断する（向き直り中に背後判定が入るのを防ぐため）
            if (bodyDist > stopDist) EndFlee();
            else UpdateFleeDestination();
        }
    }

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

            // 障害物チェック：何も当たらない or 最初のヒットがプレイヤーなら可視
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
    /// Net に被弾した際に行動を停止する。
    /// 停止時間・シェイクパラメータは EnemyState（EnemyModel）から取得する。
    /// 再スタン時は前回のシェイクをキャンセルして新しいシェイクを開始する。
    /// </summary>
    public void Stun()
    {
        _isStunned = true;
        _stunEndTime = Time.time + _state.StunDuration;
        _isFleeing = false;
        _state.SetBehavior(EnemyBehavior.Stunned);
        view.StopMoving();

        _shakeCts?.Cancel();
        _shakeCts?.Dispose();
        _shakeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        view.PlayStunShakeAsync(_state.StunDuration, _state.StunShakeAmplitude, _state.StunShakeFrequency, _shakeCts.Token).Forget();
    }

    /// <summary>
    /// 死亡時の破棄処理をオーバーライドし、メッシュを Dissolve（崩壊）させてから破棄する。
    /// SE 再生・コライダー無効化を行い、Dissolve 完了後に本体を破棄する。
    /// （砕け散る VFX(DamageVFX) は呼び出さない方針。コンポーネント自体は残置）
    /// </summary>
    protected override void OnDie()
    {
        _isDead = true;
        _shakeCts?.Cancel();
        view.StopMoving();
        view.PlayDeathSE(); // 敵種別ごとの死亡 SE を再生
        view.PlayDamageVFXAsync().Forget(); // 砕け散る VFX は再生するが、破棄はしない

        // メッシュ崩壊中に当たり判定が残らないよう、配下のコライダーを全て無効化する
        // （ルートの CharacterController と StunCollider など）。
        foreach (var col in GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        PlayDissolveAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>
    /// メッシュの Dissolve（崩壊）演出を再生する。
    /// _DissolveAmount を 0→1 へ _dissolveDuration 秒かけて変化させ、完了後に本体を破棄する。
    /// （将来オブジェクトプール化する際は Destroy を SetActive(false) へ変更を検討）
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async UniTask PlayDissolveAsync(CancellationToken ct)
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

    /// <summary>逃走を開始する。</summary>
    private void BeginFlee()
    {
        _isFleeing = true;
        _state.SetBehavior(EnemyBehavior.Fleeing);
        view.SetDestination(CalcFleePosition());
    }

    /// <summary>逃走を停止する。</summary>
    private void EndFlee()
    {
        _isFleeing = false;
        _state.SetBehavior(EnemyBehavior.Idle);
    }

    /// <summary>
    /// 逃走先を毎フレーム更新する。
    /// 参考実装（EnemyNavigation）と同様、常にプレイヤーと反対方向 FleeDistance 先を目的地にし続けることで、
    /// 目的地が連続的に前方へ伸び続け、NavMeshAgent が減速・経路再計算で脈動せず滑らかに移動する。
    /// </summary>
    private void UpdateFleeDestination()
    {
        view.SetDestination(CalcFleePosition());
    }

    /// <summary>
    /// プレイヤーと反対方向の FleeDistance 先を逃走先として返す（参考実装 EnemyNavigation と同方式）。
    /// 旧実装の多方向 SamplePosition は、採用角度が毎フレーム切り替わって目的地が左右に飛ぶ（ジグザグ）うえ、
    /// 手前の NavMesh 点へスナップして Agent の減速を招くためカクツキの原因となっていた。
    /// 経路の NavMesh へのクランプは NavMeshAgent.SetDestination に任せる。
    /// </summary>
    private Vector3 CalcFleePosition()
    {
        Vector3 awayDir = transform.position - _player.position;
        awayDir.y = 0f;
        awayDir.Normalize();
        return transform.position + awayDir * _state.FleeDistance;
    }

    /// <summary>クローンした EnemyState と進行中のシェイクタスクを破棄する。</summary>
    private void OnDestroy()
    {
        _shakeCts?.Cancel();
        _shakeCts?.Dispose();
        if (_state != null) Destroy(_state);
    }

#if UNITY_EDITOR
    /// <summary>
    /// シーンビューに判定範囲を表示する。
    /// 緑コーン＝FOV感知（各目起点）、シアン球＝近接感知（ボディ中心）、黄球＝目の位置。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (enemyState == null) return;

        // 判定① FOV コーン（緑）― 各目起点
        if (eyeTransforms != null)
        {
            foreach (var eye in eyeTransforms)
            {
                if (eye == null) continue;

                Gizmos.color = new Color(1f, 0.9f, 0f, 1f);
                Gizmos.DrawSphere(eye.position, 0.08f);

                DrawFovCone(eye.position, eye.forward, enemyState.FieldOfViewAngle,
                            enemyState.DetectionRange, new Color(0f, 1f, 0.2f, 0.55f));
            }
        }

        // 判定② 近接感知球（シアン）― ボディ中心
        Gizmos.color = new Color(0f, 0.85f, 1f, 0.12f);
        Gizmos.DrawSphere(transform.position, enemyState.ProximityRange);
        Gizmos.color = new Color(0f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, enemyState.ProximityRange);
    }

    /// <summary>指定角度の扇形コーンを Gizmos で描画する。</summary>
    private static void DrawFovCone(Vector3 origin, Vector3 forward, float angleDeg, float range, Color color)
    {
        Gizmos.color = color;
        const int steps = 20;
        float half = angleDeg * 0.5f;
        Vector3 prev = origin + Quaternion.Euler(0, -half, 0) * forward * range;
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.Lerp(-half, half, i / (float)steps);
            Vector3 next = origin + Quaternion.Euler(0, a, 0) * forward * range;
            Gizmos.DrawLine(origin, next);
            if (i > 0) Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
