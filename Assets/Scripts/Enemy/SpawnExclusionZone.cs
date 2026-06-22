using UnityEngine;

/// <summary>
/// 敵を湧かせたくない範囲（除外エリア）を表す設定データ。
/// center を基準に radius 半径の円（XZ 平面）内を湧き禁止とする。
/// center が未設定の場合は worldCenter を使う。
/// </summary>
[System.Serializable]
public class SpawnExclusionZone
{
    /// <summary>インスペクター上で識別するための名前</summary>
    public string label = "ExclusionZone";

    /// <summary>除外円の中心となる Transform（未設定なら worldCenter を使用）</summary>
    public Transform center;

    /// <summary>center 未設定時に使うワールド座標の中心</summary>
    public Vector3 worldCenter;

    /// <summary>除外する半径</summary>
    public float radius = 3f;

    /// <summary>この除外ゾーンの中心ワールド座標を取得する。</summary>
    public Vector3 GetCenter()
    {
        return (center != null) ? center.position : worldCenter;
    }

    /// <summary>指定した座標がこの除外ゾーン内（XZ 平面）にあるかを判定する。</summary>
    public bool Contains(Vector3 position)
    {
        Vector3 c = GetCenter();
        float dx = position.x - c.x;
        float dz = position.z - c.z;
        return (dx * dx + dz * dz) <= (radius * radius);
    }
}
