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

    [Header("湧かない範囲（除外設定）")]
    [Tooltip("中心からこの半径より内側には湧かせない（0 で無効）")]
    [SerializeField] private float exclusionInnerRadius = 0f;

    [Tooltip("指定した円の内側には湧かせない（複数指定可）")]
    [SerializeField] private List<SpawnExclusionZone> exclusionZones = new List<SpawnExclusionZone>();

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

    /// <summary>
    /// スポーンを停止し、現在生存している敵をすべて削除する。
    /// MainScene を離れてリザルトへ遷移する際に呼ばれ、敵が背景に残らないようにする。
    /// </summary>
    public void StopSpawning()
    {
        _cancellationTokenSource?.Cancel();
        ClearAllEnemies();
    }

    /// <summary>生成済みの敵をすべて破棄し、追跡リストを空にする。</summary>
    private void ClearAllEnemies()
    {
        for (int i = 0; i < _activeEnemies.Count; i++)
        {
            if (_activeEnemies[i] != null) Destroy(_activeEnemies[i]);
        }
        _activeEnemies.Clear();
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

                // 撃破済み（Destroy 済み = null）の敵を追跡リストから除去する。
                _activeEnemies.RemoveAll(enemy => enemy == null);

                // 常に maxEnemies 体になるよう、不足分だけ補充スポーンする。
                while (_activeEnemies.Count < maxEnemies)
                {
                    int before = _activeEnemies.Count;
                    SpawnEnemy();
                    // スポーンに失敗（NavMesh 上の有効位置なし）した場合は
                    // 無限ループを避けるため、このフレームでの補充を打ち切る。
                    if (_activeEnemies.Count == before) break;
                }

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

        // 湧かない範囲（内側除外半径）を赤で表示
        if (exclusionInnerRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawSphere(center, exclusionInnerRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, exclusionInnerRadius);
        }

        // 個別の除外ゾーンを赤で表示
        if (exclusionZones != null)
        {
            foreach (var zone in exclusionZones)
            {
                if (zone == null || zone.radius <= 0f) continue;
                Vector3 zc = zone.GetCenter();
                Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                Gizmos.DrawSphere(zc, zone.radius);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(zc, zone.radius);
            }
        }
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
                // 湧かない範囲に入っている場合は採用せず再抽選する
                if (IsExcluded(hit.position)) continue;

                result = hit.position;
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }

    /// <summary>
    /// 指定した位置が湧き禁止範囲（内側半径または除外ゾーン）に含まれるかを判定する。
    /// </summary>
    private bool IsExcluded(Vector3 position)
    {
        // 中心からの内側除外半径チェック（XZ 平面）
        if (exclusionInnerRadius > 0f)
        {
            Vector3 center = spawnPoint.position;
            float dx = position.x - center.x;
            float dz = position.z - center.z;
            if (dx * dx + dz * dz <= exclusionInnerRadius * exclusionInnerRadius) return true;
        }

        // 個別の除外ゾーンチェック
        if (exclusionZones != null)
        {
            for (int i = 0; i < exclusionZones.Count; i++)
            {
                var zone = exclusionZones[i];
                if (zone != null && zone.radius > 0f && zone.Contains(position)) return true;
            }
        }

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
