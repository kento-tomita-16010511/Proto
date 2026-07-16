using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 病院マップの既定レイアウトの構築と検証を行うユーティリティクラス。
/// レイアウト表は Docs/MapDesign.md の 2-1 / 2-2 と必ず1対1で同期させること。
/// 状態は持たず、メソッドのみを提供する。
/// </summary>
public static class HospitalMapLayoutUtility
{
    /// <summary>通行ゾーン(廊下・ホール)を表すゾーンID</summary>
    public const int WalkwayZoneId = 0;

    /// <summary>グリッド外(屋外)を表すゾーンID</summary>
    public const int EmptyZoneId = -1;

    /// <summary>廊下総延長の最低セル数(3mグリッドで100m以上)</summary>
    private const int MinCorridorCellCount = 34;

    /// <summary>隣接セルへの4方向オフセット</summary>
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down,
    };

    /// <summary>
    /// 設計書に基づく既定レイアウトを構築して返す。
    /// </summary>
    /// <returns>病院マップのレイアウト</returns>
    public static HospitalMapLayout CreateDefaultLayout()
    {
        var areas = CreateWalkwayAreas();
        areas.AddRange(CreatePublicRoomAreas());
        areas.AddRange(CreateWardRoomAreas());
        areas.AddRange(CreateStaffRoomAreas());
        return new HospitalMapLayout(areas);
    }

    /// <summary>
    /// レイアウトからゾーングリッドを構築する。
    /// 通行ゾーンは WalkwayZoneId、部屋は 1 以降の固有ID、屋外は EmptyZoneId になる。
    /// </summary>
    /// <param name="layout">対象レイアウト</param>
    /// <returns>セルごとのゾーンID配列</returns>
    public static int[,] BuildZoneGrid(HospitalMapLayout layout)
    {
        var grid = CreateEmptyGrid(layout.GridWidth, layout.GridHeight);
        var roomZoneId = 1;
        foreach (var area in layout.Areas)
        {
            FillZone(grid, area.Bounds, area.IsWalkway ? WalkwayZoneId : roomZoneId++);
        }
        return grid;
    }

    /// <summary>
    /// レイアウトの整合性を検証し、エラーメッセージの一覧を返す(空なら正常)。
    /// </summary>
    /// <param name="layout">対象レイアウト</param>
    /// <returns>エラーメッセージ一覧</returns>
    public static List<string> Validate(HospitalMapLayout layout)
    {
        var errors = new List<string>();
        AddOverlapErrors(layout, errors);
        AddRoomConnectionErrors(layout, errors);
        AddConnectivityErrors(layout, errors);
        AddCorridorLengthErrors(layout, errors);
        return errors;
    }

    /// <summary>
    /// レイアウトの概要(総延長・部屋数など)を文字列で返す。
    /// </summary>
    /// <param name="layout">対象レイアウト</param>
    /// <param name="cellSize">1セルの一辺の長さ(m)</param>
    /// <returns>概要文字列</returns>
    public static string CreateSummary(HospitalMapLayout layout, float cellSize)
    {
        var corridorLength = CountCorridorCells(layout) * cellSize;
        var roomCount = layout.Areas.Count(area => area.Category == MapAreaCategory.Room);
        return $"廊下総延長: {corridorLength:0}m / 部屋数: {roomCount} / エリア総数: {layout.Areas.Count}";
    }

    /// <summary>
    /// 通行ゾーン(廊下・ホール)の全セルを収集して返す。
    /// </summary>
    /// <param name="layout">対象レイアウト</param>
    /// <returns>通行ゾーンのセル集合</returns>
    public static HashSet<Vector2Int> CollectWalkwayCells(HospitalMapLayout layout)
    {
        return layout.Areas
            .Where(area => area.IsWalkway)
            .SelectMany(area => EnumerateCells(area.Bounds))
            .ToHashSet();
    }

    /// <summary>
    /// 矩形内の全セル座標を列挙する。
    /// </summary>
    /// <param name="bounds">対象矩形</param>
    /// <returns>セル座標の列挙</returns>
    public static IEnumerable<Vector2Int> EnumerateCells(RectInt bounds)
    {
        foreach (var cell in bounds.allPositionsWithin)
        {
            yield return cell;
        }
    }

    /// <summary>
    /// 部屋の矩形が通行ゾーンと接する辺(ドア候補)を列挙する。
    /// </summary>
    /// <param name="roomBounds">部屋の矩形</param>
    /// <param name="walkwayCells">通行ゾーンのセル集合</param>
    /// <returns>ドア候補の辺一覧</returns>
    public static List<WallEdge> FindWalkwayEdges(RectInt roomBounds, HashSet<Vector2Int> walkwayCells)
    {
        var edges = new List<WallEdge>();
        foreach (var cell in EnumerateCells(roomBounds))
        {
            AddEdgeCandidates(cell, walkwayCells, edges);
        }
        return edges;
    }

    /// <summary>通行ゾーン(廊下・ホール)のエリア定義を構築する。設計書 2-1 準拠</summary>
    private static List<MapAreaDefinition> CreateWalkwayAreas()
    {
        return new List<MapAreaDefinition>
        {
            new MapAreaDefinition("玄関廊下", MapAreaCategory.Corridor, new RectInt(13, 2, 1, 8)),
            new MapAreaDefinition("中央ホール", MapAreaCategory.Hall, new RectInt(12, 10, 4, 4)),
            new MapAreaDefinition("西廊下", MapAreaCategory.Corridor, new RectInt(4, 11, 8, 1)),
            new MapAreaDefinition("西翼北廊下", MapAreaCategory.Corridor, new RectInt(4, 12, 1, 7)),
            new MapAreaDefinition("サービス廊下", MapAreaCategory.Corridor, new RectInt(4, 5, 1, 6)),
            new MapAreaDefinition("東廊下", MapAreaCategory.Corridor, new RectInt(16, 12, 12, 1)),
            new MapAreaDefinition("東翼廊下", MapAreaCategory.Corridor, new RectInt(27, 13, 1, 6)),
            new MapAreaDefinition("北廊下", MapAreaCategory.Corridor, new RectInt(8, 19, 20, 1)),
            new MapAreaDefinition("中央北廊下", MapAreaCategory.Corridor, new RectInt(14, 14, 1, 9)),
        };
    }

    /// <summary>受付・診察エリアの部屋定義を構築する。設計書 2-2 準拠</summary>
    private static List<MapAreaDefinition> CreatePublicRoomAreas()
    {
        return new List<MapAreaDefinition>
        {
            new MapAreaDefinition("受付", MapAreaCategory.Room, new RectInt(14, 4, 4, 5)),
            new MapAreaDefinition("待合ホール", MapAreaCategory.Room, new RectInt(9, 4, 4, 5)),
            new MapAreaDefinition("トイレ", MapAreaCategory.Room, new RectInt(16, 9, 2, 3)),
            new MapAreaDefinition("診察室1", MapAreaCategory.Room, new RectInt(19, 8, 4, 4)),
            new MapAreaDefinition("診察室2", MapAreaCategory.Room, new RectInt(23, 8, 4, 4)),
            new MapAreaDefinition("薬局", MapAreaCategory.Room, new RectInt(16, 13, 3, 3)),
            new MapAreaDefinition("ナースステーション", MapAreaCategory.Room, new RectInt(20, 13, 4, 3)),
            new MapAreaDefinition("処置室", MapAreaCategory.Room, new RectInt(24, 13, 3, 3)),
        };
    }

    /// <summary>病棟エリアの部屋定義を構築する。設計書 2-2 準拠</summary>
    private static List<MapAreaDefinition> CreateWardRoomAreas()
    {
        return new List<MapAreaDefinition>
        {
            new MapAreaDefinition("病室1", MapAreaCategory.Room, new RectInt(28, 13, 3, 3)),
            new MapAreaDefinition("病室2", MapAreaCategory.Room, new RectInt(28, 16, 3, 3)),
            new MapAreaDefinition("大部屋病室", MapAreaCategory.Room, new RectInt(24, 20, 4, 4)),
            new MapAreaDefinition("病室3", MapAreaCategory.Room, new RectInt(19, 20, 4, 4)),
            new MapAreaDefinition("病室4", MapAreaCategory.Room, new RectInt(15, 20, 3, 3)),
            new MapAreaDefinition("リネン室", MapAreaCategory.Room, new RectInt(9, 20, 3, 2)),
            new MapAreaDefinition("非常階段3", MapAreaCategory.Room, new RectInt(13, 23, 3, 2)),
        };
    }

    /// <summary>管理・サービスエリアの部屋定義を構築する。設計書 2-2 準拠</summary>
    private static List<MapAreaDefinition> CreateStaffRoomAreas()
    {
        return new List<MapAreaDefinition>
        {
            new MapAreaDefinition("事務室", MapAreaCategory.Room, new RectInt(5, 12, 4, 3)),
            new MapAreaDefinition("院長室", MapAreaCategory.Room, new RectInt(5, 15, 3, 3)),
            new MapAreaDefinition("更衣室", MapAreaCategory.Room, new RectInt(5, 8, 3, 3)),
            new MapAreaDefinition("機械室", MapAreaCategory.Room, new RectInt(5, 5, 3, 3)),
            new MapAreaDefinition("サービスヤード", MapAreaCategory.Room, new RectInt(1, 5, 3, 4)),
            new MapAreaDefinition("霊安室", MapAreaCategory.Room, new RectInt(1, 9, 3, 3)),
            new MapAreaDefinition("非常階段1", MapAreaCategory.Room, new RectInt(3, 19, 2, 2)),
            new MapAreaDefinition("非常階段2", MapAreaCategory.Room, new RectInt(6, 18, 2, 2)),
        };
    }

    /// <summary>全セルを屋外IDで初期化したグリッドを生成する</summary>
    private static int[,] CreateEmptyGrid(int width, int height)
    {
        var grid = new int[width, height];
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
            {
                grid[x, z] = EmptyZoneId;
            }
        }
        return grid;
    }

    /// <summary>矩形内のセルを指定ゾーンIDで塗りつぶす</summary>
    private static void FillZone(int[,] grid, RectInt bounds, int zoneId)
    {
        foreach (var cell in EnumerateCells(bounds))
        {
            grid[cell.x, cell.y] = zoneId;
        }
    }

    /// <summary>セルの4方向を調べ、通行ゾーンに面した辺をドア候補として追加する</summary>
    private static void AddEdgeCandidates(Vector2Int cell, HashSet<Vector2Int> walkwayCells, List<WallEdge> edges)
    {
        if (walkwayCells.Contains(cell + Vector2Int.left)) edges.Add(new WallEdge(cell.x, cell.y, true));
        if (walkwayCells.Contains(cell + Vector2Int.right)) edges.Add(new WallEdge(cell.x + 1, cell.y, true));
        if (walkwayCells.Contains(cell + Vector2Int.down)) edges.Add(new WallEdge(cell.x, cell.y, false));
        if (walkwayCells.Contains(cell + Vector2Int.up)) edges.Add(new WallEdge(cell.x, cell.y + 1, false));
    }

    /// <summary>エリア同士の重複エラーを収集する(廊下同士の交差は交差点として許容)</summary>
    private static void AddOverlapErrors(HospitalMapLayout layout, List<string> errors)
    {
        var occupied = new Dictionary<Vector2Int, MapAreaDefinition>();
        foreach (var area in layout.Areas)
        {
            RegisterAreaCells(area, occupied, errors);
        }
    }

    /// <summary>エリアの全セルを占有登録し、不正な重複があればエラーを追加する</summary>
    private static void RegisterAreaCells(MapAreaDefinition area, Dictionary<Vector2Int, MapAreaDefinition> occupied, List<string> errors)
    {
        foreach (var cell in EnumerateCells(area.Bounds))
        {
            RegisterCell(cell, area, occupied, errors);
        }
    }

    /// <summary>セル1個を占有登録する。通行ゾーン同士以外の重複はエラーとする</summary>
    private static void RegisterCell(Vector2Int cell, MapAreaDefinition area, Dictionary<Vector2Int, MapAreaDefinition> occupied, List<string> errors)
    {
        var isConflict = occupied.TryGetValue(cell, out var other) && !(area.IsWalkway && other.IsWalkway);
        if (isConflict)
        {
            errors.Add($"エリア重複: {area.Name} と {other.Name} がセル {cell} で重複しています");
        }
        occupied[cell] = area;
    }

    /// <summary>どの通行ゾーンにも面していない部屋のエラーを収集する</summary>
    private static void AddRoomConnectionErrors(HospitalMapLayout layout, List<string> errors)
    {
        var walkwayCells = CollectWalkwayCells(layout);
        layout.Areas
            .Where(area => area.Category == MapAreaCategory.Room)
            .Where(area => FindWalkwayEdges(area.Bounds, walkwayCells).Count == 0)
            .ToList()
            .ForEach(area => errors.Add($"廊下未接続: {area.Name} はどの廊下・ホールにも面していません"));
    }

    /// <summary>通行ゾーンの分断エラーを収集する</summary>
    private static void AddConnectivityErrors(HospitalMapLayout layout, List<string> errors)
    {
        var walkwayCells = CollectWalkwayCells(layout);
        var reached = CollectReachableCells(walkwayCells);
        if (reached.Count == walkwayCells.Count) return;

        errors.Add($"廊下が分断されています: {walkwayCells.Count - reached.Count} セルが到達不能です");
    }

    /// <summary>先頭セルから到達可能な通行ゾーンセルを幅優先探索で収集する</summary>
    private static HashSet<Vector2Int> CollectReachableCells(HashSet<Vector2Int> walkwayCells)
    {
        var start = walkwayCells.First();
        var reached = new HashSet<Vector2Int> { start };
        var frontier = new Stack<Vector2Int>();
        frontier.Push(start);
        while (frontier.Count > 0)
        {
            PushUnvisitedNeighbors(frontier.Pop(), walkwayCells, reached, frontier);
        }
        return reached;
    }

    /// <summary>未訪問の隣接通行ゾーンセルを探索対象に積む</summary>
    private static void PushUnvisitedNeighbors(Vector2Int cell, HashSet<Vector2Int> walkwayCells, HashSet<Vector2Int> reached, Stack<Vector2Int> frontier)
    {
        Directions
            .Select(direction => cell + direction)
            .Where(walkwayCells.Contains)
            .Where(reached.Add)
            .ToList()
            .ForEach(frontier.Push);
    }

    /// <summary>廊下総延長の不足エラーを収集する</summary>
    private static void AddCorridorLengthErrors(HospitalMapLayout layout, List<string> errors)
    {
        var corridorCellCount = CountCorridorCells(layout);
        if (corridorCellCount >= MinCorridorCellCount) return;

        errors.Add($"廊下総延長不足: {corridorCellCount}セル(必要: {MinCorridorCellCount}セル以上)");
    }

    /// <summary>廊下(ホール除く)の総セル数を重複なしで数える</summary>
    private static int CountCorridorCells(HospitalMapLayout layout)
    {
        return layout.Areas
            .Where(area => area.Category == MapAreaCategory.Corridor)
            .SelectMany(area => EnumerateCells(area.Bounds))
            .Distinct()
            .Count();
    }
}
