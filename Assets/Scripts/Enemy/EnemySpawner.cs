using UnityEngine;
using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Linq;

/// <summary>
/// 敵のスポーンタイミングと個体数を管理するクラス。
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    /// <summary>スポーン候補となる敵のデータリスト</summary>
    [Tooltip("出現させる敵のリスト")]
    public List<EnemySpawnData> enemyPool = new List<EnemySpawnData>();

    [Space]
    /// <summary>敵をスポーンさせる間隔（秒単位）</summary>
    [Tooltip("敵をスポーンさせる間隔（秒）")]
    public float spawnInterval = 3f;

    /// <summary>シーン内に同時に存在できる敵の最大数</summary>
    [Tooltip("同時に存在できる敵の最大数")]
    public int maxEnemies = 5;

    /// <summary>敵が出現する座標の起点となるTransform</summary>
    [Tooltip("敵がスポーンする位置のTransform (未設定時はこのGameObjectの位置を使用)")]
    public Transform spawnPoint;

    [Header("湧き範囲の半径")]
    public float spawnRadius = 5f;

    /// <summary>現在生存している敵を管理するリスト</summary>
    private List<GameObject> _activeEnemies = new List<GameObject>();
    /// <summary>非同期処理（UniTask）をキャンセルするためのソース</summary>
    private CancellationTokenSource _cancellationTokenSource;
    /// <summary>敵を抽選するアルゴリズムのインターフェース</summary>
    private IEnemySelector _selector;

    void Awake()
    {
        // 重みに基づいた抽選ロジックを注入（DIP）
        _selector = new WeightedEnemySelector();

        // spawnPointが設定されていない場合、このGameObject自身のTransformを使用
        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }

        // UniTaskのキャンセル処理のためにCancellationTokenSourceを初期化
        _cancellationTokenSource = new CancellationTokenSource();
        // スポーン処理を開始
        StartSpawning(_cancellationTokenSource.Token).Forget();
    }

    void OnDestroy()
    {
        // GameObjectが破棄される際に、実行中のUniTaskをキャンセル
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }

    /// <summary>
    /// 非同期で敵のスポーン処理を管理します。
    /// </summary>
    private async UniTaskVoid StartSpawning(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // 既に破壊された敵（nullになったエントリ）をリストから削除
                _activeEnemies.RemoveAll(enemy => enemy == null);

                if (_activeEnemies.Count < maxEnemies)
                {
                    SpawnEnemy();
                }

                // 指定された間隔で待機。負の値にならないよう Clamp する
                int delayMs = Mathf.RoundToInt(Mathf.Max(0, spawnInterval) * 1000);
                await UniTask.Delay(delayMs, cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセル時は正常終了として扱う
        }
        catch (Exception e)
        {
            Debug.LogError($"EnemySpawner loop error: {e}");
        }
    }

    // オブジェクトが選択されている時だけSceneビューに円（球）を描画
    private void OnDrawGizmosSelected()
    {
        // spawnPointが未設定の場合は自身の位置を基準にする
        Vector3 center = (spawnPoint != null) ? spawnPoint.position : transform.position;

        // 範囲を分かりやすくするため、薄い緑の球とワイヤーフレームを表示
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawSphere(center, spawnRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, spawnRadius);
    }

    private void SpawnEnemy()
    {
        if (enemyPool == null || enemyPool.Count == 0)
            return;

        // 抽選ロジックを外部（Selector）に委譲 (SRP)
        GameObject prefabToSpawn = _selector.Select(enemyPool);
        if (prefabToSpawn == null) return;

        // 半径に基づいたランダムな座標を計算（XZ平面上）
        // UnityEngine.Random.insideUnitCircle は半径1の円内のランダムな点を返す
        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = spawnPoint.position + new Vector3(randomOffset.x, 0f, randomOffset.y);

        // 生成
        GameObject newEnemy = Instantiate(prefabToSpawn, spawnPos, spawnPoint.rotation);
        _activeEnemies.Add(newEnemy);
    }
}