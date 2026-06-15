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
    /// <summary>CharacterController ベースの移動コンポーネント。NavMeshAgent がない場合に使用する。</summary>
    [SerializeField] private CreatureMover creatureMover;

    /// <summary>移動を担う NavMeshAgent。未設定時は Awake で自動取得する。</summary>

    private NavMeshAgent _agent;
    /// <summary>コンポーネント参照を確立する。</summary>
    private void Awake()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        if (creatureMover == null) creatureMover = GetComponent<CreatureMover>();
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
            return;
        }

        if (creatureMover != null)
        {
            // destination 方向を target として渡し、前進（axis.y=1）＋走りで移動させる
            Debug.Log($"[EnemyView:{name}] CreatureMover.SetInput → dest={destination:F2}");
            creatureMover.SetInput(Vector2.up, destination, isRun: true, isJump: false);
        }
        else
        {
            Debug.LogWarning($"[EnemyView:{name}] agent も creatureMover も null。移動不可。");
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

        if (creatureMover != null)
        {
            creatureMover.SetInput(Vector2.zero, transform.position, isRun: false, isJump: false);
        }
    }
}
