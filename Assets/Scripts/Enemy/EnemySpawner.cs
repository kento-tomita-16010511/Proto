using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;

public class EnemySpawner : MonoBehaviour, IFreezable
{
    [Header("Spawn Settings")]
    [Tooltip("出現させる敵のリスト")]
    [SerializeField] private List<EnemySpawnData> enemyPool = new List<EnemySpawnData>();

    [Space]
    [Tooltip("敵をスポーンさせる間隔（秒）")]
    [SerializeField] private float spawnInterval = 3f;

    [Tooltip("同時に存在できる敵の最大数")]
    [SerializeField] private int maxEnemies = 5;

    [Tooltip("敵がスポーンする位置のTransform")]
    [SerializeField] private Transform spawnPoint;

    [Header("湧き範囲の半径")]
    [SerializeField] private float spawnRadius = 5f;

    [Header("NavMesh Settings")]
    /// <summary>NavMesh 上の点を探す際の許容距離。候補点からこの距離内に NavMesh があれば採用</summary>
    [SerializeField] private float navMeshSampleDistance = 2f;

    /// <summary>NavMesh 上の点が見つかるまでの最大再抽選回数</summary>
    [SerializeField] private int maxSampleAttempts = 10;

    /// <summary>判定対象とする NavMesh エリアマスク（デフォルトは全エリア）</summary>
    [SerializeField] private int areaMask = NavMesh.AllAreas;

    private List<GameObject> _activeEnemies = new List<GameObject>();
    private CancellationTokenSource _cancellationTokenSource;
    private IEnemySelector _selector;
    private bool _isFrozen;

    void Awake()
    {
        _selector = new WeightedEnemySelector();
        if (spawnPoint == null) spawnPoint = transform;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// FreezeAll() で無効化された後に Start() が呼ばれるため、
    /// UnfreezeAll までスポーンが始まらない。
    /// </summary>
    void Start()
    {
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
                if (_isFrozen)
                {
                    // 停止中は短いスパンで待機してループを維持する
                    await UniTask.Delay(100, cancellationToken: cancellationToken);
                    continue;
                }
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

    /// <summary>
    /// 敵を1体スポーンさせる。スポーン位置は指定エリア内かつ NavMesh 上の点に限定される。
    /// NavMesh 上の点が見つからない場合はスポーンをスキップする。
    /// </summary>
    private void SpawnEnemy()
    {
        if (enemyPool == null || enemyPool.Count == 0) return;

        GameObject prefabToSpawn = _selector.Select(enemyPool);
        if (prefabToSpawn == null) return;

        if (TryGetNavMeshSpawnPosition(out Vector3 spawnPos))
        {
            GameObject newEnemy = Instantiate(prefabToSpawn, spawnPos, spawnPoint.rotation);
            newEnemy.transform.SetParent(this.transform, worldPositionStays: true);
            _activeEnemies.Add(newEnemy);
        }
        else
        {
            // 有効な位置が見つからなかった場合は警告のみ（次のスポーン間隔で再試行される）
            Debug.LogWarning("EnemySpawner: NavMesh 上の有効なスポーン位置が見つかりませんでした。");
        }
    }

    /// <summary>
    /// spawnPoint 中心・spawnRadius 半径の円内で、NavMesh 上の有効な位置を探す。
    /// </summary>
    /// <param name="result">見つかった NavMesh 上の座標</param>
    /// <returns>有効な位置が見つかれば true</returns>
    private bool TryGetNavMeshSpawnPosition(out Vector3 result)
    {
        // spawnPoint 直下の NavMesh Y を基準にする（spawnPoint が地形より高い場合の垂直差を吸収）
        float baseY = spawnPoint.position.y;
        if (NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit centerHit, 100f, areaMask))
        {
            baseY = centerHit.position.y;
        }

        for (int i = 0; i < maxSampleAttempts; i++)
        {
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = new Vector3(
                spawnPoint.position.x + randomOffset.x,
                baseY,
                spawnPoint.position.z + randomOffset.y
            );

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, areaMask))
            {
                result = hit.position;
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }

    public void Freeze()
    {
        _isFrozen = true;
    }

    public void Unfreeze()
    {
        _isFrozen = false;
    }

}
