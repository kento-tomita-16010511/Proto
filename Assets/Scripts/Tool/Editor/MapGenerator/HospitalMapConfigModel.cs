using UnityEngine;

/// <summary>
/// 病院マップ生成の設定を保持するModelクラス。
/// グリッド寸法・乱数シード・生成に使うPrefab参照のみを持ち、ロジックは持たない。
/// Prefab参照を本番アセットに差し替えることで見た目を変更できる。
/// </summary>
[CreateAssetMenu(fileName = "HospitalMapConfig", menuName = "Model/HospitalMapConfigModel")]
public class HospitalMapConfigModel : ScriptableObject
{
    /// <summary>1グリッドセルの一辺の長さ(m)。設計書準拠で3m</summary>
    [SerializeField] private float _cellSize = 3f;

    /// <summary>壁の高さ(m)</summary>
    [SerializeField] private float _wallHeight = 3f;

    /// <summary>ドア位置などディテールの揺らぎに使う乱数シード。同じ値なら同じマップが生成される</summary>
    [SerializeField] private int _randomSeed = 12345;

    /// <summary>床タイルPrefab(1セル分。原点=床面中央)</summary>
    [SerializeField] private GameObject _floorPrefab;

    /// <summary>壁Prefab(1セル分。原点=下端中央)</summary>
    [SerializeField] private GameObject _wallPrefab;

    /// <summary>ドア枠Prefab(1セル分の開口部。原点=下端中央)</summary>
    [SerializeField] private GameObject _doorPrefab;

    /// <summary>天井Prefab(1セル分。原点=天井面中央)</summary>
    [SerializeField] private GameObject _ceilingPrefab;

    /// <summary>天井を生成するかどうか(エディタ上の視認性のため既定はOFF)</summary>
    [SerializeField] private bool _generateCeiling = false;

    /// <summary>エリア名ラベルを生成するかどうか(レイアウト確認用)</summary>
    [SerializeField] private bool _generateAreaLabels = true;

    /// <summary>1グリッドセルの一辺の長さ(m)</summary>
    public float CellSize => _cellSize;

    /// <summary>壁の高さ(m)</summary>
    public float WallHeight => _wallHeight;

    /// <summary>ディテールの揺らぎに使う乱数シード</summary>
    public int RandomSeed => _randomSeed;

    /// <summary>床タイルPrefab</summary>
    public GameObject FloorPrefab => _floorPrefab;

    /// <summary>壁Prefab</summary>
    public GameObject WallPrefab => _wallPrefab;

    /// <summary>ドア枠Prefab</summary>
    public GameObject DoorPrefab => _doorPrefab;

    /// <summary>天井Prefab</summary>
    public GameObject CeilingPrefab => _ceilingPrefab;

    /// <summary>天井を生成するかどうか</summary>
    public bool GenerateCeiling => _generateCeiling;

    /// <summary>エリア名ラベルを生成するかどうか</summary>
    public bool GenerateAreaLabels => _generateAreaLabels;
}
