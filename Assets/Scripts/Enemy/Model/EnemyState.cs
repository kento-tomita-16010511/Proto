using UnityEngine;
using UniRx;

/// <summary>エネミーの行動状態を表す列挙体。</summary>
public enum EnemyBehavior { Idle, Fleeing, Stunned }

/// <summary>
/// エネミーの検知パラメータと現在の行動状態を保持する ScriptableObject。
/// EnemyPresenter が Awake 時に Instantiate して per-instance のコピーを作成する。
/// </summary>
[CreateAssetMenu(fileName = "EnemyState", menuName = "Game/EnemyState")]
public class EnemyState : ScriptableObject
{
    /// <summary>プレイヤーを検知する最大距離（m）。</summary>
    [SerializeField] private float detectionRange = 10f;

    /// <summary>視野角（度）。この角度の半分がエネミー正面からの許容角度になる。</summary>
    [SerializeField] private float fieldOfViewAngle = 120f;

    /// <summary>この角度以上でエネミーの背後とみなし逃走を停止する（度）。</summary>
    [SerializeField] private float backAngleThreshold = 150f;

    /// <summary>逃走時の移動速度（m/s）。</summary>
    [SerializeField] private float fleeSpeed = 5f;

    /// <summary>プレイヤーから逃走先を計算する距離（m）。</summary>
    [SerializeField] private float fleeDistance = 8f;

    /// <summary>逃走時の加速度（m/s²）。大きいほど素早く最高速に達する。</summary>
    [SerializeField] private float fleeAcceleration = 20f;

    /// <summary>逃走先を再計算する間隔（秒）。毎フレーム再計算を防ぐ。</summary>
    [SerializeField] private float fleeUpdateInterval = 0.3f;

    /// <summary>逃走停止距離の倍率。detectionRange にこの値を掛けた距離まで離れたら停止する。</summary>
    [SerializeField] private float fleeStopMultiplier = 1.2f;

    /// <summary>近接全方位センサーの半径（m）。この距離内はどの角度からでも検知する。</summary>
    [SerializeField] private float proximityRange = 3f;

    /// <summary>逃走先を NavMesh 上に補正する際の許容距離（m）。</summary>
    [SerializeField] private float navMeshSampleDistance = 5f;

    [Header("Stun Settings")]
    /// <summary>Net に被弾した際に行動を停止させる時間（秒）。</summary>
    [SerializeField] private float stunDuration = 3f;

    /// <summary>スタン中の横揺れ幅（m）。</summary>
    [SerializeField] private float stunShakeAmplitude = 0.15f;

    /// <summary>スタン中の横揺れ速度（Hz）。</summary>
    [SerializeField] private float stunShakeFrequency = 8f;

    private readonly ReactiveProperty<EnemyBehavior> _currentBehavior =
        new ReactiveProperty<EnemyBehavior>(EnemyBehavior.Idle);

    /// <summary>検知距離。</summary>
    public float DetectionRange => detectionRange;

    /// <summary>視野角（度）。</summary>
    public float FieldOfViewAngle => fieldOfViewAngle;

    /// <summary>背後判定角度（度）。</summary>
    public float BackAngleThreshold => backAngleThreshold;

    /// <summary>逃走速度。</summary>
    public float FleeSpeed => fleeSpeed;

    /// <summary>逃走距離。</summary>
    public float FleeDistance => fleeDistance;

    /// <summary>逃走加速度。</summary>
    public float FleeAcceleration => fleeAcceleration;

    /// <summary>逃走先の再計算間隔（秒）。</summary>
    public float FleeUpdateInterval => fleeUpdateInterval;

    /// <summary>逃走停止距離の倍率。</summary>
    public float FleeStopMultiplier => fleeStopMultiplier;

    /// <summary>近接全方位センサーの半径（m）。</summary>
    public float ProximityRange => proximityRange;

    /// <summary>NavMesh サンプリング許容距離（m）。</summary>
    public float NavMeshSampleDistance => navMeshSampleDistance;

    /// <summary>スタン持続時間（秒）。</summary>
    public float StunDuration => stunDuration;

    /// <summary>スタン中の横揺れ幅（m）。</summary>
    public float StunShakeAmplitude => stunShakeAmplitude;

    /// <summary>スタン中の横揺れ速度（Hz）。</summary>
    public float StunShakeFrequency => stunShakeFrequency;

    /// <summary>現在の行動状態（読み取り専用の ReactiveProperty）。</summary>
    public IReadOnlyReactiveProperty<EnemyBehavior> CurrentBehavior => _currentBehavior;

    /// <summary>行動状態を設定する。</summary>
    /// <param name="behavior">設定する行動状態。</param>
    public void SetBehavior(EnemyBehavior behavior) => _currentBehavior.Value = behavior;

    /// <summary>ScriptableObject が有効化されたときに状態をリセットする。</summary>
    private void OnEnable() => _currentBehavior.Value = EnemyBehavior.Idle;
}
