using UnityEngine;
using UnityEngine.AI;
using UniRx;

/// <summary>
/// エネミーの視野角・距離判定と逃走ロジックを担う Presenter クラス。
/// 毎フレーム Observable.EveryUpdate() で判定し、EnemyState へ状態を書き込む。
/// EnemyState の変化を Subscribe して EnemyView に移動指示を出す。
/// </summary>
public class EnemyPresenter : MonoBehaviour
{
    /// <summary>検知パラメータのテンプレート（per-instance にクローンされる）。</summary>
    [SerializeField] private EnemyState enemyStateTemplate;

    /// <summary>移動指示を受け取る View。省略時は自動取得する。</summary>
    [SerializeField] private EnemyView view;

    private EnemyState _state;
    private Transform _player;

    /// <summary>前回逃走先を計算した時刻。fleeUpdateInterval の間隔制御に使う。</summary>
    private float _lastFleeCalcTime;

    /// <summary>現在逃走中かどうか。ヒステリシス判定に使う。</summary>
    private bool _isFleeing;

    /// <summary>EnemyState を per-instance にクローンし、View を取得する。</summary>
    private void Awake()
    {
        _state = ScriptableObject.Instantiate(enemyStateTemplate);
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

        // Idle 遷移時に移動を停止する
        _state.CurrentBehavior
            .Where(b => b == EnemyBehavior.Idle)
            .Subscribe(_ => view.StopMoving())
            .AddTo(this);

        // 毎フレーム視野角・距離を判定する
        Observable.EveryUpdate()
            .Subscribe(_ => UpdateDetection())
            .AddTo(this);
    }

    /// <summary>視野角・距離・ヒステリシスに基づいて逃走状態を更新する。</summary>
    private float _debugLogTimer;

    private void UpdateDetection()
    {
        if (_player == null) return;

        float distance = Vector3.Distance(transform.position, _player.position);
        Vector3 dirToPlayer = (_player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);

        bool isBehind = angle > _state.BackAngleThreshold;
        bool inFov = !isBehind && angle < _state.FieldOfViewAngle * 0.5f;

        float startDist = _state.DetectionRange;
        float stopDist = _state.DetectionRange * _state.FleeStopMultiplier;

        // 1秒ごとに最もプレイヤーに近い1体だけ診断ログを出す
        _debugLogTimer -= Time.deltaTime;
        if (_debugLogTimer <= 0f)
        {
            _debugLogTimer = 1f;
            Debug.Log($"[EnemyPresenter:{name}] dist={distance:F1} angle={angle:F0}° inFov={inFov} isFleeing={_isFleeing} (range={startDist} fov={_state.FieldOfViewAngle * 0.5f:F0}°)");
        }

        if (!_isFleeing)
        {
            // 逃走開始：視野角内かつ検知距離内のときのみ
            if (inFov && distance < startDist) BeginFlee();
        }
        else
        {
            // 逃走停止：距離だけで判断する（背後判定は含めない）
            // 理由：逃げ方向へ向き直る過程でプレイヤーが「背後」に入り EndFlee が即呼ばれるのを防ぐ
            if (distance > stopDist) EndFlee();
            else UpdateFleeDestination();
        }
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
    /// <summary>シーンビューに視野角コーン・検知距離・背後判定ラインを表示する。</summary>
    private void OnDrawGizmosSelected()
    {
        if (enemyStateTemplate == null) return;

        Vector3 pos = transform.position;

        // 検知距離 (黄色)
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.12f);
        Gizmos.DrawSphere(pos, enemyStateTemplate.DetectionRange);
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.9f);
        Gizmos.DrawWireSphere(pos, enemyStateTemplate.DetectionRange);

        // 視野角コーン (緑)
        DrawFovCone(pos, transform.forward, enemyStateTemplate.FieldOfViewAngle,
                    enemyStateTemplate.DetectionRange, new Color(0f, 1f, 0f, 0.6f));

        // 背後判定コーン (赤 / 半径を短く)
        DrawFovCone(pos, transform.forward, enemyStateTemplate.BackAngleThreshold,
                    enemyStateTemplate.DetectionRange * 0.5f, new Color(1f, 0.2f, 0.2f, 0.45f));
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
