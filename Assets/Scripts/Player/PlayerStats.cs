using UnityEngine;

/// <summary>
/// プレイヤーの調整可能なステータスを保持する ScriptableObject。
/// 現状は Net（蜘蛛の巣）による敵の行動停止時間を管理する。
/// </summary>
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Game/PlayerStats")]
public class PlayerStats : ScriptableObject
{
    /// <summary>Net に当たった敵を停止させる時間（秒）。</summary>
    [SerializeField] private float webStunDuration = 3f;

    /// <summary>Net による敵の停止時間（秒）。</summary>
    public float WebStunDuration => webStunDuration;
}
