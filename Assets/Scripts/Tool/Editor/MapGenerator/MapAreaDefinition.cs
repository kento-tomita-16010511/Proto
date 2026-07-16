using UnityEngine;

/// <summary>
/// マップ上のエリア1件(廊下・ホール・部屋)の定義を保持するデータクラス。
/// 座標系は Docs/MapDesign.md 準拠(1グリッド=3m、RectIntで矩形指定)。
/// </summary>
public class MapAreaDefinition
{
    /// <summary>エリア名(例: 受付、東廊下)</summary>
    private readonly string _name;

    /// <summary>エリア種別</summary>
    private readonly MapAreaCategory _category;

    /// <summary>グリッド上の占有矩形</summary>
    private readonly RectInt _bounds;

    /// <summary>
    /// エリア定義を生成する。
    /// </summary>
    /// <param name="name">エリア名</param>
    /// <param name="category">エリア種別</param>
    /// <param name="bounds">グリッド上の占有矩形</param>
    public MapAreaDefinition(string name, MapAreaCategory category, RectInt bounds)
    {
        _name = name;
        _category = category;
        _bounds = bounds;
    }

    /// <summary>エリア名</summary>
    public string Name => _name;

    /// <summary>エリア種別</summary>
    public MapAreaCategory Category => _category;

    /// <summary>グリッド上の占有矩形</summary>
    public RectInt Bounds => _bounds;

    /// <summary>このエリアが通行ゾーン(廊下・ホール)かどうか</summary>
    public bool IsWalkway => _category != MapAreaCategory.Room;
}
