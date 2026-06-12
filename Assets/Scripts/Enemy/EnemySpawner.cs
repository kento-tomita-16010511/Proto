using UnityEngine;
using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Linq;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("出現させる敵のリスト")]
    public List<EnemySpawnData> enemyPool = new List<EnemySpawnData>();

    [Space]
    [Tooltip("敵をスポーンさせる間隔（秒）")]
    public float spawnInterval = 3f;

    [Tooltip("同時に存在できる敵の最大数")]
    public int maxEnemies = 5;

    [Tooltip("敵がスポーンする位置のTransform")]
    public Transform spawnPoint;

    [Header("湧き範囲の半径")]
    public float spawnRadius = 5f;

    private List<GameObject> _activeEnemies = new List<GameObject>();
    private CancellationTokenSource _cancellationTokenSource;
    private IEnemySelector _selector;

    void Awake()
    {
        _selector = new WeightedEnemySelector();
        if (spawnPoint == null) spawnPoint = transform;
        _cancellationTokenSource = new CancellationTokenSource();
        StartSpawning(_cancellationTokenSource.Token).Forget();
    }

    void OnDestroy()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }

    public void StopSpawning()
    {
        _cancellationTokenSource?.Cancel();
    }

    private async UniTaskVoid StartSpawning(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _activeEnemies.RemoveAll(enemy => enemy == null);
                if (_activeEnemies.Count < maxEnemies) SpawnEnemy();
                int delayMs = Mathf.RoundToInt(Mathf.Max(0, spawnInterval) * 1000);
                await UniTask.Delay(delayMs, cancellationToken: cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogError($"EnemySpawner loop error: {e}"); }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = (spawnPoint != null) ? spawnPoint.position : transform.position;
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawSphere(center, spawnRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, spawnRadius);
    }

    private void SpawnEnemy()
    {
        if (enemyPool == null || enemyPool.Count == 0) return;
        GameObject prefabToSpawn = _selector.Select(enemyPool);
        if (prefabToSpawn == null) return;
        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = spawnPoint.position + new Vector3(randomOffset.x, 0f, randomOffset.y);
        GameObject newEnemy = Instantiate(prefabToSpawn, spawnPos, spawnPoint.rotation);
        _activeEnemies.Add(newEnemy);
    }
}
