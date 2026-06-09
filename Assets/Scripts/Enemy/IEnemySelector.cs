using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 敵の抽選ロジックを定義するインターフェース。
/// </summary>
public interface IEnemySelector
{
    /// <summary>
    /// 指定されたリストの中から生成する敵のプレハブを選択します。
    /// </summary>
    /// <param name="pool">スポーン候補のリスト</param>
    /// <returns>選択された敵のプレハブ。候補がない場合は null</returns>
    GameObject Select(IEnumerable<EnemySpawnData> pool);
}