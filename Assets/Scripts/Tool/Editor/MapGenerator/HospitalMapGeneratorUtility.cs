using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// レイアウト定義からシーン上に病院マップのGameObject群を生成するユーティリティクラス。
/// 床・壁・ドアはパーツPrefabのインスタンスとして生成し、さらにエリア(部屋・廊下)単位で
/// Prefabアセット化してシーンにはそのインスタンスを配置する(後から部屋単位で編集できる)。
/// 状態は持たず、メソッドのみを提供する。
/// </summary>
public static class HospitalMapGeneratorUtility
{
    /// <summary>生成するマップのルートGameObject名</summary>
    public const string RootObjectName = "HospitalMap";

    /// <summary>エリアPrefabの保存先フォルダ</summary>
    public const string AreaPrefabFolder = "Assets/Prefab/Map/Areas";

    /// <summary>エリア名ラベルの床面からの高さ(m)</summary>
    private const float LabelHeight = 0.05f;

    /// <summary>玄関の開口部(玄関廊下の南端セル(13,2)の南辺)。設計書 3-2 準拠</summary>
    private static readonly WallEdge EntranceEdge = new WallEdge(13, 2, false);

    /// <summary>
    /// 設定アセットに基づいてマップを生成し、ルートGameObjectを返す。
    /// </summary>
    /// <param name="config">マップ生成設定</param>
    /// <param name="overwriteAreaPrefabs">既存のエリアPrefabを作り直すかどうか(falseなら編集済みPrefabを再利用する)</param>
    /// <returns>生成したマップのルートGameObject</returns>
    public static GameObject Generate(HospitalMapConfigModel config, bool overwriteAreaPrefabs)
    {
        var layout = HospitalMapLayoutUtility.CreateDefaultLayout();
        var zoneGrid = HospitalMapLayoutUtility.BuildZoneGrid(layout);
        var areaGrid = HospitalMapLayoutUtility.BuildAreaGrid(layout);
        var walkwayCells = HospitalMapLayoutUtility.CollectWalkwayCells(layout);
        var doorEdges = SelectDoorEdges(layout, walkwayCells, new System.Random(config.RandomSeed));
        var edgesByArea = CollectEdgesByArea(layout, zoneGrid, areaGrid);
        var cellsByArea = CollectCellsByArea(areaGrid);
        var root = new GameObject(RootObjectName);
        CreateAreaInstances(root.transform, layout, cellsByArea, edgesByArea, doorEdges, config, overwriteAreaPrefabs);
        return root;
    }

    /// <summary>玄関と各部屋のドア位置を選定する。部屋のドアは通行ゾーンに面した辺からシード付き乱数で1箇所選ぶ</summary>
    private static HashSet<WallEdge> SelectDoorEdges(HospitalMapLayout layout, HashSet<Vector2Int> walkwayCells, System.Random random)
    {
        var doorEdges = new HashSet<WallEdge> { EntranceEdge };
        layout.Areas
            .Where(area => area.Category == MapAreaCategory.Room)
            .ToList()
            .ForEach(room => AddRoomDoorEdge(doorEdges, room, walkwayCells, random));
        return doorEdges;
    }

    /// <summary>部屋1件のドア位置をドア候補から選んで追加する</summary>
    private static void AddRoomDoorEdge(HashSet<WallEdge> doorEdges, MapAreaDefinition room, HashSet<Vector2Int> walkwayCells, System.Random random)
    {
        var candidates = HospitalMapLayoutUtility.FindWalkwayEdges(room.Bounds, walkwayCells);
        if (candidates.Count == 0) return;

        doorEdges.Add(candidates[random.Next(candidates.Count)]);
    }

    /// <summary>壁・ドアが必要な全ての辺を、所属エリアごとに振り分けて収集する</summary>
    private static Dictionary<int, List<WallEdge>> CollectEdgesByArea(HospitalMapLayout layout, int[,] zoneGrid, int[,] areaGrid)
    {
        var edgesByArea = new Dictionary<int, List<WallEdge>>();
        var width = zoneGrid.GetLength(0);
        var height = zoneGrid.GetLength(1);
        for (var x = 0; x <= width; x++)
        {
            for (var z = 0; z <= height; z++)
            {
                TryAssignEdge(edgesByArea, layout, zoneGrid, areaGrid, new WallEdge(x, z, true));
                TryAssignEdge(edgesByArea, layout, zoneGrid, areaGrid, new WallEdge(x, z, false));
            }
        }
        return edgesByArea;
    }

    /// <summary>辺の両側のゾーンが異なる場合のみ、所有エリアを決めて辺を振り分ける</summary>
    private static void TryAssignEdge(Dictionary<int, List<WallEdge>> edgesByArea, HospitalMapLayout layout, int[,] zoneGrid, int[,] areaGrid, WallEdge edge)
    {
        var sideAX = edge.IsVertical ? edge.X - 1 : edge.X;
        var sideAZ = edge.IsVertical ? edge.Z : edge.Z - 1;
        if (GridValueAt(zoneGrid, sideAX, sideAZ) == GridValueAt(zoneGrid, edge.X, edge.Z)) return;

        var owner = ChooseOwnerAreaIndex(layout, GridValueAt(areaGrid, sideAX, sideAZ), GridValueAt(areaGrid, edge.X, edge.Z));
        AddToGroup(edgesByArea, owner, edge);
    }

    /// <summary>辺の所有エリアを決める。部屋側を優先し、部屋同士はインデックスが小さい方、それ以外は屋内側とする</summary>
    private static int ChooseOwnerAreaIndex(HospitalMapLayout layout, int areaA, int areaB)
    {
        var isRoomA = IsRoomArea(layout, areaA);
        var isRoomB = IsRoomArea(layout, areaB);
        if (isRoomA && isRoomB) return Mathf.Min(areaA, areaB);
        if (isRoomA) return areaA;
        if (isRoomB) return areaB;
        return Mathf.Max(areaA, areaB);
    }

    /// <summary>指定エリアインデックスが部屋かどうかを返す</summary>
    private static bool IsRoomArea(HospitalMapLayout layout, int areaIndex)
    {
        return areaIndex >= 0 && layout.Areas[areaIndex].Category == MapAreaCategory.Room;
    }

    /// <summary>エリアインデックスグリッドから、エリアごとの所有セル一覧を収集する</summary>
    private static Dictionary<int, List<Vector2Int>> CollectCellsByArea(int[,] areaGrid)
    {
        var cellsByArea = new Dictionary<int, List<Vector2Int>>();
        for (var x = 0; x < areaGrid.GetLength(0); x++)
        {
            for (var z = 0; z < areaGrid.GetLength(1); z++)
            {
                AddCellToArea(cellsByArea, areaGrid[x, z], new Vector2Int(x, z));
            }
        }
        return cellsByArea;
    }

    /// <summary>屋外以外のセルを所有エリアのグループへ追加する</summary>
    private static void AddCellToArea(Dictionary<int, List<Vector2Int>> cellsByArea, int areaIndex, Vector2Int cell)
    {
        if (areaIndex == HospitalMapLayoutUtility.EmptyZoneId) return;

        AddToGroup(cellsByArea, areaIndex, cell);
    }

    /// <summary>全エリアをPrefabインスタンスとしてシーンに配置する</summary>
    private static void CreateAreaInstances(Transform root, HospitalMapLayout layout, Dictionary<int, List<Vector2Int>> cellsByArea, Dictionary<int, List<WallEdge>> edgesByArea, HashSet<WallEdge> doorEdges, HospitalMapConfigModel config, bool overwriteAreaPrefabs)
    {
        EditorFolderUtility.EnsureFolder(AreaPrefabFolder);
        for (var index = 0; index < layout.Areas.Count; index++)
        {
            CreateAreaInstance(root, layout.Areas[index],
                GroupOf(cellsByArea, index), GroupOf(edgesByArea, index),
                doorEdges, config, overwriteAreaPrefabs);
        }
    }

    /// <summary>
    /// エリア1件をPrefab化してシーンに配置する。
    /// 既存Prefabがあり上書きしない場合は、編集済みPrefabをそのままインスタンス化する。
    /// </summary>
    private static void CreateAreaInstance(Transform root, MapAreaDefinition area, List<Vector2Int> cells, List<WallEdge> edges, HashSet<WallEdge> doorEdges, HospitalMapConfigModel config, bool overwriteAreaPrefabs)
    {
        var origin = AreaOrigin(area.Bounds, config.CellSize);
        var prefabPath = $"{AreaPrefabFolder}/{area.Name}.prefab";
        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existingPrefab != null && !overwriteAreaPrefabs)
        {
            InstantiatePlaced(existingPrefab, root, origin, Quaternion.identity, area.Name);
            return;
        }
        var areaRoot = BuildAreaObject(root, area, cells, edges, doorEdges, config, origin);
        PrefabUtility.SaveAsPrefabAssetAndConnect(areaRoot, prefabPath, InteractionMode.AutomatedAction);
    }

    /// <summary>エリア1件分の床・壁・天井・ラベルを持つGameObjectを構築する</summary>
    private static GameObject BuildAreaObject(Transform root, MapAreaDefinition area, List<Vector2Int> cells, List<WallEdge> edges, HashSet<WallEdge> doorEdges, HospitalMapConfigModel config, Vector3 origin)
    {
        var areaRoot = new GameObject(area.Name);
        areaRoot.transform.SetParent(root);
        areaRoot.transform.position = origin;
        CreateFloors(areaRoot.transform, cells, config);
        CreateEdgeObjects(areaRoot.transform, edges, doorEdges, config);
        if (config.GenerateCeiling)
        {
            CreateCeilings(areaRoot.transform, cells, config);
        }
        if (config.GenerateAreaLabels && area.Category != MapAreaCategory.Corridor)
        {
            CreateAreaLabel(areaRoot.transform, area, config);
        }
        return areaRoot;
    }

    /// <summary>エリアの所有セルすべてに床を生成する</summary>
    private static void CreateFloors(Transform areaRoot, List<Vector2Int> cells, HospitalMapConfigModel config)
    {
        var floorsParent = CreateChild(areaRoot, "Floors");
        cells.ForEach(cell => InstantiatePlaced(config.FloorPrefab, floorsParent,
            CellCenter(cell, config.CellSize), Quaternion.identity, $"Floor_{cell.x}_{cell.y}"));
    }

    /// <summary>エリアの所有セルすべてに天井を生成する</summary>
    private static void CreateCeilings(Transform areaRoot, List<Vector2Int> cells, HospitalMapConfigModel config)
    {
        var ceilingsParent = CreateChild(areaRoot, "Ceilings");
        cells.ForEach(cell => InstantiatePlaced(config.CeilingPrefab, ceilingsParent,
            CellCenter(cell, config.CellSize) + Vector3.up * config.WallHeight, Quaternion.identity, $"Ceiling_{cell.x}_{cell.y}"));
    }

    /// <summary>エリアの所有する辺すべてに壁またはドアを生成する</summary>
    private static void CreateEdgeObjects(Transform areaRoot, List<WallEdge> edges, HashSet<WallEdge> doorEdges, HospitalMapConfigModel config)
    {
        var wallsParent = CreateChild(areaRoot, "Walls");
        edges.ForEach(edge => CreateEdgeObject(wallsParent, edge, doorEdges.Contains(edge), config));
    }

    /// <summary>辺1本分の壁またはドアを生成する</summary>
    private static void CreateEdgeObject(Transform parent, WallEdge edge, bool isDoor, HospitalMapConfigModel config)
    {
        var prefab = isDoor ? config.DoorPrefab : config.WallPrefab;
        var name = $"{(isDoor ? "Door" : "Wall")}_{edge.X}_{edge.Z}_{(edge.IsVertical ? "V" : "H")}";
        InstantiatePlaced(prefab, parent, EdgeCenter(edge, config.CellSize), EdgeRotation(edge), name);
    }

    /// <summary>エリア名を床面に表示するラベルを生成する(レイアウト確認用)</summary>
    private static void CreateAreaLabel(Transform parent, MapAreaDefinition area, HospitalMapConfigModel config)
    {
        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent);
        labelObject.transform.position = AreaCenter(area.Bounds, config.CellSize) + Vector3.up * LabelHeight;
        labelObject.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        var textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = area.Name;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 48;
        textMesh.characterSize = 0.12f;
        textMesh.color = Color.black;
    }

    /// <summary>グループ辞書へ要素を追加する(グループが無ければ作成する)</summary>
    private static void AddToGroup<T>(Dictionary<int, List<T>> groups, int key, T item)
    {
        if (!groups.TryGetValue(key, out var list))
        {
            list = new List<T>();
            groups[key] = list;
        }
        list.Add(item);
    }

    /// <summary>グループ辞書から要素一覧を取得する(無ければ空リスト)</summary>
    private static List<T> GroupOf<T>(Dictionary<int, List<T>> groups, int key)
    {
        return groups.TryGetValue(key, out var list) ? list : new List<T>();
    }

    /// <summary>グリッド範囲内なら値を、範囲外なら屋外IDを返す</summary>
    private static int GridValueAt(int[,] grid, int x, int z)
    {
        var isInside = x >= 0 && x < grid.GetLength(0) && z >= 0 && z < grid.GetLength(1);
        return isInside ? grid[x, z] : HospitalMapLayoutUtility.EmptyZoneId;
    }

    /// <summary>セル中心のワールド座標(床面)を返す</summary>
    private static Vector3 CellCenter(Vector2Int cell, float cellSize)
    {
        return new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
    }

    /// <summary>エリア矩形の南西角のワールド座標(Prefabの原点)を返す</summary>
    private static Vector3 AreaOrigin(RectInt bounds, float cellSize)
    {
        return new Vector3(bounds.xMin * cellSize, 0f, bounds.yMin * cellSize);
    }

    /// <summary>エリア矩形の中心のワールド座標(床面)を返す</summary>
    private static Vector3 AreaCenter(RectInt bounds, float cellSize)
    {
        return new Vector3(bounds.center.x * cellSize, 0f, bounds.center.y * cellSize);
    }

    /// <summary>辺の中点のワールド座標(床面)を返す</summary>
    private static Vector3 EdgeCenter(WallEdge edge, float cellSize)
    {
        return edge.IsVertical
            ? new Vector3(edge.X * cellSize, 0f, (edge.Z + 0.5f) * cellSize)
            : new Vector3((edge.X + 0.5f) * cellSize, 0f, edge.Z * cellSize);
    }

    /// <summary>辺の向きに応じた壁・ドアの回転を返す(Prefabは東西方向を基準とする)</summary>
    private static Quaternion EdgeRotation(WallEdge edge)
    {
        return edge.IsVertical ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
    }

    /// <summary>空の子GameObjectを生成して返す(ローカル座標は親と一致させる)</summary>
    private static Transform CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    /// <summary>Prefabをインスタンスとしてシーンに配置する。Prefabアセット以外が指定された場合は複製で代替する</summary>
    private static void InstantiatePlaced(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, string name)
    {
        var instance = PrefabUtility.IsPartOfPrefabAsset(prefab)
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)
            : Object.Instantiate(prefab, parent);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.name = name;
    }
}
