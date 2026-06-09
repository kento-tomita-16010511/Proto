using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 重みに基づいて敵をランダムに抽選するセレクタークラス。
/// </summary>
public class WeightedEnemySelector : IEnemySelector
{
    /// <summary>
    /// 重みに基づいた抽選を行い、生成すべき敵のプレハブを返します。
    /// </summary>
    public GameObject Select(IEnumerable<EnemySpawnData> pool)
    {
        // 有効なデータ（nullでなく、プレハブが設定されているもの）のみを対象にする
        var validPool = pool?.Where(e => e != null && e.prefab != null).ToList();

        if (validPool == null || !validPool.Any()) return null;

        int totalWeight = validPool.Sum(e => e.weight);
        if (totalWeight <= 0) return null;

        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var enemy in validPool)
        {
            currentWeight += enemy.weight;
            if (randomValue < currentWeight)
                return enemy.prefab;
        }
        return validPool.First().prefab;
    }
}