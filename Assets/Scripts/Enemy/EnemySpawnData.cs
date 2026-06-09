using UnityEngine;

/// <summary>
/// 敵のスポーンに関する設定データを保持するクラス。
/// </summary>
[System.Serializable]
public class EnemySpawnData
{
    /// <summary>インスペクター上で識別するための名前</summary>
    public string enemyName;

    /// <summary>生成する敵のプレハブ</summary>
    public GameObject prefab;

    /// <summary>出現確率の重み。数値が大きいほど抽選されやすくなります</summary>
    [Tooltip("出現確率の重み。大きいほど出やすい")]
    public int weight = 1;
}