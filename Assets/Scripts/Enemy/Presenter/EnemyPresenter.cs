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
public class EnemyPresenter : MonoBehaviour
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

    private EnemyState _state;
    private Transform _player;

    /// <summary>前回逃走先を計算した時刻。fleeUpdateInterval の間隔制御に使う。</summary>
    private float _lastFleeCalcTime;

    /// <summary>現在逃走中かどうか。ヒステリシス判定に使う。</summary>
    private bool _isFleeing;

    /// <summary>スタン（行動停止）中かどうか。</summary>
    private bool _isStunned;

    /// <summary>スタンが解除される時刻（Time.time 基準）。</summary>
    private float _stunEndTime;

    /// <summary>EnemyState を per-instance にクローンし、View を取得する。</summary>
    private void Awake()
    {
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
        if (_player == null) return;

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
    /// 指定時間だけ行動を停止する（Net による足止め）。
    /// 逃走状態を解除し移動を止め、Stunned 状態へ移行する。
    /// </summary>
    /// <param name="duration">停止時間（秒）。</param>
    public void Stun(float duration)
    {
        _isStunned = true;
        _stunEndTime = Time.time + duration;
        _isFleeing = false;
        _state.SetBehavior(EnemyBehavior.Stunned);
        view.StopMoving();
    }

    /// <summary>逃走を開始する。</summary>
    private void BeginFlee()
    {
        _isFleeing = true;
        _state.SetBehavior(EnemyBehavior.Fleeing);
        Vector3 fleePos = CalcFleePosition();
        Debug.Log($"[EnemyPresenter:{name}] BeginFlee → fleePos={fleePos:F2} (自分={transform.position:F2})");
        view.SetDestination(fleePos);
        _lastFleeCalcTime = Time.time;
    }

    /// <summary>逃走を停止する。</summary>
    private void EndFlee()
    {
        _isFleeing = false;
        _state.SetBehavior(EnemyBehavior.Idle);
    }

    /// <summary>逃走先を fleeUpdateInterval の間隔で再計算する。</summary>
    private void UpdateFleeDestination()
    {
        if (Time.time - _lastFleeCalcTime < _state.FleeUpdateInterval) return;
        view.SetDestination(CalcFleePosition());
        _lastFleeCalcTime = Time.time;
    }

    /// <summary>プレイヤーから遠ざかる方向に逃走先を計算し、NavMesh 上の最近点を返す。
    /// 真後ろが壁の場合は ±45°・±90° の方向も順に試みる。</summary>
    private Vector3 CalcFleePosition()
    {
        Vector3 awayDir = (transform.position - _player.position).normalized;
        float[] angles = { 0f, 45f, -45f, 90f, -90f };
        foreach (float a in angles)
        {
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * awayDir;
            Vector3 target = transform.position + dir * _state.FleeDistance;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, _state.NavMeshSampleDistance, NavMesh.AllAreas))
                return hit.position;
        }
        return transform.position + awayDir * _state.FleeDistance;
    }

    /// <summary>クローンした EnemyState インスタンスを破棄する。</summary>
    private void OnDestroy()
    {
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
