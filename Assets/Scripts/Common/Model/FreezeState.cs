using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FreezeAll / UnfreezeAll で一時停止した Behaviour と Rigidbody の状態を保持する ScriptableObject。
/// プレイ開始ごとに OnEnable でクリアされる。
/// </summary>
[CreateAssetMenu(fileName = "FreezeState", menuName = "Game/FreezeState")]
public class FreezeState : ScriptableObject
{
    /// <summary>停止した Behaviour と元の enabled 状態を記録する構造体。</summary>
    public struct Entry
    {
        /// <summary>対象の Behaviour。</summary>
        public Behaviour component;
        /// <summary>停止前の enabled 値。</summary>
        public bool wasEnabled;
    }

    /// <summary>停止した Rigidbody と元の isKinematic 状態を記録する構造体。</summary>
    public struct RbEntry
    {
        /// <summary>対象の Rigidbody。</summary>
        public Rigidbody rb;
        /// <summary>停止前の isKinematic 値。</summary>
        public bool wasKinematic;
    }

    /// <summary>停止中の Behaviour エントリ一覧。</summary>
    public List<Entry> Behaviours { get; } = new List<Entry>();

    /// <summary>停止中の Rigidbody エントリ一覧。</summary>
    public List<RbEntry> Rigidbodies { get; } = new List<RbEntry>();

    /// <summary>全エントリをクリアする。</summary>
    public void Clear()
    {
        Behaviours.Clear();
        Rigidbodies.Clear();
    }

    /// <summary>プレイ開始時にリストをリセットする。</summary>
    private void OnEnable() => Clear();
}
