using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 手榴弾の「破片飛散」エフェクト。
/// 閃光・爆煙・爆風は含まず、金属片が四方へ飛び散る様子のみを ParticleSystem（Shuriken）で再現する。
/// Explode(Vector3) を呼ぶだけで指定座標に破片を一括発生させ、全パーティクル消滅後に GameObject を自動破棄する。
/// GrenadeFragments.prefab のルートにアタッチして使用する。
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class GrenadeFragmentEffect : MonoBehaviour
{
    [Header("破片の量")]
    [Tooltip("一度の爆発で飛散する破片の数（Burst で 0 秒に一括発生）。")]
    [SerializeField, Range(1, 200)] private int fragmentCount = 30;

    [Header("初速（m/s）")]
    [Tooltip("破片の初速の最小値。")]
    [SerializeField] private float minSpeed = 8f;
    [Tooltip("破片の初速の最大値。")]
    [SerializeField] private float maxSpeed = 20f;

    [Header("拡散範囲")]
    [Tooltip("発生源となる球（Sphere）の半径（m）。爆心点からこの範囲に散らばって発生する。")]
    [SerializeField] private float spreadRadius = 0.15f;

    [Header("寿命（秒）")]
    [Tooltip("破片の寿命の最小値。")]
    [SerializeField] private float minLifetime = 2f;
    [Tooltip("破片の寿命の最大値。")]
    [SerializeField] private float maxLifetime = 4f;

    [Header("再生時の挙動")]
    [Tooltip("Explode 呼び出し時に自動で再生するか。false の場合は外部から Play() する。")]
    [SerializeField] private bool playOnExplode = true;
    [Tooltip("再生後、全パーティクルの消滅を検知して GameObject を自動破棄するか（メモリリーク防止）。")]
    [SerializeField] private bool autoDestroy = true;

    [Header("破片メッシュのバリエーション")]
    [Tooltip("起動時にいびつな多面体メッシュを動的生成し、Renderer のメッシュリストへ追加する（Cube と混在させて不規則感を出す）。")]
    [SerializeField] private bool generateProceduralMeshes = true;
    [Tooltip("動的生成する破片メッシュのバリエーション数。")]
    [SerializeField, Range(0, 8)] private int proceduralMeshVariants = 3;
    [Tooltip("Cube 形状からの頂点ジッター量（大きいほど歪む）。")]
    [SerializeField, Range(0f, 0.5f)] private float meshJitter = 0.28f;

    private ParticleSystem _particle;
    private ParticleSystemRenderer _renderer;
    private readonly List<Mesh> _generatedMeshes = new List<Mesh>();

    private void Awake()
    {
        _particle = GetComponent<ParticleSystem>();
        _renderer = GetComponent<ParticleSystemRenderer>();
        ApplyInspectorSettings();
        if (generateProceduralMeshes && proceduralMeshVariants > 0)
        {
            BuildProceduralMeshes();
        }
    }

    /// <summary>
    /// 指定座標で破片を爆発させる。プレハブをインスタンス化したあと、この 1 メソッドを呼ぶだけでよい。
    /// </summary>
    /// <param name="position">爆心点（ワールド座標）。</param>
    public void Explode(Vector3 position)
    {
        transform.position = position;
        if (_particle == null) _particle = GetComponent<ParticleSystem>();

        // インスペクターで調整した値を発生直前に反映する。
        ApplyInspectorSettings();

        if (playOnExplode)
        {
            _particle.Clear(true);
            _particle.Play(true);
        }

        if (autoDestroy)
        {
            StartCoroutine(DestroyWhenFinished());
        }
    }

    /// <summary>
    /// プレハブから 1 ショットだけ生成して即爆発させる簡易ヘルパー。
    /// 例: <c>GrenadeFragmentEffect.Spawn(_fragmentPrefab, hitPoint);</c>
    /// </summary>
    public static GrenadeFragmentEffect Spawn(GrenadeFragmentEffect prefab, Vector3 position)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[GrenadeFragmentEffect] prefab が null のため生成をスキップしました。");
            return null;
        }

        var instance = Instantiate(prefab);
        instance.Explode(position);
        return instance;
    }

    /// <summary>インスペクターで調整した値（数・初速・拡散範囲・寿命）を ParticleSystem 各モジュールへ反映する。</summary>
    private void ApplyInspectorSettings()
    {
        var main = _particle.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);

        var shape = _particle.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.0001f, spreadRadius);

        // Burst を fragmentCount に合わせて再設定（時刻 0 秒で一括発生）。
        var emission = _particle.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)fragmentCount) });
    }

    /// <summary>
    /// 全パーティクル（子含む）が消滅するまで待ってから自身を破棄する。
    /// </summary>
    private IEnumerator DestroyWhenFinished()
    {
        // 再生開始を 1 フレーム待ってから監視を始める（即時 IsAlive=false での誤破棄を防ぐ）。
        yield return null;
        while (_particle != null && _particle.IsAlive(true))
        {
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>
    /// いびつな多面体（歪んだ Cube）メッシュを生成し、Renderer の既存メッシュリストへ追加する。
    /// 各面を独立した頂点で構成することでハードエッジ（金属片らしいギザギザ感）を出す。
    /// </summary>
    private void BuildProceduralMeshes()
    {
        var meshes = new List<Mesh>();

        // プレハブに既に割り当て済みのメッシュ（Unity 標準 Cube など）を保持する。
        int existing = _renderer.meshCount;
        if (existing > 0)
        {
            var current = new Mesh[existing];
            _renderer.GetMeshes(current);
            meshes.AddRange(current);
        }

        for (int i = 0; i < proceduralMeshVariants; i++)
        {
            var shard = CreateShardMesh(i);
            _generatedMeshes.Add(shard);
            meshes.Add(shard);
        }

        _renderer.SetMeshes(meshes.ToArray(), meshes.Count);
    }

    /// <summary>
    /// 中心 ±0.5 の立方体の 8 頂点をランダムにジッターし、6 面それぞれを独立頂点で張った
    /// 歪んだ多面体メッシュを生成する。ハードエッジになるよう面ごとに頂点を複製する。
    /// </summary>
    private Mesh CreateShardMesh(int seed)
    {
        var rng = new System.Random(unchecked(GetInstanceID() * 73856093 + seed * 19349663));

        // 立方体 8 頂点（±0.5）をジッター。
        var corners = new Vector3[8];
        for (int c = 0; c < 8; c++)
        {
            float bx = ((c & 1) == 0) ? -0.5f : 0.5f;
            float by = ((c & 2) == 0) ? -0.5f : 0.5f;
            float bz = ((c & 4) == 0) ? -0.5f : 0.5f;
            corners[c] = new Vector3(
                bx + (float)(rng.NextDouble() - 0.5) * 2f * meshJitter,
                by + (float)(rng.NextDouble() - 0.5) * 2f * meshJitter,
                bz + (float)(rng.NextDouble() - 0.5) * 2f * meshJitter);
        }

        // 各面を構成する 4 つの角インデックス（CCW）。ビット: x=1, y=2, z=4。
        int[][] faces =
        {
            new[] { 0, 2, 6, 4 }, // -X
            new[] { 1, 5, 7, 3 }, // +X
            new[] { 0, 4, 5, 1 }, // -Y
            new[] { 2, 3, 7, 6 }, // +Y
            new[] { 0, 1, 3, 2 }, // -Z
            new[] { 4, 6, 7, 5 }, // +Z
        };

        var vertices = new List<Vector3>(24);
        var triangles = new List<int>(36);
        foreach (var f in faces)
        {
            int baseIndex = vertices.Count;
            vertices.Add(corners[f[0]]);
            vertices.Add(corners[f[1]]);
            vertices.Add(corners[f[2]]);
            vertices.Add(corners[f[3]]);
            triangles.Add(baseIndex + 0);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 0);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }

        var mesh = new Mesh { name = "GrenadeShard_" + seed };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); // 面ごとに独立頂点なのでハードエッジになる
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        // 動的生成したメッシュを破棄してリークを防ぐ。
        foreach (var m in _generatedMeshes)
        {
            if (m != null) Destroy(m);
        }
        _generatedMeshes.Clear();
    }
}
